using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using AblationStudio.Core.Models;
using AblationStudio.Rendering.Shaders;

namespace AblationStudio.Rendering.Renderers;

public sealed class ToolIndicatorRenderer : IDisposable
{
    private const int FloatStride = 7;
    private const int StrideBytes = FloatStride * sizeof(float);

    private int _coneVao;
    private int _coneVbo;
    private int _coneVertexCount;

    private int _linesVao;
    private int _linesVbo;
    private int _linesVertexCount;

    private bool _disposed;

    public bool IsVisible { get; set; }

    public static readonly Vector4 CutColor = new(0.0f, 0.90f, 1.0f, 1.0f);     // Neon Cyan (Profile / M03)
    public static readonly Vector4 HatchColor = new(0.85f, 0.27f, 0.94f, 1.0f); // Neon Magenta (Hatch / M03)
    public static readonly Vector4 RapidColor = new(1.0f, 0.65f, 0.05f, 1.0f); // Vivid Amber (Rapid / M05)
    public static readonly Vector4 WhiteColor = new(1.0f, 1.0f, 1.0f, 0.85f);

    public void Initialize()
    {
        if (_coneVao == 0)
        {
            _coneVao = GL.GenVertexArray();
            _coneVbo = GL.GenBuffer();
            SetupVaoAttributes(_coneVao, _coneVbo);

            _linesVao = GL.GenVertexArray();
            _linesVbo = GL.GenBuffer();
            SetupVaoAttributes(_linesVao, _linesVbo);
        }
    }

    public void UpdatePosition(Vector3 position, SegmentType segmentType, float scale)
    {
        if (_coneVao == 0)
        {
            Initialize();
        }

        float s = Math.Clamp(scale, 0.2f, 25.0f);
        float height = s * 2.5f;
        float collarRadius = s * 0.8f;
        float reticleRadius = s * 0.6f;
        Vector4 mainColor = segmentType switch
        {
            SegmentType.Cut => CutColor,
            SegmentType.Hatch => HatchColor,
            _ => RapidColor
        };

        const int sides = 16;
        Vector3 tip = position;
        Vector3 capCenter = new(position.X, position.Y, position.Z + height);
        Vector3 lightDir = Vector3.Normalize(new Vector3(0.5f, -0.6f, 0.8f));

        // Precompute base perimeter points
        var basePoints = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * (MathF.Tau / sides);
            basePoints[i] = new Vector3(
                position.X + collarRadius * MathF.Cos(angle),
                position.Y + collarRadius * MathF.Sin(angle),
                position.Z + height
            );
        }

        // --- 1. Solid Cone Triangles ---
        var coneVertices = new List<float>();

