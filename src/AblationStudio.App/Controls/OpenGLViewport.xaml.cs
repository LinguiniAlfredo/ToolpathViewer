using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using AblationStudio.App.ViewModels;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes;
using AblationStudio.Rendering;
using AblationStudio.Rendering.Camera;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;

namespace AblationStudio.App.Controls;

public partial class OpenGLViewport : UserControl
{
    private readonly SceneRenderer _renderer = new();
    private Point _lastMousePosition;
    private bool _isOrbiting;
    private bool _isPanning;
    private bool _isDrawing;
    private bool _isDraggingShape;
    private Vector3 _drawStartPoint;
    private Vector3 _lastShapeHitPoint;
    private ToolpathShape? _previewShape;

    private Stopwatch? _presetAnimationStopwatch;
    private float _animStartYaw;
    private float _animStartPitch;
    private float _animTargetYaw;
    private float _animTargetPitch;
    private const double PresetAnimationDurationMs = 350.0;

    public SceneRenderer Renderer => _renderer;
    public ShapeDocument? ShapeDocument { get; set; }
    public ShapeToolType CurrentTool { get; set; } = ShapeToolType.Select;
    public event Action<ShapeToolType>? ToolSwitched;

    private int _initialRenderPumps;

    public OpenGLViewport()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (s, e) => RequestRedraw();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var settings = new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3,
            Profile = OpenTK.Windowing.Common.ContextProfile.Core,
            ContextFlags = OpenTK.Windowing.Common.ContextFlags.Default,
            RenderContinuously = true
        };

        GlSurface.Start(settings);

        // Actively pump redraws until the viewport layout finishes and initial frames render
        _initialRenderPumps = 0;
        System.Windows.Media.CompositionTarget.Rendering += OnInitialCompositionRendering;

        Dispatcher.InvokeAsync(RequestRedraw, System.Windows.Threading.DispatcherPriority.Loaded);
        Dispatcher.InvokeAsync(RequestRedraw, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void OnInitialCompositionRendering(object? sender, EventArgs e)
    {
        RequestRedraw();
        _initialRenderPumps++;
        if (_initialRenderPumps >= 5 && GlSurface.FrameBufferWidth > 0 && GlSurface.FrameBufferHeight > 0)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnInitialCompositionRendering;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPresetAnimation();
        System.Windows.Media.CompositionTarget.Rendering -= OnInitialCompositionRendering;
        _renderer.Dispose();
    }

    public void LoadToolpath(Toolpath toolpath, bool autoFit = false)
    {
        CancelPresetAnimation();
        _renderer.LoadToolpath(toolpath, autoFit);
        GlSurface.InvalidateVisual();
    }

    public void FitView()
    {
        CancelPresetAnimation();
        _renderer.FitView();
        GlSurface.InvalidateVisual();
    }

    public void SetPreset(ViewPreset preset, bool animate = true)
    {
        var (targetYaw, targetPitch) = Camera3D.GetPresetAngles(preset);

        if (!animate)
        {
            CancelPresetAnimation();
            _renderer.Camera.Yaw = targetYaw;
            _renderer.Camera.Pitch = targetPitch;
            GlSurface.InvalidateVisual();
            return;
        }

        CancelPresetAnimation();

        float startYaw = (_renderer.Camera.Yaw % 360f + 360f) % 360f;
        float startPitch = _renderer.Camera.Pitch;

        float diffYaw = Camera3D.ShortestAngleDistance(startYaw, targetYaw);
        float diffPitch = targetPitch - startPitch;

        if (MathF.Abs(diffYaw) < 1e-3f && MathF.Abs(diffPitch) < 1e-3f)
        {
            _renderer.Camera.Yaw = targetYaw;
            _renderer.Camera.Pitch = targetPitch;
            GlSurface.InvalidateVisual();
            return;
        }

        _animStartYaw = startYaw;
        _animStartPitch = startPitch;
        _animTargetYaw = targetYaw;
        _animTargetPitch = targetPitch;
        _presetAnimationStopwatch = Stopwatch.StartNew();

        System.Windows.Media.CompositionTarget.Rendering += OnPresetAnimationTick;
    }

    private void CancelPresetAnimation()
    {
        if (_presetAnimationStopwatch is not null)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnPresetAnimationTick;
            _presetAnimationStopwatch.Stop();
            _presetAnimationStopwatch = null;
        }
    }

    private void OnPresetAnimationTick(object? sender, EventArgs e)
    {
        if (_presetAnimationStopwatch is null)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnPresetAnimationTick;
            return;
        }

        double elapsedMs = _presetAnimationStopwatch.Elapsed.TotalMilliseconds;
        float progress = (float)(elapsedMs / PresetAnimationDurationMs);

        if (progress >= 1.0f)
        {
            _renderer.Camera.Yaw = _animTargetYaw;
            _renderer.Camera.Pitch = _animTargetPitch;
            CancelPresetAnimation();
        }
        else
        {
            float t = Camera3D.EaseInOutCubic(progress);
            _renderer.Camera.Yaw = Camera3D.InterpolateAngle(_animStartYaw, _animTargetYaw, t);
            _renderer.Camera.Pitch = Math.Clamp(_animStartPitch + (_animTargetPitch - _animStartPitch) * t, Camera3D.MinPitch, Camera3D.MaxPitch);
        }

        GlSurface.InvalidateVisual();
    }

    public void SetTheme(bool isDark)
    {
        if (isDark)
        {
            _renderer.ClearColor = new Vector4(0.08f, 0.085f, 0.095f, 1.0f);
        }
        else
        {
            _renderer.ClearColor = new Vector4(0.94f, 0.95f, 0.96f, 1.0f);
        }

        GlSurface.InvalidateVisual();
    }

    public void RequestRedraw()
    {
        GlSurface.InvalidateVisual();
    }

    public void SetSimulationIndicator(ToolpathPoint? point, SegmentType segmentType, float modelExtent)
    {
        if (point is null)
        {
            _renderer.ToolIndicator.IsVisible = false;
        }
        else
        {
            _renderer.ToolIndicator.IsVisible = true;
            float scale = Math.Clamp(modelExtent * 0.04f, 0.4f, 8.0f);
            var v3 = new Vector3(point.Value.X, point.Value.Y, point.Value.Z);
            _renderer.ToolIndicator.UpdatePosition(v3, segmentType, scale);
        }

        GlSurface.InvalidateVisual();
    }

    private void GlSurface_OnRender(TimeSpan delta)
    {
        int width = (int)GlSurface.FrameBufferWidth;
        int height = (int)GlSurface.FrameBufferHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _renderer.Initialize();
        _renderer.ShapeOverlay.SetSelectedShape(ShapeDocument?.SelectedShape);
        _renderer.Render(width, height);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        CancelPresetAnimation();
        RootGrid.Focus();
        _lastMousePosition = e.GetPosition(RootGrid);

        int w = (int)ActualWidth > 0 ? (int)ActualWidth : (int)GlSurface.FrameBufferWidth;
        int h = (int)ActualHeight > 0 ? (int)ActualHeight : (int)GlSurface.FrameBufferHeight;

        // Pan: Right Click, Middle Click, or Shift + Left Click
        if (e.RightButton == MouseButtonState.Pressed ||
            e.MiddleButton == MouseButtonState.Pressed ||
            (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Shift))
        {
            _isPanning = true;
            RootGrid.CaptureMouse();
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var (rayOrigin, rayDir) = _renderer.Camera.ScreenPointToRay(
                (float)_lastMousePosition.X, (float)_lastMousePosition.Y, w, h);
            float planeZ = ShapeDocument?.SelectedShape?.PositionZ ?? 0f;
            bool hitPlane = _renderer.Camera.IntersectRayPlaneZ(rayOrigin, rayDir, planeZ, out Vector3 hitPoint);

            if (CurrentTool == ShapeToolType.Select)
            {
                if (hitPlane && ShapeDocument is not null)
                {
                    float fovRad = MathHelper.DegreesToRadians(_renderer.Camera.FieldOfViewDegrees);
                    float worldHeight = 2.0f * _renderer.Camera.Distance * MathF.Tan(fovRad * 0.5f);
                    float worldPerPixel = h > 0 ? (worldHeight / h) : 0.05f;
                    float hitTolerance = MathF.Max(0.8f, 10.0f * worldPerPixel);

                    ToolpathShape? hitShape = ShapeDocument.HitTest(hitPoint.X, hitPoint.Y, hitTolerance);
                    if (hitShape is not null)
                    {
                        ShapeDocument.SelectedShape = hitShape;
                        _isDraggingShape = true;
                        if (_renderer.Camera.IntersectRayPlaneZ(rayOrigin, rayDir, hitShape.PositionZ, out Vector3 shapeHit))
                        {
                            _lastShapeHitPoint = shapeHit;
                        }
                        else
                        {
                            _lastShapeHitPoint = hitPoint;
                        }
                        RootGrid.CaptureMouse();
                        GlSurface.InvalidateVisual();
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        // Deselect if clicking on empty space
                        ShapeDocument.SelectedShape = null;
                        GlSurface.InvalidateVisual();
                    }
                }

                _isOrbiting = true;
                RootGrid.CaptureMouse();
                e.Handled = true;
            }
            else if (hitPlane)
            {
                _isDrawing = true;
                _drawStartPoint = hitPoint;

                _previewShape = CurrentTool switch
                {
                    ShapeToolType.Line => new LineShape(hitPoint.X, hitPoint.Y, planeZ, hitPoint.X, hitPoint.Y, planeZ),
                    ShapeToolType.Circle => new CircleShape(hitPoint.X, hitPoint.Y, planeZ, 0.05f),
                    ShapeToolType.Rectangle => new RectangleShape(hitPoint.X, hitPoint.Y, planeZ, 0.1f, 0.1f),
                    ShapeToolType.Polygon => new PolygonShape(hitPoint.X, hitPoint.Y, planeZ, 0.05f, 5),
                    _ => null
                };

                _renderer.ShapeOverlay.SetPreviewShape(_previewShape);
                RootGrid.CaptureMouse();
                GlSurface.InvalidateVisual();
                e.Handled = true;
            }
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        Point currentPos = e.GetPosition(RootGrid);
        Vector delta = currentPos - _lastMousePosition;

        int w = (int)ActualWidth > 0 ? (int)ActualWidth : (int)GlSurface.FrameBufferWidth;
        int h = (int)ActualHeight > 0 ? (int)ActualHeight : (int)GlSurface.FrameBufferHeight;

        if (_isDrawing && _previewShape is not null)
        {
            var (rayOrigin, rayDir) = _renderer.Camera.ScreenPointToRay(
                (float)currentPos.X, (float)currentPos.Y, w, h);
            float planeZ = _drawStartPoint.Z;

            if (_renderer.Camera.IntersectRayPlaneZ(rayOrigin, rayDir, planeZ, out Vector3 hitPoint))
            {
                switch (_previewShape)
                {
                    case LineShape line:
                        line.EndX = hitPoint.X;
                        line.EndY = hitPoint.Y;
                        line.EndZ = planeZ;
                        break;

                    case CircleShape circle:
                        float r = MathF.Sqrt(MathF.Pow(hitPoint.X - _drawStartPoint.X, 2) + MathF.Pow(hitPoint.Y - _drawStartPoint.Y, 2));
                        circle.Radius = MathF.Max(0.05f, r);
                        break;

                    case RectangleShape rect:
                        float cx = (_drawStartPoint.X + hitPoint.X) * 0.5f;
                        float cy = (_drawStartPoint.Y + hitPoint.Y) * 0.5f;
                        float rw = MathF.Abs(hitPoint.X - _drawStartPoint.X);
                        float rh = MathF.Abs(hitPoint.Y - _drawStartPoint.Y);
                        rect.PositionX = cx;
                        rect.PositionY = cy;
                        rect.Width = MathF.Max(0.1f, rw);
                        rect.Height = MathF.Max(0.1f, rh);
                        break;

                    case PolygonShape poly:
                        float pr = MathF.Sqrt(MathF.Pow(hitPoint.X - _drawStartPoint.X, 2) + MathF.Pow(hitPoint.Y - _drawStartPoint.Y, 2));
                        poly.Radius = MathF.Max(0.05f, pr);
                        float rot = MathF.Atan2(hitPoint.Y - _drawStartPoint.Y, hitPoint.X - _drawStartPoint.X) * (180f / MathF.PI);
                        poly.RotationDegrees = rot;
                        break;
                }

                _renderer.ShapeOverlay.SetPreviewShape(_previewShape);
                GlSurface.InvalidateVisual();
            }

            e.Handled = true;
            return;
        }

        if (_isDraggingShape && ShapeDocument?.SelectedShape is not null)
        {
            var (rayOrigin, rayDir) = _renderer.Camera.ScreenPointToRay(
                (float)currentPos.X, (float)currentPos.Y, w, h);
            float planeZ = ShapeDocument.SelectedShape.PositionZ;

            if (_renderer.Camera.IntersectRayPlaneZ(rayOrigin, rayDir, planeZ, out Vector3 hitPoint))
            {
                float dx = hitPoint.X - _lastShapeHitPoint.X;
                float dy = hitPoint.Y - _lastShapeHitPoint.Y;

                if (MathF.Abs(dx) > 1e-4f || MathF.Abs(dy) > 1e-4f)
                {
                    ShapeDocument.SelectedShape.Translate(dx, dy, 0f);
                    _lastShapeHitPoint = hitPoint;
                    GlSurface.InvalidateVisual();
                }
            }

            _lastMousePosition = currentPos;
            e.Handled = true;
            return;
        }

        if (_isOrbiting)
        {
            float deltaYaw = -(float)delta.X * 0.4f;
            float deltaPitch = (float)delta.Y * 0.4f;
            _renderer.Camera.Orbit(deltaYaw, deltaPitch);
            _lastMousePosition = currentPos;
            GlSurface.InvalidateVisual();
            e.Handled = true;
        }
        else if (_isPanning)
        {
            _renderer.Camera.Pan((float)delta.X, (float)delta.Y, w, h);
            _lastMousePosition = currentPos;
            GlSurface.InvalidateVisual();
            e.Handled = true;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Released)
        {
            if (_isDrawing && _previewShape is not null)
            {
                _renderer.ShapeOverlay.SetPreviewShape(null);
                _isDrawing = false;

                bool isValid = _previewShape switch
                {
                    LineShape line => line.Length > 0.05f,
                    CircleShape circle => circle.Radius > 0.05f,
                    RectangleShape rect => rect.Width > 0.05f && rect.Height > 0.05f,
                    PolygonShape poly => poly.Radius > 0.05f,
                    _ => false
                };

                if (isValid && ShapeDocument is not null)
                {
                    ShapeDocument.AddShape(_previewShape);
                    CurrentTool = ShapeToolType.Select;
                    ToolSwitched?.Invoke(ShapeToolType.Select);
                }

                _previewShape = null;
            }

            _isDraggingShape = false;
            _isOrbiting = false;
        }

        if (e.RightButton == MouseButtonState.Released || e.MiddleButton == MouseButtonState.Released)
        {
            _isPanning = false;
        }

        if (!_isOrbiting && !_isPanning && !_isDrawing && !_isDraggingShape)
        {
            RootGrid.ReleaseMouseCapture();
        }

        GlSurface.InvalidateVisual();
        e.Handled = true;
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        CancelPresetAnimation();
        _renderer.Camera.Zoom((float)e.Delta / 120.0f);
        GlSurface.InvalidateVisual();
        e.Handled = true;
    }
}
