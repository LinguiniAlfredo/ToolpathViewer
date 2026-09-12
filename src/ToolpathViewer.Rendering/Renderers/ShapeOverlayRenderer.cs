using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ToolpathViewer.Core.Models;
using ToolpathViewer.Core.Shapes;
using ToolpathViewer.Rendering.Shaders;

namespace ToolpathViewer.Rendering.Renderers;

public sealed class ShapeOverlayRenderer : IDisposable
{
    private const int FloatStride = 7;
    private const int StrideBytes = FloatStride * sizeof(float);

    private int _previewVao;
    private int _previewVbo;
    private int _previewVertexCount;

    private int _selectionVao;
    private int _selectionVbo;
    private int _selectionVertexCount;

    private int _handlesVao;
    private int _handlesVbo;
    private int _handlesVertexCount;

    private bool _hasPreview;
    private bool _hasSelection;
    private bool _disposed;

    public static readonly Vector4 PreviewColor = new(1.0f, 0.9f, 0.2f, 1.0f);     // Bright Yellow
    public static readonly Vector4 SelectionColor = new(0.0f, 0.65f, 1.0f, 0.9f);  // Cyan Selection Frame
    public static readonly Vector4 HandleColor = new(1.0f, 1.0f, 1.0f, 0.95f);      // Crisp White Corner Handles

    public void Initialize()
    {
        if (_previewVao == 0)
        {
            _previewVao = GL.GenVertexArray();
            _previewVbo = GL.GenBuffer();
            SetupVao(_previewVao, _previewVbo);

            _selectionVao = GL.GenVertexArray();
            _selectionVbo = GL.GenBuffer();
            SetupVao(_selectionVao, _selectionVbo);

            _handlesVao = GL.GenVertexArray();
            _handlesVbo = GL.GenBuffer();
            SetupVao(_handlesVao, _handlesVbo);
        }
    }

    public void SetPreviewShape(ToolpathShape? shape)
    {
        if (shape is null)
        {
            _hasPreview = false;
            _previewVertexCount = 0;
            return;
        }

        IReadOnlyList<ToolpathPoint> points = shape.GetPathPoints();
        if (points.Count < 2)
        {
            _hasPreview = false;
            _previewVertexCount = 0;
            return;
        }

        var vertices = new List<float>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            AddSegment(vertices, points[i], points[i + 1], PreviewColor);
        }

        if (shape.IsClosed && points.Count > 2)
        {
            AddSegment(vertices, points[^1], points[0], PreviewColor);
        }

