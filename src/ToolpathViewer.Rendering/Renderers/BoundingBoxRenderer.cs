using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ToolpathViewer.Core.Models;
using ToolpathViewer.Rendering.Shaders;

namespace ToolpathViewer.Rendering.Renderers;

public sealed class BoundingBoxRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private bool _hasBox;
    private bool _disposed;

    public bool IsVisible { get; set; } = true;
    public static readonly Vector4 BoxColor = new(0.45f, 0.45f, 0.50f, 0.45f);

    public void Initialize()
    {
        if (_vao == 0)
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
        }
    }

    public void UpdateBox(BoundingBox3D bbox)
    {
        if (_vao == 0)
        {
            Initialize();
        }

        if (bbox.IsEmpty)
        {
            _hasBox = false;
            return;
        }

        float x0 = bbox.MinX; float x1 = bbox.MaxX;
        float y0 = bbox.MinY; float y1 = bbox.MaxY;
        float z0 = bbox.MinZ; float z1 = bbox.MaxZ;

        var vertices = new List<float>();

        void AddEdge(float ax, float ay, float az, float bx, float by, float bz)
        {
            vertices.Add(ax); vertices.Add(ay); vertices.Add(az);
            vertices.Add(BoxColor.X); vertices.Add(BoxColor.Y); vertices.Add(BoxColor.Z); vertices.Add(BoxColor.W);
            vertices.Add(bx); vertices.Add(by); vertices.Add(bz);
            vertices.Add(BoxColor.X); vertices.Add(BoxColor.Y); vertices.Add(BoxColor.Z); vertices.Add(BoxColor.W);
        }

        // Bottom face edges
        AddEdge(x0, y0, z0, x1, y0, z0);
        AddEdge(x1, y0, z0, x1, y1, z0);
        AddEdge(x1, y1, z0, x0, y1, z0);
        AddEdge(x0, y1, z0, x0, y0, z0);

        // Top face edges
        AddEdge(x0, y0, z1, x1, y0, z1);
        AddEdge(x1, y0, z1, x1, y1, z1);
        AddEdge(x1, y1, z1, x0, y1, z1);
        AddEdge(x0, y1, z1, x0, y0, z1);

        // Vertical pillar edges
        AddEdge(x0, y0, z0, x0, y0, z1);
        AddEdge(x1, y0, z0, x1, y0, z1);
        AddEdge(x1, y1, z0, x1, y1, z1);
        AddEdge(x0, y1, z0, x0, y1, z1);

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        float[] array = vertices.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * sizeof(float), array, BufferUsageHint.StaticDraw);

        const int stride = 7 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

        GL.BindVertexArray(0);

        _hasBox = true;
    }

    public void Render(ShaderProgram shader, Matrix4 mvp)
    {
        if (!IsVisible || !_hasBox || _vao == 0)
        {
            return;
        }

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1.0f);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, 24);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_vbo != 0) GL.DeleteBuffer(_vbo);
            if (_vao != 0) GL.DeleteVertexArray(_vao);
            _disposed = true;
        }
    }
}
