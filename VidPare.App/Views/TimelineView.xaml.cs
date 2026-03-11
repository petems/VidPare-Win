using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using VidPare.Core.Models;

namespace VidPare.App.Views;

public sealed partial class TimelineView : UserControl
{
  private TrimState? _trimState;
  private TimeSpan _duration;
  private List<BitmapImage>? _thumbnails;

  // Visual elements
  private readonly List<Image> _thumbnailImages = new();
  private Rectangle? _dimLeft;
  private Rectangle? _dimRight;
  private Rectangle? _handleLeft;
  private Rectangle? _handleRight;
  private Rectangle? _trimBorderTop;
  private Rectangle? _trimBorderBottom;
  private Line? _playheadLine;

  // Drag state
  private enum DragTarget { None, LeftHandle, RightHandle, Playhead }
  private DragTarget _dragTarget = DragTarget.None;

  private const double HandleWidth = 12;
  private const double HandleHitWidth = 24;

  public TimelineView()
  {
    InitializeComponent();
  }

  public void SetState(TrimState trimState, TimeSpan duration)
  {
    _trimState = trimState;
    _duration = duration;
    _trimState.PropertyChanged += (s, e) => RedrawOverlays();
    RedrawAll();
  }

  public void SetThumbnails(List<BitmapImage> thumbnails)
  {
    _thumbnails = thumbnails;
    RedrawAll();
  }

  public void UpdatePlayhead(TimeSpan position)
  {
    if (_playheadLine == null || _duration == TimeSpan.Zero) return;
    var canvasWidth = timelineCanvas.ActualWidth;
    var x = (position / _duration) * canvasWidth;
    _playheadLine.X1 = x;
    _playheadLine.X2 = x;
  }

  private void Timeline_SizeChanged(object sender, SizeChangedEventArgs e)
  {
    RedrawAll();
  }

  private void RedrawAll()
  {
    timelineCanvas.Children.Clear();
    _thumbnailImages.Clear();

    var width = timelineCanvas.ActualWidth;
    var height = timelineCanvas.ActualHeight;
    if (width <= 0 || height <= 0) return;

    // Draw thumbnails
    if (_thumbnails != null && _thumbnails.Count > 0)
    {
      var thumbWidth = width / _thumbnails.Count;
      for (int i = 0; i < _thumbnails.Count; i++)
      {
        var img = new Image
        {
          Source = _thumbnails[i],
          Width = thumbWidth + 1, // overlap to avoid gaps
          Height = height,
          Stretch = Stretch.UniformToFill
        };
        Canvas.SetLeft(img, i * thumbWidth);
        Canvas.SetTop(img, 0);
        timelineCanvas.Children.Add(img);
        _thumbnailImages.Add(img);
      }
    }

    // Dim overlays (outside trim region)
    _dimLeft = new Rectangle
    {
      Fill = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
      Height = height
    };
    timelineCanvas.Children.Add(_dimLeft);

    _dimRight = new Rectangle
    {
      Fill = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
      Height = height
    };
    timelineCanvas.Children.Add(_dimRight);

    // Trim border lines (top and bottom)
    var accentColor = Color.FromArgb(255, 0, 120, 215);
    _trimBorderTop = new Rectangle
    {
      Fill = new SolidColorBrush(accentColor),
      Height = 2
    };
    timelineCanvas.Children.Add(_trimBorderTop);

    _trimBorderBottom = new Rectangle
    {
      Fill = new SolidColorBrush(accentColor),
      Height = 2
    };
    timelineCanvas.Children.Add(_trimBorderBottom);

    // Trim handles
    _handleLeft = new Rectangle
    {
      Fill = new SolidColorBrush(accentColor),
      Width = HandleWidth,
      Height = height,
      RadiusX = 3,
      RadiusY = 3
    };
    timelineCanvas.Children.Add(_handleLeft);

    _handleRight = new Rectangle
    {
      Fill = new SolidColorBrush(accentColor),
      Width = HandleWidth,
      Height = height,
      RadiusX = 3,
      RadiusY = 3
    };
    timelineCanvas.Children.Add(_handleRight);

    // Playhead
    _playheadLine = new Line
    {
      Stroke = new SolidColorBrush(Colors.White),
      StrokeThickness = 2,
      Y1 = 0,
      Y2 = height
    };
    timelineCanvas.Children.Add(_playheadLine);

    RedrawOverlays();
  }

