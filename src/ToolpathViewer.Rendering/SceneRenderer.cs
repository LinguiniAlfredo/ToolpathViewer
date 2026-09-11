using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ToolpathViewer.Core.Models;
using ToolpathViewer.Rendering.Camera;
using ToolpathViewer.Rendering.Renderers;
using ToolpathViewer.Rendering.Shaders;

namespace ToolpathViewer.Rendering;

public sealed class SceneRenderer : IDisposable
{
    private ShaderProgram? _shader;
    private bool _isInitialized;
    private bool _disposed;

    public Camera3D Camera { get; } = new();
    public ToolpathRenderer ToolpathRenderer { get; } = new();
    public GridRenderer GridRenderer { get; } = new();
    public CoordinateAxesRenderer AxesRenderer { get; } = new();
    public BoundingBoxRenderer BoundingBoxRenderer { get; } = new();

    public Vector4 ClearColor { get; set; } = new(0.08f, 0.085f, 0.095f, 1.0f); // Windows 11 Dark Canvas

    public Toolpath CurrentToolpath { get; private set; } = Toolpath.Empty;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _shader = new ShaderProgram(CommonShaders.VertexShaderSource, CommonShaders.FragmentShaderSource);

        ToolpathRenderer.Initialize();
        GridRenderer.Initialize(size: 20f, step: 1f, majorEvery: 5);
        AxesRenderer.Initialize();
        BoundingBoxRenderer.Initialize();

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        _isInitialized = true;
    }

    public void LoadToolpath(Toolpath toolpath)
    {
        CurrentToolpath = toolpath;

        if (_isInitialized)
        {
            ToolpathRenderer.LoadToolpath(toolpath);
            BoundingBoxRenderer.UpdateBox(toolpath.BoundingBox);

            // Dynamically adjust grid and axis scale based on toolpath extent
            float extent = toolpath.BoundingBox.IsEmpty ? 10f : toolpath.BoundingBox.MaxExtent;
            float gridRadius = MathF.Max(10f, MathF.Ceiling(extent * 1.5f));
            float step = gridRadius > 50f ? 10f : (gridRadius > 10f ? 1f : 0.5f);
            GridRenderer.Initialize(size: gridRadius, step: step, majorEvery: 5);

            AxesRenderer.AxisLength = MathF.Max(2f, extent * 0.3f);
            AxesRenderer.UpdateGeometry();

            // Auto-frame view
            Camera.FitToBounds(toolpath.BoundingBox);
        }
    }

    public void Render(int width, int height)
    {
        if (!_isInitialized || _shader is null || width <= 0 || height <= 0)
        {
            return;
        }

        GL.Viewport(0, 0, width, height);
        GL.ClearColor(ClearColor.X, ClearColor.Y, ClearColor.Z, ClearColor.W);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Camera.AspectRatio = (float)width / height;
        Matrix4 mvp = Camera.GetViewProjectionMatrix();

        // 1. Grid
        GridRenderer.Render(_shader, mvp);

        // 2. Coordinate Axes
        AxesRenderer.Render(_shader, mvp);

        // 3. Bounding Box Wireframe
        BoundingBoxRenderer.Render(_shader, mvp);

        // 4. Toolpath Segments
        ToolpathRenderer.Render(_shader, mvp);
    }

    public void FitView()
    {
        Camera.FitToBounds(CurrentToolpath.BoundingBox);
    }

    public void SetPreset(ViewPreset preset)
    {
        Camera.SetPreset(preset);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _shader?.Dispose();
            ToolpathRenderer.Dispose();
            GridRenderer.Dispose();
            AxesRenderer.Dispose();
            BoundingBoxRenderer.Dispose();
            _disposed = true;
        }
    }
}
