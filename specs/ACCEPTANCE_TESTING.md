# Acceptance Testing Plan

## Overview

A separate `VidPare.App.Tests` project tests `VideoEngine` and `ThumbnailGenerator` end-to-end
against a real MP4 fixture file. These tests require Windows with Windows App SDK installed and
run on the `windows-latest` GitHub Actions runner.

## Project Structure

```
VidPare.App.Tests/
  VidPare.App.Tests.csproj
  AppRuntimeFixture.cs          # Bootstraps Windows App SDK runtime once per test run
  VideoEngineAcceptanceTests.cs
  ThumbnailGeneratorAcceptanceTests.cs
  TestFixtures/
    sample.mp4                  # Small synthetic test video (committed to repo)
```

Add the project to the solution:

```
dotnet sln add VidPare.App.Tests/VidPare.App.Tests.csproj
```

## Generating the Test Fixture

Generate `TestFixtures/sample.mp4` locally with FFmpeg and commit it:

```bash
ffmpeg -f lavfi -i "testsrc=duration=3:size=320x240:rate=30" \
       -f lavfi -i "sine=frequency=440:duration=3" \
       -c:v libx264 -c:a aac -shortest \
       VidPare.App.Tests/TestFixtures/sample.mp4
```

A 3-second 320×240 H.264 file is ~100 KB — acceptable to keep in source control.

## Project File

```xml
<!-- VidPare.App.Tests/VidPare.App.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.22000.0</TargetFramework>
    <Platforms>x64</Platforms>
    <RuntimeIdentifiers>win-x64</RuntimeIdentifiers>
    <IsTestProject>true</IsTestProject>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.*" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="coverlet.collector" Version="6.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\VidPare.App\VidPare.App.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Update="TestFixtures\sample.mp4" CopyToOutputDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

If `VideoEngine` is `internal`, add to `VidPare.App.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="VidPare.App.Tests" />
</ItemGroup>
```

## Windows App SDK Bootstrap

WinRT APIs need the Windows App SDK runtime initialized before any test runs.
With xUnit v2, use a shared `IClassFixture`:

```csharp
// VidPare.App.Tests/AppRuntimeFixture.cs
namespace VidPare.App.Tests;

public class AppRuntimeFixture : IDisposable
{
    public AppRuntimeFixture()
    {
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(0x00010006);
    }

    public void Dispose()
    {
        Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Shutdown();
    }
}
```

## VideoEngine Acceptance Tests

```csharp
// VidPare.App.Tests/VideoEngineAcceptanceTests.cs
namespace VidPare.App.Tests;

public class VideoEngineAcceptanceTests : IClassFixture<AppRuntimeFixture>
{
    private static readonly string SampleMp4 = Path.Combine(
        AppContext.BaseDirectory, "TestFixtures", "sample.mp4");

    [Fact]
    public async Task CreateDocumentAsync_RealMp4_ReturnsCorrectMetadata()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);

        Assert.Equal("sample.mp4", doc.FileName);
        Assert.True(doc.Duration > TimeSpan.Zero);
        Assert.True(doc.NaturalWidth > 0);
        Assert.True(doc.NaturalHeight > 0);
        Assert.True(doc.FileSize > 0);
    }

    [Fact]
    public async Task ExportAsync_TrimsVideo_ProducesOutputFile()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);
        var output = Path.Combine(Path.GetTempPath(), $"vidpare_test_{Guid.NewGuid()}.mp4");

        try
        {
            var engine = new VideoEngine();
            var trimEnd = doc.Duration < TimeSpan.FromSeconds(2)
                ? doc.Duration
                : TimeSpan.FromSeconds(2);

            await engine.ExportAsync(
                SampleMp4, output,
                trimStart: TimeSpan.Zero,
                trimEnd: trimEnd,
                format: ExportFormat.MP4H264,
                quality: QualityPreset.Original,
                progress: new Progress<double>(),
                ct: CancellationToken.None);

            Assert.True(File.Exists(output));
            Assert.True(new FileInfo(output).Length > 0);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ExportAsync_ReportsProgressToCompletion()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);
        var output = Path.Combine(Path.GetTempPath(), $"vidpare_test_{Guid.NewGuid()}.mp4");
        var progressValues = new List<double>();

        try
        {
            var engine = new VideoEngine();
            await engine.ExportAsync(
                SampleMp4, output,
                TimeSpan.Zero, doc.Duration,
                ExportFormat.MP4H264, QualityPreset.High,
                new Progress<double>(v => progressValues.Add(v)),
                CancellationToken.None);

            Assert.NotEmpty(progressValues);
            Assert.Equal(1.0, progressValues.Last(), precision: 2);
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Fact]
    public async Task ExportAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);
        var output = Path.Combine(Path.GetTempPath(), $"vidpare_test_{Guid.NewGuid()}.mp4");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var engine = new VideoEngine();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                engine.ExportAsync(
                    SampleMp4, output,
                    TimeSpan.Zero, doc.Duration,
                    ExportFormat.MP4H264, QualityPreset.Original,
                    new Progress<double>(),
                    cts.Token));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }
}
```

## ThumbnailGenerator Acceptance Tests

```csharp
// VidPare.App.Tests/ThumbnailGeneratorAcceptanceTests.cs
namespace VidPare.App.Tests;

public class ThumbnailGeneratorAcceptanceTests : IClassFixture<AppRuntimeFixture>
{
    private static readonly string SampleMp4 = Path.Combine(
        AppContext.BaseDirectory, "TestFixtures", "sample.mp4");

    [Fact]
    public async Task GenerateAsync_RealMp4_Returns12Thumbnails()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);
        var generator = new ThumbnailGenerator();

        var thumbnails = await generator.GenerateAsync(SampleMp4, doc.Duration, count: 12);

        Assert.Equal(12, thumbnails.Count);
        Assert.All(thumbnails, t => Assert.NotNull(t));
    }

    [Fact]
    public async Task GenerateAsync_RealMp4_ThumbnailsHaveExpectedDimensions()
    {
        var doc = await VideoEngine.CreateDocumentAsync(SampleMp4);
        var generator = new ThumbnailGenerator();

        var thumbnails = await generator.GenerateAsync(SampleMp4, doc.Duration, count: 1);

        Assert.Single(thumbnails);
        Assert.Equal(160u, thumbnails[0].PixelWidth);
        Assert.Equal(90u, thumbnails[0].PixelHeight);
    }
}
```

## Running the Tests

```bash
# From repo root (requires Windows with Windows App SDK)
dotnet test VidPare.App.Tests --configuration Release --runtime win-x64
```

## CI Integration

Add an acceptance test step to the GitHub Actions CI workflow after the existing unit test step:

```yaml
- name: Run acceptance tests
  run: dotnet test VidPare.App.Tests --configuration Release --runtime win-x64 --logger "trx;LogFileName=acceptance.trx"

- name: Upload acceptance test results
  if: always()
  uses: actions/upload-artifact@v4
  with:
    name: acceptance-test-results
    path: "**/*.trx"
```

The `windows-latest` runner includes the Windows App SDK runtime when using
`WindowsPackageType=None` — the runtime ships with the NuGet package.

## Notes

- These tests are intentionally slow (real transcoding) — do not run them in hot-reload/watch mode.
- The cancellation test may produce a partial output file; the `finally` block cleans it up.
- HEVC export tests require the Windows 11 pre-installed codec and should be skipped on machines
  where `ExportCapabilities.CanEncodeHEVC` returns `false`.
