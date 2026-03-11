using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace VidPare.App;

public sealed partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
    AppWindow.Resize(new SizeInt32(960, 640));
    Title = "VidPare";
  }
}
