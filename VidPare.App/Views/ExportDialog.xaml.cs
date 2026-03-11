using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using VidPare.Core.Models;
using VidPare.Core.Utilities;
using VidPare.App.Services;

namespace VidPare.App.Views;

public sealed partial class ExportDialog : ContentDialog
{
  private readonly VideoDocument _document;
  private readonly TrimState _trimState;
  private CancellationTokenSource? _cts;

  public ExportDialog(VideoDocument document, TrimState trimState)
  {
    InitializeComponent();
    _document = document;
    _trimState = trimState;

    trimInfoText.Text = $"Trim: {TimeFormatter.PreciseFormat(trimState.StartTime)} → {TimeFormatter.PreciseFormat(trimState.EndTime)} ({TimeFormatter.Format(trimState.Duration)})";
  }

  private void FormatPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (_trimState == null) return;
    if (formatPicker?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
    {
      _trimState.ExportFormat = tag == "MP4HEVC" ? ExportFormat.MP4HEVC : ExportFormat.MP4H264;
    }
  }

  private void QualityPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (_trimState == null) return;
    if (qualityPicker?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
    {
      _trimState.QualityPreset = tag switch
      {
        "Original" => QualityPreset.Original,
        "High" => QualityPreset.High,
        "Medium" => QualityPreset.Medium,
        "Low" => QualityPreset.Low,
        _ => QualityPreset.Original
      };
    }
  }

  private async void Export_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
  {
    // Defer closing so we can show progress
    var deferral = args.GetDeferral();

    try
    {
      // Pick save location
      var savePicker = new FileSavePicker();
      var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
      WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

      savePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
      savePicker.FileTypeChoices.Add("MP4 Video", new List<string> { ".mp4" });
      savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(_document.FileName) + "_trimmed";

      var file = await savePicker.PickSaveFileAsync();
      if (file == null)
      {
        args.Cancel = true;
        deferral.Complete();
        return;
      }

      // Show progress, disable buttons
      progressPanel.Visibility = Visibility.Visible;
      IsPrimaryButtonEnabled = false;
      formatPicker.IsEnabled = false;
      qualityPicker.IsEnabled = false;

      _cts = new CancellationTokenSource();

      var progress = new Progress<double>(percent =>
      {
        exportProgress.Value = percent;
        progressText.Text = $"Exporting... {percent:F0}%";
      });

      var engine = new VideoEngine();
      await engine.ExportAsync(
        _document.FilePath,
        file.Path,
        _trimState.StartTime,
        _trimState.EndTime,
        _trimState.ExportFormat,
        _trimState.QualityPreset,
        progress,
        _cts.Token);

      progressText.Text = "Export complete!";
      exportProgress.Value = 100;

      // Show in Explorer
      Process.Start("explorer.exe", $"/select,\"{file.Path}\"");
    }
    catch (OperationCanceledException)
    {
      progressText.Text = "Export cancelled.";
      args.Cancel = true;
    }
    catch (Exception ex)
    {
      progressText.Text = $"Export failed: {ex.Message}";
      args.Cancel = true;
    }
    finally
    {
      deferral.Complete();
    }
  }
}