        UploadData(_previewVao, _previewVbo, vertices);
        _previewVertexCount = vertices.Count / FloatStride;
        _hasPreview = _previewVertexCount > 0;
    }

    public void SetSelectedShape(ToolpathShape? shape)
    {
        if (shape is null)
        {
            _hasSelection = false;
            _selectionVertexCount = 0;
            _handlesVertexCount = 0;
            return;
        }

        var boxLines = new List<float>();
        var handleTriangles = new List<float>();

        BoundingBox3D bounds = shape.GetBounds();
        float z = shape.PositionZ;

        // Bounding box frame lines
        var p0 = new ToolpathPoint(bounds.MinX, bounds.MinY, z);
        var p1 = new ToolpathPoint(bounds.MaxX, bounds.MinY, z);
        var p2 = new ToolpathPoint(bounds.MaxX, bounds.MaxY, z);
        var p3 = new ToolpathPoint(bounds.MinX, bounds.MaxY, z);

        AddSegment(boxLines, p0, p1, SelectionColor);
        AddSegment(boxLines, p1, p2, SelectionColor);
        AddSegment(boxLines, p2, p3, SelectionColor);
        AddSegment(boxLines, p3, p0, SelectionColor);

        // Center cross marker
        var center = new ToolpathPoint(shape.PositionX, shape.PositionY, z);
        float markerSize = MathF.Max(0.5f, MathF.Min(bounds.SizeX, bounds.SizeY) * 0.1f);
        AddCross(boxLines, center, SelectionColor, markerSize);

        // Corner & Edge Handles
        float handleRadius = MathF.Max(0.2f, MathF.Min(bounds.SizeX, bounds.SizeY) * 0.04f);
        AddHandleQuad(handleTriangles, p0.X, p0.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p1.X, p1.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p2.X, p2.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p3.X, p3.Y, z, handleRadius, HandleColor);

        // Upload
        UploadData(_selectionVao, _selectionVbo, boxLines);
        _selectionVertexCount = boxLines.Count / FloatStride;

        UploadData(_handlesVao, _handlesVbo, handleTriangles);
        _handlesVertexCount = handleTriangles.Count / FloatStride;

        _hasSelection = true;
    }

    public void Render(ShaderProgram shader, Matrix4 mvp)
    {
        if (!_hasPreview && !_hasSelection)
        {
            return;
        }

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        // 1. Draw Selection Box
        if (_hasSelection && _selectionVertexCount > 0)
        {
            GL.LineWidth(2.0f);
            GL.BindVertexArray(_selectionVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _selectionVertexCount);

            // Draw Handles
            if (_handlesVertexCount > 0)
            {
                GL.BindVertexArray(_handlesVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, _handlesVertexCount);
            }
        }

        // 2. Draw Interactive Drawing Preview
        if (_hasPreview && _previewVertexCount > 0)
        {
            GL.LineWidth(2.5f);
            GL.BindVertexArray(_previewVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _previewVertexCount);
        }

        GL.BindVertexArray(0);
    }

    private static void AddSegment(List<float> buffer, ToolpathPoint p0, ToolpathPoint p1, Vector4 color)
    {
        buffer.Add(p0.X); buffer.Add(p0.Y); buffer.Add(p0.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);

        buffer.Add(p1.X); buffer.Add(p1.Y); buffer.Add(p1.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
    }

    private static void AddCross(List<float> buffer, ToolpathPoint pt, Vector4 color, float size)
    {
        float s = size * 0.5f;
        buffer.Add(pt.X - s); buffer.Add(pt.Y); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(pt.X + s); buffer.Add(pt.Y); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);

        buffer.Add(pt.X); buffer.Add(pt.Y - s); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(pt.X); buffer.Add(pt.Y + s); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
    }

    private static void AddHandleQuad(List<float> buffer, float cx, float cy, float cz, float r, Vector4 color)
    {
        // 2 Triangles for a square handle
        float x0 = cx - r, y0 = cy - r;
        float x1 = cx + r, y1 = cy + r;

        // Tri 1
        AddVertex(buffer, x0, y0, cz, color);
        AddVertex(buffer, x1, y0, cz, color);
        AddVertex(buffer, x1, y1, cz, color);

        // Tri 2
        AddVertex(buffer, x0, y0, cz, color);
        AddVertex(buffer, x1, y1, cz, color);
        AddVertex(buffer, x0, y1, cz, color);
    }

    private static void AddVertex(List<float> buffer, float x, float y, float z, Vector4 color)
    {
        buffer.Add(x); buffer.Add(y); buffer.Add(z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
    }

    private static void SetupVao(int vao, int vbo)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, StrideBytes, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, StrideBytes, 3 * sizeof(float));

        GL.BindVertexArray(0);
    }

    private static void UploadData(int vao, int vbo, List<float> data)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        float[] array = data.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * sizeof(float), array, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_previewVbo != 0) GL.DeleteBuffer(_previewVbo);
            if (_previewVao != 0) GL.DeleteVertexArray(_previewVao);
            if (_selectionVbo != 0) GL.DeleteBuffer(_selectionVbo);
            if (_selectionVao != 0) GL.DeleteVertexArray(_selectionVao);
            if (_handlesVbo != 0) GL.DeleteBuffer(_handlesVbo);
            if (_handlesVao != 0) GL.DeleteVertexArray(_handlesVao);
            _disposed = true;
        }
    }
}
