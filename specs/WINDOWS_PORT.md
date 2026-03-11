# VidPare Windows MVP Plan

## Context

Port VidPare's core trimming functionality from macOS/Swift/AVFoundation to Windows 11 using
C#/.NET 8/WinUI 3/Windows Media Foundation. The goal is the minimum viable app that can open a
video, display it with a trim timeline, and export a trimmed clip — running natively on the current
Windows 11 dev machine with no ffmpeg dependency.

This plan incorporates corrections from WINDOWS_CRITIQUE.md:
- Uses `MediaTranscoder` (high-level WinRT) instead of raw `IMFSourceReader/SinkWriter` for all exports
- Drops Native AOT (incompatible with WinUI 3); uses ReadyToRun instead
- Uses the new `Microsoft.Windows.Storage.Pickers` API (WindowApp SDK 1.6+) which takes `WindowId`
  directly — no `InitializeWithWindow.Initialize()` boilerplate
- Skips passthrough remux (complex DTS/PTS B-frame issues) for MVP; all exports go through MediaTranscoder
- Splits into `VidPare.Core` (platform-neutral) + `VidPare.App` (WinUI 3) for testability
- HEVC is pre-installed on Windows 11 — no codec pack needed for this machine

---

## MVP Scope (what is IN)

- Open video via drag-drop or File > Open (MP4, MOV, M4V)
- Playback via `MediaPlayerElement` (no custom player view needed)
- Interactive timeline: thumbnail strip + draggable trim handles + playhead
- Player controls: play/pause, current time display, set in/out points
- Export dialog: format picker (MP4 H.264 / MP4 HEVC), quality picker (High/Medium/Low), progress bar
- Save to file via `FileSavePicker`
- Single `.exe` via `dotnet publish --self-contained`

## MVP Scope (what is OUT for now)

