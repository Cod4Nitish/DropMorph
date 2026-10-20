using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DropMorph.App.ViewModels;

namespace DropMorph.App.Views;

public partial class CropOverlayWindow : Window
{
    public CropOverlayWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void PositionOnScreen(PixelPoint? near = null)
    {
        var screen = (near.HasValue ? Screens.ScreenFromPoint(near.Value) : null)
            ?? Screens.ScreenFromPoint(Screens.Primary?.Bounds.Center ?? default)
            ?? Screens.Primary;

        if (screen is not null)
        {
            var scaling = screen.Scaling;
            var area = screen.WorkingArea;
            var winW = (int)(Width * scaling);
            var winH = (int)(Height * scaling);
            var x = area.X + Math.Max(0, (area.Width - winW) / 2);
            var y = area.Y + Math.Max(0, (area.Height - winH) / 2);
            Position = new PixelPoint(x, y);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is CropOverlayViewModel vm)
        {
            vm.CloseRequested += Close;
            vm.ResetRequested += () => Canvas.ResetCrop();
            vm.AspectRatioChanged += ratio => Canvas.SetAspectRatio(ratio);
            Canvas.CropRectChanged += rect => vm.UpdateDimensions(rect);
            Closed += (_, _) => vm.Dispose();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Key == Key.R && e.KeyModifiers == KeyModifiers.None)
        {
            if (DataContext is CropOverlayViewModel vm)
            {
                vm.Reset();
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pt = e.GetPosition(this);
        if (pt.Y < 50 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }
}
