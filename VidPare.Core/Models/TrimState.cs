using CommunityToolkit.Mvvm.ComponentModel;

namespace VidPare.Core.Models;

public enum ExportFormat
{
  MP4H264,
  MP4HEVC
}

public enum QualityPreset
{
  Original,
  High,
  Medium,
  Low
}

public partial class TrimState : ObservableObject
{
  [ObservableProperty]
  private TimeSpan startTime;

  [ObservableProperty]
  private TimeSpan endTime;

  [ObservableProperty]
  private ExportFormat exportFormat = ExportFormat.MP4H264;

  [ObservableProperty]
  private QualityPreset qualityPreset = QualityPreset.Original;

  public TimeSpan Duration => EndTime - StartTime;

  public void Reset(TimeSpan videoDuration)
  {
    StartTime = TimeSpan.Zero;
    EndTime = videoDuration;
  }
}
