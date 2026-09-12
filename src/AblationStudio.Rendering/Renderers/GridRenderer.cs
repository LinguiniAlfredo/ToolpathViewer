using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using AblationStudio.Rendering.Shaders;

namespace AblationStudio.Rendering.Renderers;

public sealed class GridRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _vertexCount;
    private bool _disposed;

    public bool IsVisible { get; set; } = true;

    public void Initialize(float size = 50f, float step = 1f, int majorEvery = 5)
    {
        if (_vao == 0)
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
        }

        var vertices = new List<float>();

        // Subtle dark theme grid colors
        var minorColor = new Vector4(0.25f, 0.25f, 0.28f, 0.4f);
        var majorColor = new Vector4(0.42f, 0.42f, 0.46f, 0.7f);
        var axisLineColor = new Vector4(0.55f, 0.55f, 0.60f, 0.85f);

        int count = (int)(size / step);

        for (int i = -count; i <= count; i++)
        {
            float pos = i * step;
            bool isAxis = i == 0;
            bool isMajor = (i % majorEvery) == 0;
            Vector4 color = isAxis ? axisLineColor : (isMajor ? majorColor : minorColor);

            // Parallel to Y axis (varying X)
            vertices.Add(pos); vertices.Add(-size); vertices.Add(0f);
            vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
            vertices.Add(pos); vertices.Add(size); vertices.Add(0f);
            vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);

            // Parallel to X axis (varying Y)
            vertices.Add(-size); vertices.Add(pos); vertices.Add(0f);
            vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
            vertices.Add(size); vertices.Add(pos); vertices.Add(0f);
            vertices.Add(color.X); vertices.Add(color.Y); vertices.Add(color.Z); vertices.Add(color.W);
        }

        _vertexCount = vertices.Count / 7;

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
    }

    public void Render(ShaderProgram shader, Matrix4 mvp)
    {
        if (!IsVisible || _vertexCount == 0)
        {
            return;
        }

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1.0f);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, _vertexCount);
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