  private void RedrawOverlays()
  {
    if (_trimState == null || _duration == TimeSpan.Zero) return;
    var width = timelineCanvas.ActualWidth;
    var height = timelineCanvas.ActualHeight;
    if (width <= 0) return;

    var startX = (_trimState.StartTime / _duration) * width;
    var endX = (_trimState.EndTime / _duration) * width;

    // Dim regions
    if (_dimLeft != null)
    {
      Canvas.SetLeft(_dimLeft, 0);
      _dimLeft.Width = Math.Max(0, startX);
    }
    if (_dimRight != null)
    {
      Canvas.SetLeft(_dimRight, endX);
      _dimRight.Width = Math.Max(0, width - endX);
    }

    // Trim border lines
    if (_trimBorderTop != null)
    {
      Canvas.SetLeft(_trimBorderTop, startX);
      Canvas.SetTop(_trimBorderTop, 0);
      _trimBorderTop.Width = Math.Max(0, endX - startX);
    }
    if (_trimBorderBottom != null)
    {
      Canvas.SetLeft(_trimBorderBottom, startX);
      Canvas.SetTop(_trimBorderBottom, height - 2);
      _trimBorderBottom.Width = Math.Max(0, endX - startX);
    }

    // Handles
    if (_handleLeft != null)
    {
      Canvas.SetLeft(_handleLeft, startX - HandleWidth);
      Canvas.SetTop(_handleLeft, 0);
    }
    if (_handleRight != null)
    {
      Canvas.SetLeft(_handleRight, endX);
      Canvas.SetTop(_handleRight, 0);
    }
  }

  private void Timeline_PointerPressed(object sender, PointerRoutedEventArgs e)
  {
    if (_trimState == null || _duration == TimeSpan.Zero) return;
    var pos = e.GetCurrentPoint(timelineCanvas).Position;
    var width = timelineCanvas.ActualWidth;

    var startX = (_trimState.StartTime / _duration) * width;
    var endX = (_trimState.EndTime / _duration) * width;

    // Check handles (with larger hit area)
    if (Math.Abs(pos.X - startX) < HandleHitWidth)
      _dragTarget = DragTarget.LeftHandle;
    else if (Math.Abs(pos.X - endX) < HandleHitWidth)
      _dragTarget = DragTarget.RightHandle;
    else
      _dragTarget = DragTarget.Playhead;

    timelineCanvas.CapturePointer(e.Pointer);
    HandleDrag(pos.X);
    e.Handled = true;
  }

  private void Timeline_PointerMoved(object sender, PointerRoutedEventArgs e)
  {
    if (_dragTarget == DragTarget.None) return;
    var pos = e.GetCurrentPoint(timelineCanvas).Position;
    HandleDrag(pos.X);
    e.Handled = true;
  }

  private void Timeline_PointerReleased(object sender, PointerRoutedEventArgs e)
  {
    _dragTarget = DragTarget.None;
    timelineCanvas.ReleasePointerCapture(e.Pointer);
    e.Handled = true;
  }

  private void HandleDrag(double x)
  {
    if (_trimState == null || _duration == TimeSpan.Zero) return;
    var width = timelineCanvas.ActualWidth;
    var fraction = Math.Clamp(x / width, 0, 1);
    var time = TimeSpan.FromSeconds(fraction * _duration.TotalSeconds);

    switch (_dragTarget)
    {
      case DragTarget.LeftHandle:
        if (time < _trimState.EndTime - TimeSpan.FromMilliseconds(100))
          _trimState.StartTime = time;
        break;
      case DragTarget.RightHandle:
        if (time > _trimState.StartTime + TimeSpan.FromMilliseconds(100))
          _trimState.EndTime = time;
        break;
      case DragTarget.Playhead:
        // Seek the player to this position
        SeekRequested?.Invoke(this, time);
        break;
    }
  }

  public event EventHandler<TimeSpan>? SeekRequested;
}
