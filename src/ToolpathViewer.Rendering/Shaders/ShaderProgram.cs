using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace ToolpathViewer.Rendering.Shaders;

public sealed class ShaderProgram : IDisposable
{
    public int Handle { get; private set; }
    private bool _disposed;

    public ShaderProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        int vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);

        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetProgramInfoLog(Handle);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteProgram(Handle);
            throw new InvalidOperationException($"Shader Program linking failed: {infoLog}");
        }

        // Shaders can be detached and deleted after linking
        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public void Use()
    {
        GL.UseProgram(Handle);
    }

    public void SetUniformMatrix4(string name, ref Matrix4 matrix)
    {
        int location = GL.GetUniformLocation(Handle, name);
        if (location >= 0)
        {
            GL.UniformMatrix4(location, false, ref matrix);
        }
    }

    public void SetUniformVector4(string name, Vector4 vector)
    {
        int location = GL.GetUniformLocation(Handle, name);
        if (location >= 0)
        {
            GL.Uniform4(location, vector);
        }
    }

    public void SetUniformFloat(string name, float value)
    {
        int location = GL.GetUniformLocation(Handle, name);
        if (location >= 0)
        {
            GL.Uniform1(location, value);
        }
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
        if (success == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new InvalidOperationException($"Error compiling {type}: {infoLog}");
        }

        return shader;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (Handle != 0)
            {
                GL.DeleteProgram(Handle);
                Handle = 0;
            }
            _disposed = true;
        }
    }
}
