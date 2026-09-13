using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes;
using AblationStudio.Rendering.Shaders;

namespace AblationStudio.Rendering.Renderers;

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

    private int _marqueeVao;
    private int _marqueeVbo;
    private int _marqueeVertexCount;

    private int _marqueeFillVao;
    private int _marqueeFillVbo;
    private int _marqueeFillVertexCount;

    private bool _hasPreview;
    private bool _hasSelection;
    private bool _hasMarquee;
    private bool _disposed;

    public static readonly Vector4 PreviewColor = new(1.0f, 0.9f, 0.2f, 1.0f);     // Bright Yellow
    public static readonly Vector4 SelectionColor = new(0.0f, 0.65f, 1.0f, 0.9f);  // Cyan Selection Frame
    public static readonly Vector4 SubtleSelectionColor = new(0.0f, 0.65f, 1.0f, 0.4f); // Subtle Cyan Frame for inner shapes
    public static readonly Vector4 HandleColor = new(1.0f, 1.0f, 1.0f, 0.95f);      // Crisp White Corner Handles
    public static readonly Vector4 RotateHandleColor = new(0.2f, 0.9f, 0.4f, 0.95f); // Vibrant Emerald Green Rotation Handle
    public static readonly Vector4 MarqueeBorderColor = new(0.2f, 0.7f, 1.0f, 0.9f);
    public static readonly Vector4 MarqueeFillColor = new(0.2f, 0.7f, 1.0f, 0.15f);

    public static (ToolpathPoint[] Corners, ToolpathPoint RotHandle, float HandleRadius) GetHandleGeometry(BoundingBox3D bounds, float z)
    {
        var corners = new ToolpathPoint[]
        {
            new(bounds.MinX, bounds.MinY, z), // 0: Bottom-Left
            new(bounds.MaxX, bounds.MinY, z), // 1: Bottom-Right
            new(bounds.MaxX, bounds.MaxY, z), // 2: Top-Right
            new(bounds.MinX, bounds.MaxY, z)  // 3: Top-Left
        };

        float extent = MathF.Max(bounds.SizeX, bounds.SizeY);
        float minExtent = MathF.Min(bounds.SizeX, bounds.SizeY);
        float refSize = minExtent > 0.01f ? minExtent : extent;
        float handleRadius = Math.Clamp(refSize * 0.04f, 0.25f, 2.5f);
        float stemLength = MathF.Max(1.2f, handleRadius * 3.5f);
        float topCenterX = (bounds.MinX + bounds.MaxX) * 0.5f;
        var rotHandle = new ToolpathPoint(topCenterX, bounds.MaxY + stemLength, z);

        return (corners, rotHandle, handleRadius);
    }

    public static (ToolpathPoint[] Corners, ToolpathPoint RotHandle, float HandleRadius) GetHandleGeometry(ToolpathShape shape)
    {
        return GetHandleGeometry(shape.GetBounds(), shape.PositionZ);
    }

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

            _marqueeVao = GL.GenVertexArray();
            _marqueeVbo = GL.GenBuffer();
            SetupVao(_marqueeVao, _marqueeVbo);

            _marqueeFillVao = GL.GenVertexArray();
            _marqueeFillVbo = GL.GenBuffer();
            SetupVao(_marqueeFillVao, _marqueeFillVbo);
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

        if (shape is IContourShape contourShape)
        {
            float px = shape.PositionX;
            float py = shape.PositionY;
            float pz = shape.PositionZ;

            foreach (PathContour contour in contourShape.Contours)
            {
                IReadOnlyList<ToolpathPoint> pts = contour.LocalPoints;
                if (pts.Count < 2)
                {
                    continue;
                }

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p1 = new ToolpathPoint(px + pts[i].X, py + pts[i].Y, pz + pts[i].Z);
                    var p2 = new ToolpathPoint(px + pts[i + 1].X, py + pts[i + 1].Y, pz + pts[i + 1].Z);
                    AddSegment(vertices, p1, p2, PreviewColor);
                }

                if (contour.IsClosed && pts.Count > 2)
                {
                    var pEnd = new ToolpathPoint(px + pts[^1].X, py + pts[^1].Y, pz + pts[^1].Z);
                    var pStart = new ToolpathPoint(px + pts[0].X, py + pts[0].Y, pz + pts[0].Z);
                    AddSegment(vertices, pEnd, pStart, PreviewColor);
                }
            }
        }
        else
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                AddSegment(vertices, points[i], points[i + 1], PreviewColor);
            }

            if (shape.IsClosed && points.Count > 2)
            {
                AddSegment(vertices, points[^1], points[0], PreviewColor);
            }
        }

        UploadData(_previewVao, _previewVbo, vertices);
        _previewVertexCount = vertices.Count / FloatStride;
        _hasPreview = _previewVertexCount > 0;
    }

    public void SetSelectedShape(ToolpathShape? shape)
    {
        SetSelectedShapes(shape is not null ? [shape] : null);
    }

    public void SetSelectedShapes(IReadOnlyCollection<ToolpathShape>? shapes)
    {
        if (shapes is null || shapes.Count == 0)
        {
            _hasSelection = false;
            _selectionVertexCount = 0;
            _handlesVertexCount = 0;
            return;
        }

        var boxLines = new List<float>();
        var handleTriangles = new List<float>();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (ToolpathShape s in shapes)
        {
            BoundingBox3D b = s.GetBounds();
            if (b.IsEmpty)
            {
                continue;
            }

            minX = MathF.Min(minX, b.MinX);
            maxX = MathF.Max(maxX, b.MaxX);
            minY = MathF.Min(minY, b.MinY);
            maxY = MathF.Max(maxY, b.MaxY);
            minZ = MathF.Min(minZ, b.MinZ);
            maxZ = MathF.Max(maxZ, b.MaxZ);

            if (shapes.Count > 1)
            {
                float sz = s.PositionZ;
                var sp0 = new ToolpathPoint(b.MinX, b.MinY, sz);
                var sp1 = new ToolpathPoint(b.MaxX, b.MinY, sz);
                var sp2 = new ToolpathPoint(b.MaxX, b.MaxY, sz);
                var sp3 = new ToolpathPoint(b.MinX, b.MaxY, sz);

                AddSegment(boxLines, sp0, sp1, SubtleSelectionColor);
                AddSegment(boxLines, sp1, sp2, SubtleSelectionColor);
                AddSegment(boxLines, sp2, sp3, SubtleSelectionColor);
                AddSegment(boxLines, sp3, sp0, SubtleSelectionColor);
            }
        }

        if (minX > maxX)
        {
            _hasSelection = false;
            _selectionVertexCount = 0;
            _handlesVertexCount = 0;
            return;
        }

        var combinedBounds = new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);
        float z = shapes.First().PositionZ;

        var (corners, rotHandle, handleRadius) = GetHandleGeometry(combinedBounds, z);

        // Bounding box frame lines
        var p0 = corners[0];
        var p1 = corners[1];
        var p2 = corners[2];
        var p3 = corners[3];

        AddSegment(boxLines, p0, p1, SelectionColor);
        AddSegment(boxLines, p1, p2, SelectionColor);
        AddSegment(boxLines, p2, p3, SelectionColor);
        AddSegment(boxLines, p3, p0, SelectionColor);

        // Stem line connecting top-edge midpoint to rotation handle
        float topCenterX = (combinedBounds.MinX + combinedBounds.MaxX) * 0.5f;
        var topMid = new ToolpathPoint(topCenterX, combinedBounds.MaxY, z);
        AddSegment(boxLines, topMid, rotHandle, SelectionColor);

        // Center cross marker
        float centerX = topCenterX;
        float centerY = (combinedBounds.MinY + combinedBounds.MaxY) * 0.5f;
        var center = new ToolpathPoint(centerX, centerY, z);
        float markerSize = MathF.Max(0.5f, MathF.Min(combinedBounds.SizeX, combinedBounds.SizeY) * 0.1f);
        AddCross(boxLines, center, SelectionColor, markerSize);

        // Corner Handles (White)
        AddHandleQuad(handleTriangles, p0.X, p0.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p1.X, p1.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p2.X, p2.Y, z, handleRadius, HandleColor);
        AddHandleQuad(handleTriangles, p3.X, p3.Y, z, handleRadius, HandleColor);

        // Rotation Handle (Green)
        AddHandleQuad(handleTriangles, rotHandle.X, rotHandle.Y, z, handleRadius, RotateHandleColor);

        // Upload
        UploadData(_selectionVao, _selectionVbo, boxLines);
        _selectionVertexCount = boxLines.Count / FloatStride;

        UploadData(_handlesVao, _handlesVbo, handleTriangles);
        _handlesVertexCount = handleTriangles.Count / FloatStride;

        _hasSelection = true;
    }

    public void SetMarqueeRect(float x0, float y0, float x1, float y1, float z)
    {
        float minX = MathF.Min(x0, x1);
        float maxX = MathF.Max(x0, x1);
        float minY = MathF.Min(y0, y1);
        float maxY = MathF.Max(y0, y1);

        var lines = new List<float>();
        var p0 = new ToolpathPoint(minX, minY, z);
        var p1 = new ToolpathPoint(maxX, minY, z);
        var p2 = new ToolpathPoint(maxX, maxY, z);
        var p3 = new ToolpathPoint(minX, maxY, z);

        AddSegment(lines, p0, p1, MarqueeBorderColor);
        AddSegment(lines, p1, p2, MarqueeBorderColor);
        AddSegment(lines, p2, p3, MarqueeBorderColor);
        AddSegment(lines, p3, p0, MarqueeBorderColor);

        UploadData(_marqueeVao, _marqueeVbo, lines);
        _marqueeVertexCount = lines.Count / FloatStride;

        var fills = new List<float>();
        // Tri 1
        AddVertex(fills, minX, minY, z, MarqueeFillColor);
        AddVertex(fills, maxX, minY, z, MarqueeFillColor);
        AddVertex(fills, maxX, maxY, z, MarqueeFillColor);
        // Tri 2
        AddVertex(fills, minX, minY, z, MarqueeFillColor);
        AddVertex(fills, maxX, maxY, z, MarqueeFillColor);
        AddVertex(fills, minX, maxY, z, MarqueeFillColor);

        UploadData(_marqueeFillVao, _marqueeFillVbo, fills);
        _marqueeFillVertexCount = fills.Count / FloatStride;

        _hasMarquee = true;
    }

    public void ClearMarquee()
    {
        _hasMarquee = false;
        _marqueeVertexCount = 0;
        _marqueeFillVertexCount = 0;
    }

    public void Render(ShaderProgram shader, Matrix4 mvp)
    {
        if (!_hasPreview && !_hasSelection && !_hasMarquee)
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

        // 3. Draw Marquee Selection
        if (_hasMarquee)
        {
            if (_marqueeFillVertexCount > 0)
            {
                GL.BindVertexArray(_marqueeFillVao);
                GL.DrawArrays(PrimitiveType.Triangles, 0, _marqueeFillVertexCount);
            }

            if (_marqueeVertexCount > 0)
            {
                GL.LineWidth(1.5f);
                GL.BindVertexArray(_marqueeVao);
                GL.DrawArrays(PrimitiveType.Lines, 0, _marqueeVertexCount);
            }
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
            if (_marqueeVbo != 0) GL.DeleteBuffer(_marqueeVbo);
            if (_marqueeVao != 0) GL.DeleteVertexArray(_marqueeVao);
            if (_marqueeFillVbo != 0) GL.DeleteBuffer(_marqueeFillVbo);
            if (_marqueeFillVao != 0) GL.DeleteVertexArray(_marqueeFillVao);
            _disposed = true;
        }
    }
}
