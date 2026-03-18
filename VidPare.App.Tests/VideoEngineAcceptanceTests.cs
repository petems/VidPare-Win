using Xunit;
using VidPare.App.Services;
using VidPare.Core.Models;

namespace VidPare.App.Tests;

public class VideoEngineAcceptanceTests
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
                quality: QualityPreset.Medium,
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
            Assert.Equal(100.0, progressValues.Last(), precision: 2);
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
                    ExportFormat.MP4H264, QualityPreset.Medium,
                    new Progress<double>(),
                    cts.Token));
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }
}
