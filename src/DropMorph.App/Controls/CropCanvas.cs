// Interactive image crop control: handles, rule-of-thirds grid and aspect ratio constraints.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace DropMorph.App.Controls;

public enum CropHandle
{
    None,
    CreateNew,
    Body,
    TopLeft,
    Top,
    TopRight,
    Right,
    BottomRight,
    Bottom,
    BottomLeft,
    Left,
}

public sealed class CropCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> SourceImageProperty =
        AvaloniaProperty.Register<CropCanvas, Bitmap?>(nameof(SourceImage));

    public static readonly StyledProperty<Rect> NormalizedCropRectProperty =
        AvaloniaProperty.Register<CropCanvas, Rect>(nameof(NormalizedCropRect), new Rect(0.1, 0.1, 0.8, 0.8));

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<CropCanvas, double>(nameof(AspectRatio), 0);

    public Bitmap? SourceImage
    {
        get => GetValue(SourceImageProperty);
        set => SetValue(SourceImageProperty, value);
    }

    public Rect NormalizedCropRect
    {
        get => GetValue(NormalizedCropRectProperty);
        set => SetValue(NormalizedCropRectProperty, value);
    }

    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public event Action<Rect>? CropRectChanged;

    private CropHandle _activeHandle = CropHandle.None;
    private Point _dragStartPoint;
    private Rect _dragStartCrop;
    private const double MinSizePx = 24.0;

    private static readonly Cursor ArrowCursor = new(StandardCursorType.Arrow);
    private static readonly Cursor SizeAllCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor SizeNorthSouthCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor SizeWestEastCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor TopLeftCursor = new(StandardCursorType.TopLeftCorner);
    private static readonly Cursor TopRightCursor = new(StandardCursorType.TopRightCorner);
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    private static readonly IBrush DimMaskBrush = new SolidColorBrush(Color.FromArgb(175, 0, 0, 0));
    private static readonly IPen CropBorderPen = new Pen(Brushes.White, 1.5);
    private static readonly IPen GridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), 1);
    private static readonly IPen HandlePen = new Pen(Brushes.White, 3.5);
    private static readonly IPen HandleShadowPen = new Pen(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), 5.0);

    private static double SafeClamp(double val, double min, double max) =>
        min > max ? Math.Clamp(val, max, min) : Math.Clamp(val, min, max);

    static CropCanvas()
    {
        AffectsRender<CropCanvas>(SourceImageProperty, NormalizedCropRectProperty, AspectRatioProperty);
    }

    public void ResetCrop()
    {
        NormalizedCropRect = new Rect(0, 0, 1, 1);
        CropRectChanged?.Invoke(NormalizedCropRect);
        InvalidateVisual();
    }

    public void SetAspectRatio(double ratio)
    {
        AspectRatio = ratio;
        if (ratio <= 0)
        {
            InvalidateVisual();
            return;
        }

        var imgBounds = CalculateImageBounds();
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0)
            return;

        var current = GetScreenCropRect(imgBounds);

        double w, h;
        if (current.Width / Math.Max(1.0, current.Height) > ratio)
        {
            // Current crop is wider than desired ratio: shrink width to match
            h = current.Height;
            w = h * ratio;
        }
        else
        {
            // Current crop is taller than desired ratio: shrink height to match
            w = current.Width;
            h = w / ratio;
        }

        if (w > imgBounds.Width)
        {
            w = imgBounds.Width;
            h = w / ratio;
        }
        if (h > imgBounds.Height)
        {
            h = imgBounds.Height;
            w = h * ratio;
        }

        w = Math.Max(MinSizePx, w);
        h = Math.Max(MinSizePx, h);

        var x = SafeClamp(current.Center.X - w / 2.0, imgBounds.Left, Math.Max(imgBounds.Left, imgBounds.Right - w));
        var y = SafeClamp(current.Center.Y - h / 2.0, imgBounds.Top, Math.Max(imgBounds.Top, imgBounds.Bottom - h));

        SetCropFromScreen(new Rect(x, y, w, h), imgBounds);
    }

    public Rect CalculateImageBounds()
    {
        if (SourceImage is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return default;

        var imgSize = SourceImage.Size;
        var scale = Math.Min(Bounds.Width / imgSize.Width, Bounds.Height / imgSize.Height);
        var w = imgSize.Width * scale;
        var h = imgSize.Height * scale;
        var x = (Bounds.Width - w) / 2.0;
        var y = (Bounds.Height - h) / 2.0;

        return new Rect(x, y, w, h);
    }

    public Rect GetScreenCropRect(Rect imgBounds)
    {
        var norm = NormalizedCropRect;
        return new Rect(
            imgBounds.X + norm.X * imgBounds.Width,
            imgBounds.Y + norm.Y * imgBounds.Height,
            Math.Max(MinSizePx, norm.Width * imgBounds.Width),
            Math.Max(MinSizePx, norm.Height * imgBounds.Height)
        );
    }

    private void SetCropFromScreen(Rect screenCrop, Rect imgBounds)
    {
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0)
            return;

        var x = SafeClamp(screenCrop.X, imgBounds.Left, Math.Max(imgBounds.Left, imgBounds.Right - MinSizePx));
        var y = SafeClamp(screenCrop.Y, imgBounds.Top, Math.Max(imgBounds.Top, imgBounds.Bottom - MinSizePx));
        var maxW = imgBounds.Right - x;
        var maxH = imgBounds.Bottom - y;
        var w = SafeClamp(screenCrop.Width, MinSizePx, Math.Max(MinSizePx, maxW));
        var h = SafeClamp(screenCrop.Height, MinSizePx, Math.Max(MinSizePx, maxH));

        NormalizedCropRect = new Rect(
            (x - imgBounds.Left) / imgBounds.Width,
            (y - imgBounds.Top) / imgBounds.Height,
            w / imgBounds.Width,
            h / imgBounds.Height
        );
        CropRectChanged?.Invoke(NormalizedCropRect);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var imgBounds = CalculateImageBounds();
        if (SourceImage is null || imgBounds.Width <= 0 || imgBounds.Height <= 0)
            return;

        // 1. Render Source Image
        context.DrawImage(SourceImage, new Rect(0, 0, SourceImage.Size.Width, SourceImage.Size.Height), imgBounds);

        var crop = GetScreenCropRect(imgBounds);

        // 2. Draw Dim Mask around crop box
        var topMask = new Rect(imgBounds.X, imgBounds.Y, imgBounds.Width, Math.Max(0, crop.Y - imgBounds.Y));
        var bottomMask = new Rect(imgBounds.X, crop.Bottom, imgBounds.Width, Math.Max(0, imgBounds.Bottom - crop.Bottom));
        var leftMask = new Rect(imgBounds.X, crop.Y, Math.Max(0, crop.X - imgBounds.X), crop.Height);
        var rightMask = new Rect(crop.Right, crop.Y, Math.Max(0, imgBounds.Right - crop.Right), crop.Height);

        context.DrawRectangle(DimMaskBrush, null, topMask);
        context.DrawRectangle(DimMaskBrush, null, bottomMask);
        context.DrawRectangle(DimMaskBrush, null, leftMask);
        context.DrawRectangle(DimMaskBrush, null, rightMask);

        // 3. Draw Rule-of-Thirds Grid
        if (crop.Width > 50 && crop.Height > 50)
        {
            var x1 = crop.X + crop.Width / 3.0;
            var x2 = crop.X + (crop.Width * 2.0) / 3.0;
            var y1 = crop.Y + crop.Height / 3.0;
            var y2 = crop.Y + (crop.Height * 2.0) / 3.0;

            context.DrawLine(GridPen, new Point(x1, crop.Y), new Point(x1, crop.Bottom));
            context.DrawLine(GridPen, new Point(x2, crop.Y), new Point(x2, crop.Bottom));
            context.DrawLine(GridPen, new Point(crop.X, y1), new Point(crop.Right, y1));
            context.DrawLine(GridPen, new Point(crop.X, y2), new Point(crop.Right, y2));
        }

        // 4. Draw Crop Rectangle Border
        context.DrawRectangle(null, CropBorderPen, crop);

        // 5. Corner brackets and edge bars
        DrawCornerBracket(context, crop.TopLeft, 1, 1);
        DrawCornerBracket(context, crop.TopRight, -1, 1);
        DrawCornerBracket(context, crop.BottomLeft, 1, -1);
        DrawCornerBracket(context, crop.BottomRight, -1, -1);

        DrawEdgeBar(context, new Point(crop.Center.X, crop.Top), isHorizontal: true);
        DrawEdgeBar(context, new Point(crop.Center.X, crop.Bottom), isHorizontal: true);
        DrawEdgeBar(context, new Point(crop.Left, crop.Center.Y), isHorizontal: false);
        DrawEdgeBar(context, new Point(crop.Right, crop.Center.Y), isHorizontal: false);
    }

    private void DrawCornerBracket(DrawingContext context, Point corner, int dirX, int dirY)
    {
        const double length = 18;
        var pHoriz = new Point(corner.X + dirX * length, corner.Y);
        var pVert = new Point(corner.X, corner.Y + dirY * length);

        // Shadow behind bracket for clear visibility on bright/white photos
        context.DrawLine(HandleShadowPen, new Point(corner.X - dirX * 0.5, corner.Y), pHoriz);
        context.DrawLine(HandleShadowPen, new Point(corner.X, corner.Y - dirY * 0.5), pVert);

        // Crisp white bracket
        context.DrawLine(HandlePen, corner, pHoriz);
        context.DrawLine(HandlePen, corner, pVert);
    }

    private void DrawEdgeBar(DrawingContext context, Point center, bool isHorizontal)
    {
        const double halfLen = 10;
        Point p1, p2;
        if (isHorizontal)
        {
            p1 = new Point(center.X - halfLen, center.Y);
            p2 = new Point(center.X + halfLen, center.Y);
        }
        else
        {
            p1 = new Point(center.X, center.Y - halfLen);
            p2 = new Point(center.X, center.Y + halfLen);
        }

        context.DrawLine(HandleShadowPen, p1, p2);
        context.DrawLine(HandlePen, p1, p2);
    }

    private CropHandle HitTestHandle(Point pt, Rect crop, Rect imgBounds)
    {
        const double hitTol = 16.0;

        if (Distance(pt, crop.TopLeft) <= hitTol) return CropHandle.TopLeft;
        if (Distance(pt, crop.TopRight) <= hitTol) return CropHandle.TopRight;
        if (Distance(pt, crop.BottomLeft) <= hitTol) return CropHandle.BottomLeft;
        if (Distance(pt, crop.BottomRight) <= hitTol) return CropHandle.BottomRight;

        if (Math.Abs(pt.Y - crop.Top) <= hitTol && pt.X >= crop.Left && pt.X <= crop.Right) return CropHandle.Top;
        if (Math.Abs(pt.Y - crop.Bottom) <= hitTol && pt.X >= crop.Left && pt.X <= crop.Right) return CropHandle.Bottom;
        if (Math.Abs(pt.X - crop.Left) <= hitTol && pt.Y >= crop.Top && pt.Y <= crop.Bottom) return CropHandle.Left;
        if (Math.Abs(pt.X - crop.Right) <= hitTol && pt.Y >= crop.Top && pt.Y <= crop.Bottom) return CropHandle.Right;

        if (crop.Contains(pt)) return CropHandle.Body;
        if (imgBounds.Contains(pt)) return CropHandle.CreateNew;
        return CropHandle.None;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var imgBounds = CalculateImageBounds();
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return;

        var pt = e.GetPosition(this);
        var crop = GetScreenCropRect(imgBounds);

        if (_activeHandle == CropHandle.None)
        {
            var h = HitTestHandle(pt, crop, imgBounds);
            Cursor = h switch
            {
                CropHandle.TopLeft or CropHandle.BottomRight => TopLeftCursor,
                CropHandle.TopRight or CropHandle.BottomLeft => TopRightCursor,
                CropHandle.Top or CropHandle.Bottom => SizeNorthSouthCursor,
                CropHandle.Left or CropHandle.Right => SizeWestEastCursor,
                CropHandle.Body => SizeAllCursor,
                CropHandle.CreateNew => CrossCursor,
                _ => ArrowCursor,
            };
            return;
        }

        // Active manual selection drag
        if (_activeHandle == CropHandle.CreateNew)
        {
            var p1 = new Point(
                SafeClamp(_dragStartPoint.X, imgBounds.Left, imgBounds.Right),
                SafeClamp(_dragStartPoint.Y, imgBounds.Top, imgBounds.Bottom));
            var p2 = new Point(
                SafeClamp(pt.X, imgBounds.Left, imgBounds.Right),
                SafeClamp(pt.Y, imgBounds.Top, imgBounds.Bottom));

            if (AspectRatio <= 0)
            {
                // Free-form box selection
                var l = Math.Min(p1.X, p2.X);
                var t = Math.Min(p1.Y, p2.Y);
                var w = Math.Max(MinSizePx, Math.Abs(p2.X - p1.X));
                var h = Math.Max(MinSizePx, Math.Abs(p2.Y - p1.Y));
                SetCropFromScreen(new Rect(l, t, w, h), imgBounds);
            }
            else
            {
                // Constrained aspect ratio selection
                var R = AspectRatio;
                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                var signX = dx >= 0 ? 1 : -1;
                var signY = dy >= 0 ? 1 : -1;

                var rawW = Math.Abs(dx);
                var rawH = Math.Abs(dy);

                var w = Math.Max(rawW, rawH * R);
                var h = w / R;

                var maxW = signX > 0 ? (imgBounds.Right - p1.X) : (p1.X - imgBounds.Left);
                var maxH = signY > 0 ? (imgBounds.Bottom - p1.Y) : (p1.Y - imgBounds.Top);

                if (w > maxW) { w = maxW; h = w / R; }
                if (h > maxH) { h = maxH; w = h * R; }

                w = Math.Max(MinSizePx, w);
                h = Math.Max(MinSizePx, h);

                var left = signX > 0 ? p1.X : p1.X - w;
                var top = signY > 0 ? p1.Y : p1.Y - h;

                SetCropFromScreen(new Rect(left, top, w, h), imgBounds);
            }
            return;
        }

        // Active panning/moving the selection box over the image
        if (_activeHandle == CropHandle.Body)
        {
            var deltaX = pt.X - _dragStartPoint.X;
            var deltaY = pt.Y - _dragStartPoint.Y;

            var newX = SafeClamp(_dragStartCrop.X + deltaX, imgBounds.Left, Math.Max(imgBounds.Left, imgBounds.Right - _dragStartCrop.Width));
            var newY = SafeClamp(_dragStartCrop.Y + deltaY, imgBounds.Top, Math.Max(imgBounds.Top, imgBounds.Bottom - _dragStartCrop.Height));

            SetCropFromScreen(new Rect(newX, newY, _dragStartCrop.Width, _dragStartCrop.Height), imgBounds);
            return;
        }

        // Active handle resizing
        var dxRes = pt.X - _dragStartPoint.X;
        var dyRes = pt.Y - _dragStartPoint.Y;

        if (AspectRatio <= 0)
        {
            // Free-form handle resizing
            var left = _dragStartCrop.Left;
            var top = _dragStartCrop.Top;
            var right = _dragStartCrop.Right;
            var bottom = _dragStartCrop.Bottom;

            switch (_activeHandle)
            {
                case CropHandle.TopLeft:
                    left = SafeClamp(_dragStartCrop.Left + dxRes, imgBounds.Left, right - MinSizePx);
                    top = SafeClamp(_dragStartCrop.Top + dyRes, imgBounds.Top, bottom - MinSizePx);
                    break;
                case CropHandle.TopRight:
                    right = SafeClamp(_dragStartCrop.Right + dxRes, left + MinSizePx, imgBounds.Right);
                    top = SafeClamp(_dragStartCrop.Top + dyRes, imgBounds.Top, bottom - MinSizePx);
                    break;
                case CropHandle.BottomLeft:
                    left = SafeClamp(_dragStartCrop.Left + dxRes, imgBounds.Left, right - MinSizePx);
                    bottom = SafeClamp(_dragStartCrop.Bottom + dyRes, top + MinSizePx, imgBounds.Bottom);
                    break;
                case CropHandle.BottomRight:
                    right = SafeClamp(_dragStartCrop.Right + dxRes, left + MinSizePx, imgBounds.Right);
                    bottom = SafeClamp(_dragStartCrop.Bottom + dyRes, top + MinSizePx, imgBounds.Bottom);
                    break;
                case CropHandle.Top:
                    top = SafeClamp(_dragStartCrop.Top + dyRes, imgBounds.Top, bottom - MinSizePx);
                    break;
                case CropHandle.Bottom:
                    bottom = SafeClamp(_dragStartCrop.Bottom + dyRes, top + MinSizePx, imgBounds.Bottom);
                    break;
                case CropHandle.Left:
                    left = SafeClamp(_dragStartCrop.Left + dxRes, imgBounds.Left, right - MinSizePx);
                    break;
                case CropHandle.Right:
                    right = SafeClamp(_dragStartCrop.Right + dxRes, left + MinSizePx, imgBounds.Right);
                    break;
            }

            SetCropFromScreen(new Rect(left, top, right - left, bottom - top), imgBounds);
        }
        else
        {
            // Proportional aspect ratio handle resizing
            var R = AspectRatio;

            switch (_activeHandle)
            {
                case CropHandle.BottomRight:
                    {
                        var anchor = _dragStartCrop.TopLeft;
                        var delta = (dxRes + dyRes * R) / 2.0;
                        var maxW = Math.Min(imgBounds.Right - anchor.X, (imgBounds.Bottom - anchor.Y) * R);
                        var w = SafeClamp(_dragStartCrop.Width + delta, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        SetCropFromScreen(new Rect(anchor.X, anchor.Y, w, h), imgBounds);
                        break;
                    }
                case CropHandle.BottomLeft:
                    {
                        var anchorX = _dragStartCrop.Right;
                        var anchorY = _dragStartCrop.Top;
                        var delta = (-dxRes + dyRes * R) / 2.0;
                        var maxW = Math.Min(anchorX - imgBounds.Left, (imgBounds.Bottom - anchorY) * R);
                        var w = SafeClamp(_dragStartCrop.Width + delta, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        SetCropFromScreen(new Rect(anchorX - w, anchorY, w, h), imgBounds);
                        break;
                    }
                case CropHandle.TopRight:
                    {
                        var anchorX = _dragStartCrop.Left;
                        var anchorY = _dragStartCrop.Bottom;
                        var delta = (dxRes - dyRes * R) / 2.0;
                        var maxW = Math.Min(imgBounds.Right - anchorX, (anchorY - imgBounds.Top) * R);
                        var w = SafeClamp(_dragStartCrop.Width + delta, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        SetCropFromScreen(new Rect(anchorX, anchorY - h, w, h), imgBounds);
                        break;
                    }
                case CropHandle.TopLeft:
                    {
                        var anchorX = _dragStartCrop.Right;
                        var anchorY = _dragStartCrop.Bottom;
                        var delta = (-dxRes - dyRes * R) / 2.0;
                        var maxW = Math.Min(anchorX - imgBounds.Left, (anchorY - imgBounds.Top) * R);
                        var w = SafeClamp(_dragStartCrop.Width + delta, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        SetCropFromScreen(new Rect(anchorX - w, anchorY - h, w, h), imgBounds);
                        break;
                    }
                case CropHandle.Right:
                    {
                        var anchorLeft = _dragStartCrop.Left;
                        var maxW = Math.Min(imgBounds.Right - anchorLeft, imgBounds.Height * R);
                        var w = SafeClamp(_dragStartCrop.Width + dxRes, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        var y = SafeClamp(_dragStartCrop.Center.Y - h / 2.0, imgBounds.Top, imgBounds.Bottom - h);
                        SetCropFromScreen(new Rect(anchorLeft, y, w, h), imgBounds);
                        break;
                    }
                case CropHandle.Left:
                    {
                        var anchorRight = _dragStartCrop.Right;
                        var maxW = Math.Min(anchorRight - imgBounds.Left, imgBounds.Height * R);
                        var w = SafeClamp(_dragStartCrop.Width - dxRes, MinSizePx, Math.Max(MinSizePx, maxW));
                        var h = w / R;
                        var y = SafeClamp(_dragStartCrop.Center.Y - h / 2.0, imgBounds.Top, imgBounds.Bottom - h);
                        SetCropFromScreen(new Rect(anchorRight - w, y, w, h), imgBounds);
                        break;
                    }
                case CropHandle.Bottom:
                    {
                        var anchorTop = _dragStartCrop.Top;
                        var maxH = Math.Min(imgBounds.Bottom - anchorTop, imgBounds.Width / R);
                        var h = SafeClamp(_dragStartCrop.Height + dyRes, MinSizePx, Math.Max(MinSizePx, maxH));
                        var w = h * R;
                        var x = SafeClamp(_dragStartCrop.Center.X - w / 2.0, imgBounds.Left, imgBounds.Right - w);
                        SetCropFromScreen(new Rect(x, anchorTop, w, h), imgBounds);
                        break;
                    }
                case CropHandle.Top:
                    {
                        var anchorBottom = _dragStartCrop.Bottom;
                        var maxH = Math.Min(anchorBottom - imgBounds.Top, imgBounds.Width / R);
                        var h = SafeClamp(_dragStartCrop.Height - dyRes, MinSizePx, Math.Max(MinSizePx, maxH));
                        var w = h * R;
                        var x = SafeClamp(_dragStartCrop.Center.X - w / 2.0, imgBounds.Left, imgBounds.Right - w);
                        SetCropFromScreen(new Rect(x, anchorBottom - h, w, h), imgBounds);
                        break;
                    }
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var imgBounds = CalculateImageBounds();
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return;

        var pt = e.GetPosition(this);
        var crop = GetScreenCropRect(imgBounds);
        _activeHandle = HitTestHandle(pt, crop, imgBounds);

        if (_activeHandle != CropHandle.None)
        {
            _dragStartPoint = pt;
            _dragStartCrop = crop;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_activeHandle != CropHandle.None)
        {
            _activeHandle = CropHandle.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var imgBounds = CalculateImageBounds();
        if (imgBounds.Width <= 0 || imgBounds.Height <= 0) return;

        var crop = GetScreenCropRect(imgBounds);
        var delta = e.Delta.Y;
        if (Math.Abs(delta) < 0.001) return;

        var zoomFactor = delta > 0 ? 1.05 : 0.95;
        double newW, newH;

        if (AspectRatio > 0)
        {
            newW = crop.Width * zoomFactor;
            newH = newW / AspectRatio;
            if (newW > imgBounds.Width || newH > imgBounds.Height)
            {
                newW = Math.Min(imgBounds.Width, imgBounds.Height * AspectRatio);
                newH = newW / AspectRatio;
            }
        }
        else
        {
            newW = SafeClamp(crop.Width * zoomFactor, MinSizePx, imgBounds.Width);
            newH = SafeClamp(crop.Height * zoomFactor, MinSizePx, imgBounds.Height);
        }

        newW = Math.Max(1, newW);
        newH = Math.Max(1, newH);

        var newX = SafeClamp(crop.Center.X - newW / 2.0, imgBounds.Left, imgBounds.Right - newW);
        var newY = SafeClamp(crop.Center.Y - newH / 2.0, imgBounds.Top, imgBounds.Bottom - newH);

        SetCropFromScreen(new Rect(newX, newY, newW, newH), imgBounds);
        e.Handled = true;
    }
}
