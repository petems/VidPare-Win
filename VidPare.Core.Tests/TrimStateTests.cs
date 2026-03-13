using Xunit;
using VidPare.Core.Models;

namespace VidPare.Core.Tests;

public class TrimStateTests
{
    [Fact]
    public void Duration_ReturnsEndMinusStart()
    {
        var state = new TrimState
        {
            StartTime = TimeSpan.FromSeconds(10),
            EndTime = TimeSpan.FromSeconds(40)
        };
        Assert.Equal(TimeSpan.FromSeconds(30), state.Duration);
    }

    [Fact]
    public void Duration_ZeroWhenStartEqualsEnd()
    {
        var t = TimeSpan.FromSeconds(15);
        var state = new TrimState { StartTime = t, EndTime = t };
        Assert.Equal(TimeSpan.Zero, state.Duration);
    }

    [Fact]
    public void Reset_SetsStartToZeroAndEndToDuration()
    {
        var state = new TrimState
        {
            StartTime = TimeSpan.FromSeconds(5),
            EndTime = TimeSpan.FromSeconds(20)
        };
        var videoDuration = TimeSpan.FromSeconds(60);

        state.Reset(videoDuration);

        Assert.Equal(TimeSpan.Zero, state.StartTime);
        Assert.Equal(videoDuration, state.EndTime);
    }

    [Fact]
    public void Defaults_AreH264AndOriginal()
    {
        var state = new TrimState();
        Assert.Equal(ExportFormat.MP4H264, state.ExportFormat);
        Assert.Equal(QualityPreset.Original, state.QualityPreset);
    }

    [Fact]
    public void StartTime_RaisesPropertyChanged()
    {
        var state = new TrimState();
        var raised = false;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrimState.StartTime)) raised = true;
        };

        state.StartTime = TimeSpan.FromSeconds(5);

        Assert.True(raised);
    }

    [Fact]
    public void EndTime_RaisesPropertyChanged()
    {
        var state = new TrimState();
        var raised = false;
        state.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrimState.EndTime)) raised = true;
        };

        state.EndTime = TimeSpan.FromSeconds(30);

        Assert.True(raised);
    }
}
