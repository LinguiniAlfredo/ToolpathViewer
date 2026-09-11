using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ToolpathViewer.Rendering.Shaders;

namespace ToolpathViewer.Rendering.Renderers;

public sealed class CoordinateAxesRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private bool _disposed;

    public bool IsVisible { get; set; } = true;
    public float AxisLength { get; set; } = 5.0f;

    public void Initialize()
    {
        if (_vao == 0)
        {
            _vao = GL.GenVertexArray();
            _vbo = GL.GenBuffer();
        }

        UpdateGeometry();
    }

    public void UpdateGeometry()
    {
        float len = AxisLength;
        float negLen = len * 0.15f;

        var red = new Vector4(0.95f, 0.25f, 0.25f, 1.0f);
        var dimRed = new Vector4(0.5f, 0.15f, 0.15f, 0.4f);

        var green = new Vector4(0.2f, 0.9f, 0.35f, 1.0f);
        var dimGreen = new Vector4(0.15f, 0.5f, 0.2f, 0.4f);

        var blue = new Vector4(0.25f, 0.6f, 1.0f, 1.0f);
        var dimBlue = new Vector4(0.15f, 0.3f, 0.6f, 0.4f);

        var vertices = new List<float>
        {
            // X Axis
            -negLen, 0f, 0f, dimRed.X, dimRed.Y, dimRed.Z, dimRed.W,
            0f, 0f, 0f, dimRed.X, dimRed.Y, dimRed.Z, dimRed.W,
            0f, 0f, 0f, red.X, red.Y, red.Z, red.W,
            len, 0f, 0f, red.X, red.Y, red.Z, red.W,

            // Y Axis
            0f, -negLen, 0f, dimGreen.X, dimGreen.Y, dimGreen.Z, dimGreen.W,
            0f, 0f, 0f, dimGreen.X, dimGreen.Y, dimGreen.Z, dimGreen.W,
            0f, 0f, 0f, green.X, green.Y, green.Z, green.W,
            0f, len, 0f, green.X, green.Y, green.Z, green.W,

            // Z Axis
            0f, 0f, -negLen, dimBlue.X, dimBlue.Y, dimBlue.Z, dimBlue.W,
            0f, 0f, 0f, dimBlue.X, dimBlue.Y, dimBlue.Z, dimBlue.W,
            0f, 0f, 0f, blue.X, blue.Y, blue.Z, blue.W,
            0f, 0f, len, blue.X, blue.Y, blue.Z, blue.W
        };

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
        if (!IsVisible || _vao == 0)
        {
            return;
        }

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(2.5f);

        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Lines, 0, 12);
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
