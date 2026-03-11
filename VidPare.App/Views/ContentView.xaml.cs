using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using VidPare.Core.Models;
using VidPare.Core.Utilities;
using VidPare.App.Services;

namespace VidPare.App.Views;

public sealed partial class ContentView : UserControl
{
  private VideoDocument? _document;
  private TrimState _trimState = new();
  private MediaPlayer? _mediaPlayer;
  private DispatcherTimer? _playheadTimer;
  private ThumbnailGenerator? _thumbnailGenerator;

  public ContentView()
  {
    InitializeComponent();
  }

  private void Grid_DragOver(object sender, DragEventArgs e)
  {
    e.AcceptedOperation = DataPackageOperation.Copy;
    if (e.DragUIOverride != null)
    {
      e.DragUIOverride.Caption = "Open video";
      e.DragUIOverride.IsCaptionVisible = true;
    }
  }

  private async void Grid_Drop(object sender, DragEventArgs e)
  {
    if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

    var items = await e.DataView.GetStorageItemsAsync();
    if (items.Count == 0) return;

    if (items[0] is StorageFile file && VideoDocument.CanOpen(file.Path))
    {
      await LoadFileAsync(file.Path);
    }
  }

  private async void OpenFile_Click(object sender, RoutedEventArgs e)
  {
    var picker = new FileOpenPicker();
    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

    picker.FileTypeFilter.Add(".mp4");
    picker.FileTypeFilter.Add(".mov");
    picker.FileTypeFilter.Add(".m4v");

    var file = await picker.PickSingleFileAsync();
    if (file != null)
    {
      await LoadFileAsync(file.Path);
    }
  }

  private async Task LoadFileAsync(string filePath)
  {
    // Show loading spinner immediately
    emptyState.Visibility = Visibility.Collapsed;
    editorState.Visibility = Visibility.Collapsed;
    loadingState.Visibility = Visibility.Visible;
    loadingText.Text = "Loading video...";

    // Clean up previous player
    if (_mediaPlayer != null)
    {
      _playheadTimer?.Stop();
      _mediaPlayer.Pause();
      _mediaPlayer.Dispose();
    }

    // Create document with metadata
    _document = await VideoEngine.CreateDocumentAsync(filePath);
    _trimState = new TrimState();
    _trimState.Reset(_document.Duration);

    // Set up media player
    _mediaPlayer = new MediaPlayer();
    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(filePath));
    _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
    playerElement.SetMediaPlayer(_mediaPlayer);

    // Set up playhead timer
    _playheadTimer = new DispatcherTimer();
    _playheadTimer.Interval = TimeSpan.FromMilliseconds(50);
    _playheadTimer.Tick += PlayheadTimer_Tick;
    _playheadTimer.Start();

    // Wire up controls
    playerControls.SetState(_mediaPlayer, _trimState, _document.Duration);
    timelineView.SetState(_trimState, _document.Duration);
    timelineView.SeekRequested += (s, time) =>
    {
      if (_mediaPlayer != null) _mediaPlayer.Position = time;
    };
    _trimState.PropertyChanged += TrimState_PropertyChanged;

    // Update file info
    fileInfoText.Text = $"{_document.FileName}  |  {_document.NaturalWidth}x{_document.NaturalHeight}  |  {_document.CodecName}  |  {_document.FormattedFileSize}";

    // Show editor immediately — don't block on thumbnails
    loadingState.Visibility = Visibility.Collapsed;
    editorState.Visibility = Visibility.Visible;

    // Generate thumbnails in the background (timeline works without them)
    _thumbnailGenerator = new ThumbnailGenerator();
    var thumbnails = await _thumbnailGenerator.GenerateAsync(filePath, _document.Duration, 12);
    timelineView.SetThumbnails(thumbnails);
  }

  private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
  {
    DispatcherQueue.TryEnqueue(() =>
    {
      if (sender.NaturalDuration != TimeSpan.Zero && _document != null)
      {
        _document.Duration = sender.NaturalDuration;
        _trimState.Reset(_document.Duration);
        timelineView.SetState(_trimState, _document.Duration);
        playerControls.SetState(sender, _trimState, _document.Duration);
      }
    });
  }

  private void PlayheadTimer_Tick(object? sender, object e)
  {
    if (_mediaPlayer == null) return;
    var position = _mediaPlayer.Position;
    timelineView.UpdatePlayhead(position);
    playerControls.UpdateTime(position);
  }

  private void TrimState_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
  {
    if (_mediaPlayer == null || _trimState == null) return;

    // Clamp playhead to trim range
    if (_mediaPlayer.Position < _trimState.StartTime)
      _mediaPlayer.Position = _trimState.StartTime;
    else if (_mediaPlayer.Position > _trimState.EndTime)
      _mediaPlayer.Position = _trimState.EndTime;
  }

  private async void Export_Click(object sender, RoutedEventArgs e)
  {
    if (_document == null || _trimState == null) return;

    var dialog = new ExportDialog(_document, _trimState);
    dialog.XamlRoot = this.XamlRoot;
    await dialog.ShowAsync();
  }
}
