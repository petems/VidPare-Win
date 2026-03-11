namespace VidPare.Core.Models;

public class VideoDocument
{
  private static readonly HashSet<string> SupportedExtensions =
    new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".m4v" };

  public string FilePath { get; init; } = string.Empty;
  public string FileName => Path.GetFileName(FilePath);
  public TimeSpan Duration { get; set; }
  public int NaturalWidth { get; set; }
  public int NaturalHeight { get; set; }
  public string CodecName { get; set; } = string.Empty;
  public bool IsHEVC { get; set; }
  public long FileSize { get; set; }

  public static bool CanOpen(string path)
  {
    var ext = Path.GetExtension(path);
    return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
  }

  public string FormattedFileSize
  {
    get
    {
      return FileSize switch
      {
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
      };
    }
  }
}
