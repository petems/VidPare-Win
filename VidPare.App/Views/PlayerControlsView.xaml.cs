using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Media.Playback;
using VidPare.Core.Models;
using VidPare.Core.Utilities;

namespace VidPare.App.Views;

public sealed partial class PlayerControlsView : UserControl
{
  private MediaPlayer? _player;
  private TrimState? _trimState;
  private TimeSpan _duration;
  private bool _isPlaying;

  public PlayerControlsView()
  {
    InitializeComponent();
  }

  public void SetState(MediaPlayer player, TrimState trimState, TimeSpan duration)
  {
    _player = player;
    _trimState = trimState;
    _duration = duration;
    _isPlaying = false;

    _player.MediaEnded += (s, e) =>
    {
      DispatcherQueue.TryEnqueue(() =>
      {
        _isPlaying = false;
        playPauseIcon.Glyph = "\uE768"; // Play icon
      });
    };

    UpdateTrimDuration();
    _trimState.PropertyChanged += (s, e) => DispatcherQueue.TryEnqueue(UpdateTrimDuration);
  }

  public void UpdateTime(TimeSpan position)
  {
    timeDisplay.Text = $"{TimeFormatter.PreciseFormat(position)} / {TimeFormatter.Format(_duration)}";
  }

  private void UpdateTrimDuration()
  {
    if (_trimState == null) return;
    trimDurationText.Text = $"Trim: {TimeFormatter.Format(_trimState.Duration)}";
  }

  private void PlayPause_Click(object sender, RoutedEventArgs e)
  {
    if (_player == null) return;

    if (_isPlaying)
    {
      _player.Pause();
      _isPlaying = false;
      playPauseIcon.Glyph = "\uE768"; // Play
    }
    else
    {
      // If at or past trim end, restart from trim start
      if (_trimState != null && _player.Position >= _trimState.EndTime)
        _player.Position = _trimState.StartTime;
      _player.Play();
      _isPlaying = true;
      playPauseIcon.Glyph = "\uE769"; // Pause
    }
  }

  private void SetIn_Click(object sender, RoutedEventArgs e)
  {
    if (_player == null || _trimState == null) return;
    var pos = _player.Position;
    if (pos < _trimState.EndTime)
      _trimState.StartTime = pos;
  }

  private void SetOut_Click(object sender, RoutedEventArgs e)
  {
    if (_player == null || _trimState == null) return;
    var pos = _player.Position;
    if (pos > _trimState.StartTime)
      _trimState.EndTime = pos;
  }
}
