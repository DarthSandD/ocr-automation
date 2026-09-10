using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OcrAutomation.Models;

namespace OcrAutomation.Utilities;

public partial class RegionSelectorWindow : Window
{
    public CaptureRegion? SelectedRegion { get; private set; }
    private Point _start;
    private bool _dragging = false;
    private readonly Canvas _overlay;
    private readonly Rectangle _rect;

    public RegionSelectorWindow()
    {
        // Build code-behind UI (no XAML file to keep things simple and avoid file ordering)
        _overlay = new Canvas { Background = new SolidColorBrush(Color.FromArgb(20, 0, 0, 255)) };
        _rect = new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 255, 0, 0))
        };
        _overlay.Children.Add(_rect);

        Content = _overlay;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        // Cover entire virtual screen (handles multi-monitor)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Cursor = Cursors.Cross;

        MouseDown += OnMouseDownHandler;
        MouseMove += OnMouseMoveHandler;
        MouseUp += OnMouseUpHandler;
        KeyDown += OnKeyDownHandler;
    }

    private void OnMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _start = e.GetPosition(this);
            _dragging = true;
            Canvas.SetLeft(_rect, _start.X);
            Canvas.SetTop(_rect, _start.Y);
            _rect.Width = 0;
            _rect.Height = 0;
            _rect.Visibility = Visibility.Visible;
        }
    }

    private void OnMouseMoveHandler(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            var p = e.GetPosition(this);
            var x = Math.Min(p.X, _start.X);
            var y = Math.Min(p.Y, _start.Y);
            var w = Math.Abs(p.X - _start.X);
            var h = Math.Abs(p.Y - _start.Y);

            Canvas.SetLeft(_rect, x);
            Canvas.SetTop(_rect, y);
            _rect.Width = w;
            _rect.Height = h;
        }
    }

    private void OnMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            var p = e.GetPosition(this);
            int x = (int)Math.Min(p.X, _start.X);
            int y = (int)Math.Min(p.Y, _start.Y);
            int w = (int)Math.Abs(p.X - _start.X);
            int h = (int)Math.Abs(p.Y - _start.Y);

            if (w >= 5 && h >= 5)
            {
                // Convert from window-relative to screen-relative
                SelectedRegion = new CaptureRegion
                {
                    X = x + (int)Left,
                    Y = y + (int)Top,
                    Width = w,
                    Height = h
                };
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }
    }

    private void OnKeyDownHandler(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
