using Xunit;
using VidPare.Core.Models;

namespace VidPare.Core.Tests;

public class ExportCapabilitiesTests
{
    [Fact]
    public void AvailableFormats_BothEnabled_ReturnsBoth()
    {
        var caps = new ExportCapabilities { CanEncodeH264 = true, CanEncodeHEVC = true };
        Assert.Contains(ExportFormat.MP4H264, caps.AvailableFormats);
        Assert.Contains(ExportFormat.MP4HEVC, caps.AvailableFormats);
        Assert.Equal(2, caps.AvailableFormats.Count);
    }

    [Fact]
    public void AvailableFormats_HEVCDisabled_ExcludesHEVC()
    {
        var caps = new ExportCapabilities { CanEncodeH264 = true, CanEncodeHEVC = false };
        Assert.Contains(ExportFormat.MP4H264, caps.AvailableFormats);
        Assert.DoesNotContain(ExportFormat.MP4HEVC, caps.AvailableFormats);
        Assert.Single(caps.AvailableFormats);
    }

    [Fact]
    public void AvailableFormats_H264Disabled_ExcludesH264()
    {
        var caps = new ExportCapabilities { CanEncodeH264 = false, CanEncodeHEVC = true };
        Assert.DoesNotContain(ExportFormat.MP4H264, caps.AvailableFormats);
        Assert.Contains(ExportFormat.MP4HEVC, caps.AvailableFormats);
        Assert.Single(caps.AvailableFormats);
    }

    [Fact]
    public void AvailableFormats_BothDisabled_ReturnsEmpty()
    {
        var caps = new ExportCapabilities { CanEncodeH264 = false, CanEncodeHEVC = false };
        Assert.Empty(caps.AvailableFormats);
    }

    [Fact]
    public void AvailableQualities_ReturnsAllFour()
    {
        var caps = new ExportCapabilities();
        Assert.Equal(4, caps.AvailableQualities.Count);
        Assert.Contains(QualityPreset.Original, caps.AvailableQualities);
        Assert.Contains(QualityPreset.High, caps.AvailableQualities);
        Assert.Contains(QualityPreset.Medium, caps.AvailableQualities);
        Assert.Contains(QualityPreset.Low, caps.AvailableQualities);
    }

    [Fact]
    public void Defaults_BothCodecsEnabled()
    {
        var caps = new ExportCapabilities();
        Assert.True(caps.CanEncodeH264);
        Assert.True(caps.CanEncodeHEVC);
    }
}