- Passthrough/lossless remux (deferred — too complex for MVP per CRITIQUE Issue #1)
- MOV output (no native Windows MF sink)
- AXAutomation / DemoRecorder
- Snapshot & acceptance tests
- Taskbar progress, jump lists, toast notifications, shell file association
- MSIX packaging / code signing
- CI/CD pipeline

---

## Phase 0: Dev Environment Setup

### Required tools (run in PowerShell as user, no elevation needed for winget)

```powershell
# 1. Visual Studio 2022 Community — includes .NET 8, Windows SDK, WinUI 3 templates, XAML designer
winget install Microsoft.VisualStudio.2022.Community
# During install: select workload "Windows application development"
# This pulls in: Windows 11 SDK (10.0.22621), .NET 8, Windows App SDK 1.6, XAML Diagnostics

# 2. Verify .NET 8 SDK is available after VS install
dotnet --version   # should print 8.x.x

# 3. dotnet-format for code style (optional but matches pre-commit hook pattern)
dotnet tool install -g dotnet-format

# 4. Git already present (we're in a git repo)
```

### Why VS 2022 Community (not VS Code)?
WinUI 3 XAML has no hot-reload or designer in VS Code. VS 2022 provides:
- XAML Live Visual Tree / XAML Hot Reload
- WinUI 3 item templates (UserControl, ResourceDictionary, etc.)
- Integrated Windows App SDK deployment

---

## Phase 1: Project Scaffold

### Directory layout (sibling to existing Swift project)

```
C:\Users\mryok\projects\
├── VidPare/              ← existing macOS repo (unchanged)
└── VidPareWin/           ← new Windows project
    ├── VidPare.sln
    ├── VidPare.Core/     ← net8.0, no Windows APIs
    │   ├── VidPare.Core.csproj
    │   ├── Models/
    │   │   ├── VideoDocument.cs
    │   │   ├── TrimState.cs
    │   │   └── ExportCapabilities.cs
    │   └── Utilities/
    │       └── TimeFormatter.cs
    └── VidPare.App/      ← net8.0-windows10.0.22000.0, WinUI 3
        ├── VidPare.App.csproj
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / MainWindow.xaml.cs
        ├── Views/
        │   ├── ContentView.xaml / .cs
        │   ├── VideoPlayerView.xaml / .cs   (thin wrapper, may just be MediaPlayerElement inline)
        │   ├── TimelineView.xaml / .cs
        │   ├── PlayerControlsView.xaml / .cs
        │   └── ExportDialog.xaml / .cs
        └── Services/
            ├── VideoEngine.cs
            └── ThumbnailGenerator.cs
```

### Create in PowerShell

```powershell
cd C:\Users\mryok\projects
mkdir VidPareWin; cd VidPareWin
dotnet new sln -n VidPare

# Core library (platform-neutral)
dotnet new classlib -n VidPare.Core -f net8.0
dotnet sln add VidPare.Core/VidPare.Core.csproj

# App (WinUI 3 — use VS template via: File > New > Project > "Blank App, Unpackaged (WinUI 3 in Desktop)")
# Name it VidPare.App, place in VidPareWin\VidPare.App\
dotnet sln add VidPare.App/VidPare.App.csproj

# Add Core reference to App
dotnet add VidPare.App/VidPare.App.csproj reference VidPare.Core/VidPare.Core.csproj
```

> Use **Unpackaged** WinUI 3 template for MVP — no MSIX identity needed, simpler F5 debugging.

---

## Phase 2: VidPare.Core — Models & Utilities

### VidPare.Core.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
  </ItemGroup>
</Project>
```

### Models to port (1:1 from Swift, no platform APIs)

**VideoDocument.cs** — mirrors `VideoDocument.swift`
- `string FilePath`, `TimeSpan Duration`, `(int W, int H) NaturalSize`, `string CodecName`, `bool IsHEVC`, `long FileSize`
- Metadata loading happens in `VidPare.App` via `IMFSourceReader` or `MediaClip` — Core only holds the data
- `static bool CanOpen(string path)` checks extension: `.mp4`, `.mov`, `.m4v`

**TrimState.cs** — mirrors `TrimState.swift`
```csharp
[ObservableObject]
public partial class TrimState
{
    [ObservableProperty] private TimeSpan startTime;
    [ObservableProperty] private TimeSpan endTime;
    [ObservableProperty] private ExportFormat exportFormat = ExportFormat.MP4H264;
    [ObservableProperty] private QualityPreset qualityPreset = QualityPreset.High;
    public TimeSpan Duration => EndTime - StartTime;
    public void Reset(TimeSpan videoDuration) { StartTime = TimeSpan.Zero; EndTime = videoDuration; }
}

public enum ExportFormat { MP4H264, MP4HEVC }           // MOV dropped — no native MF sink
public enum QualityPreset { High, Medium, Low }          // Passthrough deferred to post-MVP
```

**ExportCapabilities.cs** — simplified for MVP
- Remove passthrough logic entirely for now
- HEVC encode check: call back via interface from App layer (Core can't reference Windows MF)
- Or keep as a simple struct that App layer populates

**TimeFormatter.cs** — pure string formatting, `CMTime` → `TimeSpan`
- `string(from: TimeSpan)` → "HH:MM:SS" or "MM:SS"
- `preciseString(from: TimeSpan)` → "MM:SS.ff"
- Translates 1:1 — zero platform APIs

---

## Phase 3: VidPare.App — csproj & Entry Point

### VidPare.App.csproj key settings

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.22000.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <WindowsPackageType>None</WindowsPackageType>  <!-- Unpackaged -->
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.*" />
    <PackageReference Include="CommunityToolkit.Mvvm"   Version="8.*"  />
  </ItemGroup>
</Project>
```

### App.xaml.cs entry point

```csharp
public partial class App : Application
{
    public static MainWindow MainWindow { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
```

### MainWindow.xaml.cs — size + title

```csharp
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32(960, 640));   // AppWindow is directly on Window in WinUI 3
        Title = "VidPare";
    }
}
```

> Note: In WinUI 3 (Windows App SDK), `this.AppWindow` is available directly on `Window` —
> no Win32 HWND dance needed unless targeting older SDK versions.

---

## Phase 4: Key View Implementations

### ContentView — File drop target + editor layout

**Drop target (XAML):**
```xaml
<Grid AllowDrop="True" DragOver="Grid_DragOver" Drop="Grid_Drop">
    <!-- empty state: show drop prompt -->
    <!-- loaded state: VideoPlayerView + PlayerControlsView + TimelineView -->
</Grid>
```

**Drop handling (C#):**
```csharp
private void Grid_DragOver(object sender, DragEventArgs e)
{
    e.AcceptedOperation = DataPackageOperation.Copy;
    e.DragUIOverride.Caption = "Open video";
}

private async void Grid_Drop(object sender, DragEventArgs e)
{
    var items = await e.DataView.GetStorageItemsAsync();
    if (items.FirstOrDefault() is StorageFile file && VideoDocument.CanOpen(file.Path))
        await LoadFile(file.Path);
}
```

**File Open Picker** (new Windows App SDK 1.6 API — no InitializeWithWindow needed):
```csharp
var picker = new FileOpenPicker(this.AppWindow.Id);  // WindowId passed directly
picker.FileTypeFilter.Add(".mp4");
picker.FileTypeFilter.Add(".mov");
picker.FileTypeFilter.Add(".m4v");
var file = await picker.PickSingleFileAsync();
```

### VideoPlayerView — MediaPlayerElement (simpler than macOS NSViewRepresentable)

```xaml
<MediaPlayerElement x:Name="playerElement"
                    AreTransportControlsEnabled="False"
                    Stretch="Uniform"
                    HorizontalAlignment="Stretch"
                    VerticalAlignment="Stretch" />
```

```csharp
// Attach player after loading file
var player = new MediaPlayer();
player.Source = MediaSource.CreateFromUri(new Uri(filePath));
playerElement.SetMediaPlayer(player);
```

No custom NSView, no AVPlayerLayer, no CATransaction — native XAML control handles it all.

### TimelineView — Canvas with pointer events

Direct port of coordinate math from `TimelineView.swift` (pure arithmetic, no platform APIs).

```xaml
<Canvas x:Name="timelineCanvas"
        PointerPressed="Timeline_PointerPressed"
        PointerMoved="Timeline_PointerMoved"
        PointerReleased="Timeline_PointerReleased"
        MinHeight="60" />
```

Elements drawn in code-behind:
- Thumbnail `Image` controls positioned via `Canvas.SetLeft`
- Dim `Rectangle` overlays (outside trim region) with 0.5 opacity black fill
- Accent-color `Rectangle` for trim region border
- White 2px `Line` for playhead
- 12px wide `Rectangle` handles for trim start/end

Drag gesture → `PointerMoved` with captured pointer (`Canvas.CapturePointer(e.Pointer)`)

### ExportDialog — ContentDialog + MediaTranscoder

```xaml
<ContentDialog x:Name="exportDialog" Title="Export">
    <!-- Format picker: ComboBox bound to ExportFormat enum -->
    <!-- Quality picker: ComboBox bound to QualityPreset enum -->
    <!-- Progress bar during export -->
</ContentDialog>
```

**MediaTranscoder for re-encode (High/Medium/Low):**
```csharp
// VideoEngine.cs
public async Task ExportAsync(string inputPath, string outputPath,
    TimeSpan trimStart, TimeSpan trimEnd,
    ExportFormat format, QualityPreset quality,
    IProgress<double> progress, CancellationToken ct)
{
    var transcoder = new MediaTranscoder();
    transcoder.TrimStartTime = trimStart;
    transcoder.TrimStopTime = trimEnd;

    var quality = quality switch {
        QualityPreset.High   => VideoEncodingQuality.HD1080p,
        QualityPreset.Medium => VideoEncodingQuality.HD720p,
        QualityPreset.Low    => VideoEncodingQuality.Wvga,
        _                    => VideoEncodingQuality.HD1080p
    };

    var profile = format == ExportFormat.MP4HEVC
        ? MediaEncodingProfile.CreateHevc(quality)
        : MediaEncodingProfile.CreateMp4(quality);

    var source = await StorageFile.GetFileFromPathAsync(inputPath);
    var dest   = await StorageFile.GetFileFromPathAsync(outputPath);

    var op = await transcoder.PrepareFileTranscodeAsync(source, dest, profile);
    if (!op.CanTranscode)
        throw new InvalidOperationException($"Cannot transcode: {op.FailureReason}");

    await op.TranscodeAsync().AsTask(ct, progress);
}
```

**FileSavePicker for output path:**
```csharp
var savePicker = new FileSavePicker(App.MainWindow.AppWindow.Id);
savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
savePicker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });
savePicker.SuggestedFileName = suggestedOutputName;
var file = await savePicker.PickSaveFileAsync();
```

**Show in Explorer after export:**
```csharp
Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
```

---

## Phase 5: ThumbnailGenerator

Uses `Windows.Media.Editing.MediaClip` for simple frame extraction (higher-level than IMFSourceReader):

```csharp
// For MVP: use MediaComposition to extract frames at evenly-spaced timestamps
var clip = await MediaClip.CreateFromFileAsync(storageFile);
var composition = new MediaComposition();
composition.Clips.Add(clip);

// Generate thumbnails via GetThumbnailAsync
var thumbnail = await composition.GetThumbnailAsync(
    timestamp, (int)maxWidth, (int)maxHeight, VideoFramePrecision.NearestKeyFrame);
// thumbnail is an ImageStream — convert to BitmapImage for display
```

If `MediaComposition` thumbnail quality is insufficient, fall back to `IMFSourceReader` with
`MF_SOURCE_READER_ENABLE_ADVANCED_VIDEO_PROCESSING` (handles rotation). This is more complex but
mirrors the macOS `AVAssetImageGenerator` approach exactly.

---

## Phase 6: Build & Run

```powershell
# From VidPareWin\ in PowerShell
dotnet build VidPare.App -c Debug
dotnet run --project VidPare.App   # or F5 in VS 2022

# Release single-file publish (ReadyToRun, not AOT — WinUI 3 is AOT-incompatible)
dotnet publish VidPare.App -r win-x64 -c Release `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true
# Output: VidPare.App\bin\Release\net8.0-windows10.0.22000.0\win-x64\publish\VidPare.App.exe
```

---

## Critical Files to Create

| File | Notes |
|------|-------|
| `VidPare.Core/VidPare.Core.csproj` | net8.0, CommunityToolkit.Mvvm only |
| `VidPare.Core/Models/VideoDocument.cs` | Data container, CanOpen() |
| `VidPare.Core/Models/TrimState.cs` | [ObservableProperty] fields, enums |
| `VidPare.Core/Utilities/TimeFormatter.cs` | Port from TimeFormatter.swift 1:1 |
| `VidPare.App/VidPare.App.csproj` | WinUI 3 unpackaged, WindowsAppSDK 1.6 |
| `VidPare.App/App.xaml(.cs)` | OnLaunched → new MainWindow().Activate() |
| `VidPare.App/MainWindow.xaml(.cs)` | AppWindow.Resize(960, 640) |
| `VidPare.App/Views/ContentView.xaml(.cs)` | Drop target + editor layout |
| `VidPare.App/Views/TimelineView.xaml(.cs)` | Canvas, pointer events, coordinate math |
| `VidPare.App/Views/PlayerControlsView.xaml(.cs)` | Play/pause, time display, I/O buttons |
| `VidPare.App/Views/ExportDialog.xaml(.cs)` | ContentDialog, format/quality pickers |
| `VidPare.App/Services/VideoEngine.cs` | MediaTranscoder trim+encode |
| `VidPare.App/Services/ThumbnailGenerator.cs` | MediaClip/MediaComposition frame extraction |

---

## Verification

1. **Build check**: `dotnet build` in `VidPareWin\` — zero errors
2. **Launch**: F5 in VS 2022 — app window appears at 960×640
3. **Drop test**: drag an `.mp4` onto the window → video plays in MediaPlayerElement
4. **Trim test**: drag handles on timeline → trim region updates, playhead stays in range
5. **Export test**: click Export → ContentDialog → choose High quality → FileSavePicker → file written to disk → "Show in Explorer" reveals it
6. **HEVC test**: choose MP4 HEVC export on Windows 11 → succeeds (codec pre-installed)
7. **Cancel test**: start export, click Cancel → operation stops cleanly

---

## Post-MVP additions (in order of value)

1. Passthrough remux (lossless trim) — biggest UX win but most complex
2. Unit tests for Core models (xUnit, runs on any platform)
3. Taskbar export progress (`ITaskbarList3::SetProgressValue`)
4. Toast notification on export complete
5. Shell file association (right-click `.mp4` → "Trim with VidPare")
6. MSIX packaging for clean install/uninstall