        // Cone sides: connecting base rim to tip
        for (int i = 0; i < sides; i++)
        {
            Vector3 p1 = basePoints[i];
            Vector3 p2 = basePoints[(i + 1) % sides];
            Vector3 p3 = tip;

            Vector3 edge1 = p2 - p1;
            Vector3 edge2 = p3 - p1;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));

            float diffuse = MathF.Max(0f, Vector3.Dot(normal, lightDir));
            float intensity = 0.45f + 0.55f * diffuse;

            Vector4 facetColor = new(
                Math.Clamp(mainColor.X * intensity, 0f, 1f),
                Math.Clamp(mainColor.Y * intensity, 0f, 1f),
                Math.Clamp(mainColor.Z * intensity, 0f, 1f),
                0.95f
            );

            Vector4 tipColor = new(
                Math.Clamp(facetColor.X * 1.25f, 0f, 1f),
                Math.Clamp(facetColor.Y * 1.25f, 0f, 1f),
                Math.Clamp(facetColor.Z * 1.25f, 0f, 1f),
                1.0f
            );

            AddTriangle(coneVertices, p1, facetColor, p2, facetColor, p3, tipColor);
        }

        // Top Cap: circular disk at base
        Vector3 capNormal = Vector3.UnitZ;
        float capDiffuse = MathF.Max(0f, Vector3.Dot(capNormal, lightDir));
        float capIntensity = 0.50f + 0.50f * capDiffuse;
        Vector4 capColor = new(
            Math.Clamp(mainColor.X * capIntensity, 0f, 1f),
            Math.Clamp(mainColor.Y * capIntensity, 0f, 1f),
            Math.Clamp(mainColor.Z * capIntensity, 0f, 1f),
            0.95f
        );

        for (int i = 0; i < sides; i++)
        {
            Vector3 p1 = capCenter;
            Vector3 p2 = basePoints[(i + 1) % sides];
            Vector3 p3 = basePoints[i];

            AddTriangle(coneVertices, p1, capColor, p2, capColor, p3, capColor);
        }

        _coneVertexCount = coneVertices.Count / FloatStride;
        UploadBufferData(_coneVao, _coneVbo, coneVertices);

        // --- 2. Reticle and Rim Lines ---
        var lineVertices = new List<float>();

        // Focal Reticle Ring on XY plane
        for (int i = 0; i < sides; i++)
        {
            float a1 = i * (MathF.Tau / sides);
            float a2 = (i + 1) * (MathF.Tau / sides);

            float r1X = position.X + reticleRadius * MathF.Cos(a1);
            float r1Y = position.Y + reticleRadius * MathF.Sin(a1);
            float r2X = position.X + reticleRadius * MathF.Cos(a2);
            float r2Y = position.Y + reticleRadius * MathF.Sin(a2);

            AddLine(lineVertices, r1X, r1Y, position.Z, r2X, r2Y, position.Z, mainColor);
        }

        // Reticle Crosshairs (+X, -X, +Y, -Y)
        float chLen = s * 1.2f;
        AddLine(lineVertices, position.X - chLen, position.Y, position.Z, position.X + chLen, position.Y, position.Z, mainColor);
        AddLine(lineVertices, position.X, position.Y - chLen, position.Z, position.X, position.Y + chLen, position.Z, mainColor);

        // Base collar rim outline for crisp silhouette
        for (int i = 0; i < sides; i++)
        {
            Vector3 p1 = basePoints[i];
            Vector3 p2 = basePoints[(i + 1) % sides];
            AddLine(lineVertices, p1.X, p1.Y, p1.Z, p2.X, p2.Y, p2.Z, WhiteColor);
        }

        _linesVertexCount = lineVertices.Count / FloatStride;
        UploadBufferData(_linesVao, _linesVbo, lineVertices);
    }

    public void Render(ShaderProgram shader, Matrix4 mvp)
    {
        if (!IsVisible || _coneVao == 0)
        {
            return;
        }

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);

        // 1. Draw solid cone and top cap
        if (_coneVertexCount > 0)
        {
            GL.BindVertexArray(_coneVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _coneVertexCount);
        }

        // 2. Draw reticle lines & base rim
        if (_linesVertexCount > 0 && _linesVao != 0)
        {
            GL.LineWidth(2.0f);
            GL.BindVertexArray(_linesVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _linesVertexCount);
        }

        GL.BindVertexArray(0);
    }

    private static void SetupVaoAttributes(int vao, int vbo)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

        // Location 0: aPosition (vec3)
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, StrideBytes, 0);

        // Location 1: aColor (vec4)
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, StrideBytes, 3 * sizeof(float));

        GL.BindVertexArray(0);
    }

    private static void UploadBufferData(int vao, int vbo, List<float> data)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        float[] array = data.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * sizeof(float), array, BufferUsageHint.DynamicDraw);
        GL.BindVertexArray(0);
    }

    private static void AddTriangle(List<float> buffer, Vector3 p1, Vector4 c1, Vector3 p2, Vector4 c2, Vector3 p3, Vector4 c3)
    {
        buffer.Add(p1.X); buffer.Add(p1.Y); buffer.Add(p1.Z);
        buffer.Add(c1.X); buffer.Add(c1.Y); buffer.Add(c1.Z); buffer.Add(c1.W);

        buffer.Add(p2.X); buffer.Add(p2.Y); buffer.Add(p2.Z);
        buffer.Add(c2.X); buffer.Add(c2.Y); buffer.Add(c2.Z); buffer.Add(c2.W);

        buffer.Add(p3.X); buffer.Add(p3.Y); buffer.Add(p3.Z);
        buffer.Add(c3.X); buffer.Add(c3.Y); buffer.Add(c3.Z); buffer.Add(c3.W);
    }

    private static void AddLine(List<float> buffer, float x1, float y1, float z1, float x2, float y2, float z2, Vector4 color)
    {
        buffer.Add(x1); buffer.Add(y1); buffer.Add(z1);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);

        buffer.Add(x2); buffer.Add(y2); buffer.Add(z2);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_coneVbo != 0)
            {
                GL.DeleteBuffer(_coneVbo);
                _coneVbo = 0;
            }

            if (_coneVao != 0)
            {
                GL.DeleteVertexArray(_coneVao);
                _coneVao = 0;
            }

            if (_linesVbo != 0)
            {
                GL.DeleteBuffer(_linesVbo);
                _linesVbo = 0;
            }

            if (_linesVao != 0)
            {
                GL.DeleteVertexArray(_linesVao);
                _linesVao = 0;
            }

            _disposed = true;
        }
    }
}
