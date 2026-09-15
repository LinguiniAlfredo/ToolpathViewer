using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using AblationStudio.Core.Shapes;
using AblationStudio.Rendering.Shaders;

namespace AblationStudio.Rendering.Renderers;

public sealed class ImageOverlayRenderer : IDisposable
{
    private sealed class CachedImageTexture : IDisposable
    {
        public int TextureHandle { get; set; }
        public int Vao { get; set; }
        public int Vbo { get; set; }
        public int Version { get; set; }
        public int PixelWidth { get; set; }
        public int PixelHeight { get; set; }

        public void Dispose()
        {
            if (TextureHandle != 0)
            {
                GL.DeleteTexture(TextureHandle);
                TextureHandle = 0;
            }
            if (Vbo != 0)
            {
                GL.DeleteBuffer(Vbo);
                Vbo = 0;
            }
            if (Vao != 0)
            {
                GL.DeleteVertexArray(Vao);
                Vao = 0;
            }
        }
    }

    private readonly Dictionary<string, CachedImageTexture> _textures = [];
    private ShaderProgram? _shader;
    private bool _isInitialized;
    private bool _disposed;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _shader = new ShaderProgram(CommonShaders.TextureVertexShaderSource, CommonShaders.TextureFragmentShaderSource);
        _isInitialized = true;
    }

    public void Render(Matrix4 mvp, IEnumerable<ToolpathShape>? shapes)
    {
        if (!_isInitialized || _shader is null || shapes is null)
        {
            return;
        }

        var imageShapes = shapes.OfType<ImageShape>().Where(img => img.ShowOverlay && img.OverlayOpacity > 0.001f).ToList();
        if (imageShapes.Count == 0)
        {
            return;
        }

        _shader.Use();
        _shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        // Active texture unit 0
        GL.ActiveTexture(TextureUnit.Texture0);
        _shader.SetUniformInt("uTexture", 0);

        var activeIds = new HashSet<string>();

        foreach (ImageShape img in imageShapes)
        {
            activeIds.Add(img.Id);

            if (!_textures.TryGetValue(img.Id, out CachedImageTexture? cached))
            {
                cached = new CachedImageTexture();
                _textures[img.Id] = cached;
                UploadTexture(cached, img);
            }
            else if (cached.PixelWidth != img.PixelWidth || cached.PixelHeight != img.PixelHeight)
            {
                UploadTexture(cached, img);
            }

            UpdateQuadGeometry(cached, img);

            _shader.SetUniformFloat("uOpacity", img.OverlayOpacity);

            GL.BindTexture(TextureTarget.Texture2D, cached.TextureHandle);
            GL.BindVertexArray(cached.Vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        }

        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        // Evict removed shapes
        List<string>? toRemove = null;
        foreach (string id in _textures.Keys)
        {
            if (!activeIds.Contains(id))
            {
                toRemove ??= [];
                toRemove.Add(id);
            }
        }

        if (toRemove is not null)
        {
            foreach (string id in toRemove)
            {
                _textures[id].Dispose();
                _textures.Remove(id);
            }
        }
    }

    private static void UploadTexture(CachedImageTexture cached, ImageShape img)
    {
        if (cached.TextureHandle == 0)
        {
            cached.TextureHandle = GL.GenTexture();
        }

        GL.BindTexture(TextureTarget.Texture2D, cached.TextureHandle);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        cached.PixelWidth = img.PixelWidth;
        cached.PixelHeight = img.PixelHeight;

        int w = img.PixelWidth;
        int h = img.PixelHeight;

        if (img.PixelData.Length >= w * h && w > 0 && h > 0)
        {
            // Upload 8-bit grayscale pixel data as Red channel with swizzle to RGB
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.R8,
                w,
                h,
                0,
                PixelFormat.Red,
                PixelType.UnsignedByte,
                img.PixelData);

            // Replicate R component to G, B, and full Alpha
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleR, (int)All.Red);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleG, (int)All.Red);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleB, (int)All.Red);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleA, (int)All.One);
        }

        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private static void UpdateQuadGeometry(CachedImageTexture cached, ImageShape img)
    {
        if (cached.Vao == 0)
        {
            cached.Vao = GL.GenVertexArray();
            cached.Vbo = GL.GenBuffer();

            GL.BindVertexArray(cached.Vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, cached.Vbo);

            const int stride = 5 * sizeof(float);
            // location 0: vec3 aPosition
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);

            // location 1: vec2 aTexCoord
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));

            GL.BindVertexArray(0);
        }

        float hw = img.Width * 0.5f;
        float hh = img.Height * 0.5f;
        float rad = img.RotationDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        // Place image quad minutely below toolpath segments (Z - 0.005) to avoid Z-fighting
        float z = img.PositionZ - 0.005f;

        (float X, float Y) Transform(float lx, float ly)
        {
            return (
                img.PositionX + lx * cos - ly * sin,
                img.PositionY + lx * sin + ly * cos
            );
        }

        // 4 corners: Top-Left, Bottom-Left, Bottom-Right, Top-Right
        // In texture coords, U goes 0..1 (left to right), V goes 0..1 (top to bottom)
        var tl = Transform(-hw, hh);
        var bl = Transform(-hw, -hh);
        var br = Transform(hw, -hh);
        var tr = Transform(hw, hh);

        // 2 Triangles: (tl, bl, br) and (tl, br, tr)
        float[] vertices =
        [
            // Triangle 1
            tl.X, tl.Y, z, 0.0f, 0.0f,
            bl.X, bl.Y, z, 0.0f, 1.0f,
            br.X, br.Y, z, 1.0f, 1.0f,

            // Triangle 2
            tl.X, tl.Y, z, 0.0f, 0.0f,
            br.X, br.Y, z, 1.0f, 1.0f,
            tr.X, tr.Y, z, 1.0f, 0.0f
        ];

        GL.BindVertexArray(cached.Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, cached.Vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (CachedImageTexture cached in _textures.Values)
            {
                cached.Dispose();
            }
            _textures.Clear();
            _shader?.Dispose();
            _shader = null;
            _disposed = true;
        }
    }
}
