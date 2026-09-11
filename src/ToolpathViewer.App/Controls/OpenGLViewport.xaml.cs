using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenTK.Mathematics;
using OpenTK.Wpf;
using ToolpathViewer.Core.Models;
using ToolpathViewer.Rendering;
using ToolpathViewer.Rendering.Camera;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;

namespace ToolpathViewer.App.Controls;

public partial class OpenGLViewport : UserControl
{
    private readonly SceneRenderer _renderer = new();
    private Point _lastMousePosition;
    private bool _isOrbiting;
    private bool _isPanning;

    public SceneRenderer Renderer => _renderer;

    public OpenGLViewport()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderer.Dispose();
    }

    public void LoadToolpath(Toolpath toolpath)
    {
        _renderer.LoadToolpath(toolpath);
        GlSurface.InvalidateVisual();
    }

    public void FitView()
    {
        _renderer.FitView();
        GlSurface.InvalidateVisual();
    }

    public void SetPreset(ViewPreset preset)
    {
        _renderer.SetPreset(preset);
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

    private void GlSurface_OnRender(TimeSpan delta)
    {
        int width = (int)GlSurface.FrameBufferWidth;
        int height = (int)GlSurface.FrameBufferHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _renderer.Initialize();
        _renderer.Render(width, height);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        RootGrid.Focus();
        _lastMousePosition = e.GetPosition(RootGrid);

        if (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.None)
        {
            _isOrbiting = true;
            RootGrid.CaptureMouse();
            e.Handled = true;
        }
        else if (e.RightButton == MouseButtonState.Pressed ||
                 e.MiddleButton == MouseButtonState.Pressed ||
                 (e.LeftButton == MouseButtonState.Pressed && Keyboard.Modifiers == ModifierKeys.Shift))
        {
            _isPanning = true;
            RootGrid.CaptureMouse();
            e.Handled = true;
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        Point currentPos = e.GetPosition(RootGrid);
        Vector delta = currentPos - _lastMousePosition;

        if (_isOrbiting)
        {
            float deltaYaw = (float)delta.X * 0.4f;
            float deltaPitch = -(float)delta.Y * 0.4f;
            _renderer.Camera.Orbit(deltaYaw, deltaPitch);
            _lastMousePosition = currentPos;
            GlSurface.InvalidateVisual();
            e.Handled = true;
        }
        else if (_isPanning)
        {
            int w = (int)ActualWidth > 0 ? (int)ActualWidth : (int)GlSurface.FrameBufferWidth;
            int h = (int)ActualHeight > 0 ? (int)ActualHeight : (int)GlSurface.FrameBufferHeight;
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
            _isOrbiting = false;
        }

        if (e.RightButton == MouseButtonState.Released || e.MiddleButton == MouseButtonState.Released)
        {
            _isPanning = false;
        }

        if (!_isOrbiting && !_isPanning)
        {
            RootGrid.ReleaseMouseCapture();
        }

        e.Handled = true;
    }

    private void OnViewportMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _renderer.Camera.Zoom((float)e.Delta / 120.0f);
        GlSurface.InvalidateVisual();
        e.Handled = true;
    }
}
