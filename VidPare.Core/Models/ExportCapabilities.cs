namespace VidPare.Core.Models;

public class ExportCapabilities
{
  public bool CanEncodeHEVC { get; set; } = true; // Windows 11 has HEVC pre-installed
  public bool CanEncodeH264 { get; set; } = true;

  public IReadOnlyList<ExportFormat> AvailableFormats
  {
    get
    {
      var formats = new List<ExportFormat>();
      if (CanEncodeH264) formats.Add(ExportFormat.MP4H264);
      if (CanEncodeHEVC) formats.Add(ExportFormat.MP4HEVC);
      return formats;
    }
  }

  public IReadOnlyList<QualityPreset> AvailableQualities =>
    [QualityPreset.Original, QualityPreset.High, QualityPreset.Medium, QualityPreset.Low];
}
