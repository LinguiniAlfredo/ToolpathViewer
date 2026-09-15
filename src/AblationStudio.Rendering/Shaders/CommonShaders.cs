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

    public const string ToolpathVertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in float aDistance;

        uniform mat4 uMvp;

        out vec4 vColor;
        out float vDistance;

        void main()
        {
            gl_Position = uMvp * vec4(aPosition, 1.0);
            vColor = aColor;
            vDistance = aDistance;
        }
        """;

    public const string ToolpathFragmentShaderSource = """
        #version 330 core
        in vec4 vColor;
        in float vDistance;

        uniform float uMaxDistance;
        uniform int uProgressiveMode;
        uniform float uGhostOpacity;

        out vec4 FragColor;

        void main()
        {
            if (uProgressiveMode != 0 && vDistance > uMaxDistance)
            {
                if (uGhostOpacity <= 0.0)
                {
                    discard;
                }
                FragColor = vec4(vColor.rgb * 0.4, vColor.a * uGhostOpacity);
                return;
            }
            FragColor = vColor;
        }
        """;

    public const string TextureVertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec2 aTexCoord;

        uniform mat4 uMvp;

        out vec2 vTexCoord;

        void main()
        {
            gl_Position = uMvp * vec4(aPosition, 1.0);
            vTexCoord = aTexCoord;
        }
        """;

    public const string TextureFragmentShaderSource = """
        #version 330 core
        in vec2 vTexCoord;

        uniform sampler2D uTexture;
        uniform float uOpacity;

        out vec4 FragColor;

        void main()
        {
            vec4 texColor = texture(uTexture, vTexCoord);
            FragColor = vec4(texColor.rgb, texColor.a * uOpacity);
        }
        """;
}

