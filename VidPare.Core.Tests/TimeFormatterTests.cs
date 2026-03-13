using Xunit;
using VidPare.Core.Utilities;

namespace VidPare.Core.Tests;

public class TimeFormatterTests
{
    [Fact]
    public void Format_Zero_ReturnsPaddedZero()
    {
        Assert.Equal("00:00", TimeFormatter.Format(TimeSpan.Zero));
    }

    [Fact]
    public void Format_SubMinute_ReturnsMMSS()
    {
        Assert.Equal("00:45", TimeFormatter.Format(TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public void Format_Minutes_ReturnsMMSS()
    {
        Assert.Equal("03:05", TimeFormatter.Format(new TimeSpan(0, 3, 5)));
    }

    [Fact]
    public void Format_OneHour_ReturnsHHMMSS()
    {
        Assert.Equal("01:00:00", TimeFormatter.Format(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Format_HourSpanning_ReturnsHHMMSS()
    {
        Assert.Equal("02:15:30", TimeFormatter.Format(new TimeSpan(2, 15, 30)));
    }

    [Fact]
    public void PreciseFormat_Zero_ReturnsZeroWithCentiseconds()
    {
        Assert.Equal("00:00.00", TimeFormatter.PreciseFormat(TimeSpan.Zero));
    }

    [Fact]
    public void PreciseFormat_SubMinute_ReturnsMMSSCs()
    {
        Assert.Equal("00:05.50", TimeFormatter.PreciseFormat(TimeSpan.FromMilliseconds(5500)));
    }

    [Fact]
    public void PreciseFormat_HourSpanning_ReturnsHHMMSSCs()
    {
        Assert.Equal("01:00:00.00", TimeFormatter.PreciseFormat(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void FormatDuration_ReturnsDifference()
    {
        var start = TimeSpan.FromSeconds(10);
        var end = TimeSpan.FromSeconds(70);
        Assert.Equal("01:00", TimeFormatter.FormatDuration(start, end));
    }

    [Fact]
    public void FormatDuration_SameStartAndEnd_ReturnsZero()
    {
        var t = TimeSpan.FromSeconds(30);
        Assert.Equal("00:00", TimeFormatter.FormatDuration(t, t));
    }
}
