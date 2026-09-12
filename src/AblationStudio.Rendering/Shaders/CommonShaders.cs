namespace AblationStudio.Rendering.Shaders;

public static class CommonShaders
{
    public const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;

        uniform mat4 uMvp;

        out vec4 vColor;

        void main()
        {
            gl_Position = uMvp * vec4(aPosition, 1.0);
            vColor = aColor;
        }
        """;

    public const string FragmentShaderSource = """
        #version 330 core
        in vec4 vColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vColor;
        }
        """;
}
