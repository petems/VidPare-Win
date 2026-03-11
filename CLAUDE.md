# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```batch
build.bat           # Runs dotnet build (sets up VS2022 Community x64 env)
dotnet build        # Direct build command
```

Run from Visual Studio or:
```batch
dotnet run --project VidPare.App
```

**Requirements:** Visual Studio 2022 Community, Windows 10.0.22000+ (Windows 11), x64 only.

## Architecture

VidPare is a WinUI 3 Windows application for video trimming and export.

**Two-project solution:**
- `VidPare.Core` — .NET 8 class library: models (`VideoDocument`, `TrimState`, `ExportCapabilities`) and utilities (`TimeFormatter`)
- `VidPare.App` — .NET 8 WinUI 3 app: views, services, entry point

**Data flow:**
1. User loads a video (drag-drop or file picker) → `VideoEngine.CreateDocumentAsync()` → `VideoDocument`
2. `MediaPlayer` handles playback; position tracked on a 50ms timer
3. `TimelineView` renders a thumbnail strip + trim handles + playhead on a `Canvas`
4. `TrimState` (MVVM `ObservableObject`) holds trim in/out points; views react via `PropertyChanged`
5. `ExportDialog` collects format/quality → `VideoEngine.ExportAsync()` → Windows `MediaTranscoder`

**Key services (`VidPare.App/Services/`):**
- `VideoEngine` — wraps `MediaTranscoder` and `MediaComposition` for metadata extraction and transcoding
- `ThumbnailGenerator` — generates 12 thumbnail frames at 160×90 using `Windows.Media.Editing`

**Key views (`VidPare.App/Views/`):**
- `ContentView` — top-level editor container; manages loading state (Empty / Loading / Editor)
- `TimelineView` — custom Canvas-based scrubber with thumbnail strip, trim handles (24px hit zones), and playhead drag
- `PlayerControlsView` — play/pause, set in/out point buttons; raises `SeekRequested` events
- `ExportDialog` — format (H.264 / HEVC) and quality preset selection with progress tracking

## Tech Stack

- **UI:** Windows App SDK (WinUI 3) v1.6, XAML
- **MVVM:** `CommunityToolkit.Mvvm` v8 (`ObservableObject`, `[ObservableProperty]`)
- **Media APIs:** `Windows.Media.Transcoding`, `Windows.Media.Editing`, `Windows.Media.Playback`, `Windows.Media.Core`
- **Language:** C# with `Nullable=enable`, `ImplicitUsings=enable`, file-scoped namespaces
- **Packaging:** `WindowsPackageType=None` (standalone, not MSIX/UWP)

## Supported Formats

Input: `.mp4`, `.mov`, `.m4v`. Output codecs: H.264 (`MP4H264`) and HEVC/H.265 (`MP4HEVC`). HEVC requires the Windows 11 pre-installed codec.
