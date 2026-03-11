using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VidPare.App.Services;

public class ThumbnailGenerator
{
  public async Task<List<BitmapImage>> GenerateAsync(
    string filePath,
    TimeSpan duration,
    int count)
  {
    var thumbnails = new List<BitmapImage>();
    if (duration == TimeSpan.Zero || count <= 0) return thumbnails;

    try
    {
      var file = await StorageFile.GetFileFromPathAsync(filePath);
      var clip = await MediaClip.CreateFromFileAsync(file);
      var composition = new MediaComposition();
      composition.Clips.Add(clip);

      var interval = duration.TotalSeconds / count;

      for (int i = 0; i < count; i++)
      {
        var timestamp = TimeSpan.FromSeconds(i * interval);
        if (timestamp >= duration)
          timestamp = duration - TimeSpan.FromMilliseconds(100);

        var thumbnail = await composition.GetThumbnailAsync(
          timestamp, 160, 90,
          VideoFramePrecision.NearestKeyFrame);

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(thumbnail);
        thumbnails.Add(bitmap);
      }
    }
    catch
    {
      // If thumbnail generation fails, return empty list — timeline works without them
    }

    return thumbnails;
  }
}
