namespace VidPare.Core.Utilities;

public static class TimeFormatter
{
  public static string Format(TimeSpan time)
  {
    if (time.TotalHours >= 1)
      return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    return $"{time.Minutes:D2}:{time.Seconds:D2}";
  }

  public static string PreciseFormat(TimeSpan time)
  {
    if (time.TotalHours >= 1)
      return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
    return $"{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
  }

  public static string FormatDuration(TimeSpan start, TimeSpan end)
  {
    var duration = end - start;
    return Format(duration);
  }
}
