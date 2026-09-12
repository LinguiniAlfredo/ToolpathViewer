using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using AblationStudio.Core.Models;
using AblationStudio.Rendering.Shaders;

namespace AblationStudio.Rendering.Renderers;

public sealed class ToolpathRenderer : IDisposable
{
    private const int FloatStride = 8;
    private const int StrideBytes = FloatStride * sizeof(float);

    private ShaderProgram? _shader;

    private int _cutVao;
    private int _cutVbo;
    private int _cutVertexCount;

    private int _hatchVao;
    private int _hatchVbo;
    private int _hatchVertexCount;

    private int _rapidVao;
    private int _rapidVbo;
    private int _rapidVertexCount;

    private int _markersVao;
    private int _markersVbo;
    private int _markersVertexCount;

    private bool _hasData;
    private bool _disposed;

    // Fluent Colors
    public static readonly Vector4 CutColor = new(0.0f, 0.85f, 1.0f, 1.0f);     // Fluent Cyan (Profile)
    public static readonly Vector4 HatchColor = new(0.85f, 0.27f, 0.94f, 1.0f); // Fluent Neon Magenta (Hatch)
    public static readonly Vector4 RapidColor = new(1.0f, 0.65f, 0.0f, 0.65f);  // Fluent Amber
    public static readonly Vector4 StartNodeColor = new(0.1f, 0.9f, 0.2f, 1.0f);// Green
    public static readonly Vector4 EndNodeColor = new(1.0f, 0.25f, 0.25f, 1.0f);// Red

    public bool ShowCuts { get; set; } = true;
    public bool ShowHatch { get; set; } = true;
    public bool ShowRapids { get; set; } = true;
    public bool ShowMarkers { get; set; } = true;
    public float LineWidth { get; set; } = 2.0f;

    public bool IsProgressive { get; set; }
    public float CurrentDistance { get; set; }
    public bool ShowGhostPath { get; set; }
    public float GhostOpacity { get; set; } = 0.20f;

    public void Initialize()
    {
        _shader ??= new ShaderProgram(CommonShaders.ToolpathVertexShaderSource, CommonShaders.ToolpathFragmentShaderSource);

        if (_cutVao == 0)
        {
            _cutVao = GL.GenVertexArray();
            _cutVbo = GL.GenBuffer();
            SetupVaoAttributes(_cutVao, _cutVbo);

            _hatchVao = GL.GenVertexArray();
            _hatchVbo = GL.GenBuffer();
            SetupVaoAttributes(_hatchVao, _hatchVbo);

            _rapidVao = GL.GenVertexArray();
            _rapidVbo = GL.GenBuffer();
            SetupVaoAttributes(_rapidVao, _rapidVbo);

            _markersVao = GL.GenVertexArray();
            _markersVbo = GL.GenBuffer();
            SetupVaoAttributes(_markersVao, _markersVbo);
        }
    }

    public void LoadToolpath(Toolpath toolpath)
    {
        if (_cutVao == 0)
        {
            Initialize();
        }

        var cutVertices = new List<float>();
        var hatchVertices = new List<float>();
        var rapidVertices = new List<float>();
        var markerVertices = new List<float>();

        float cumulativeDist = 0f;

        foreach (ToolpathSegment seg in toolpath.Segments)
        {
            float segStartDist = cumulativeDist;
            float segEndDist = cumulativeDist + seg.Length;
            cumulativeDist = segEndDist;

            switch (seg.Type)
            {
                case SegmentType.Cut:
                    AddSegmentVertices(cutVertices, seg, CutColor, segStartDist, segEndDist);
                    break;
                case SegmentType.Hatch:
                    AddSegmentVertices(hatchVertices, seg, HatchColor, segStartDist, segEndDist);
                    break;
                case SegmentType.Rapid:
                default:
                    AddSegmentVertices(rapidVertices, seg, RapidColor, segStartDist, segEndDist);
                    break;
            }
        }

        if (toolpath.Segments.Count > 0)
        {
            ToolpathPoint first = toolpath.Segments[0].Start;
            ToolpathPoint last = toolpath.Segments[^1].End;
            AddCrossMarker(markerVertices, first, StartNodeColor, 0.2f, 0f);
            AddCrossMarker(markerVertices, last, EndNodeColor, 0.2f, toolpath.Statistics.TotalLength);
        }

        // Upload Cut buffers
        _cutVertexCount = cutVertices.Count / FloatStride;
        UploadBufferData(_cutVao, _cutVbo, cutVertices);

        // Upload Hatch buffers
        _hatchVertexCount = hatchVertices.Count / FloatStride;
        UploadBufferData(_hatchVao, _hatchVbo, hatchVertices);

        // Upload Rapid buffers
        _rapidVertexCount = rapidVertices.Count / FloatStride;
        UploadBufferData(_rapidVao, _rapidVbo, rapidVertices);

        // Upload Markers buffer
        _markersVertexCount = markerVertices.Count / FloatStride;
        UploadBufferData(_markersVao, _markersVbo, markerVertices);

        _hasData = toolpath.Segments.Count > 0;
    }

    public void Render(Matrix4 mvp)
    {
        if (!_hasData)
        {
            return;
        }

        if (_shader is null)
        {
            Initialize();
        }

        if (_shader is null)
        {
            return;
        }

        _shader.Use();
        _shader.SetUniformMatrix4("uMvp", ref mvp);
        _shader.SetUniformInt("uProgressiveMode", IsProgressive ? 1 : 0);
        _shader.SetUniformFloat("uMaxDistance", CurrentDistance);
        _shader.SetUniformFloat("uGhostOpacity", ShowGhostPath ? GhostOpacity : 0.0f);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(Math.Clamp(LineWidth, 1.0f, 10.0f));

        // Draw Rapid moves first so Cuts and Hatch are rendered on top
        if (ShowRapids && _rapidVertexCount > 0)
        {
            GL.BindVertexArray(_rapidVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _rapidVertexCount);
        }

        // Draw Hatch infill moves
        if (ShowHatch && _hatchVertexCount > 0)
        {
            GL.BindVertexArray(_hatchVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _hatchVertexCount);
        }

        // Draw Profile Cutting moves on top of hatch
        if (ShowCuts && _cutVertexCount > 0)
        {
            GL.BindVertexArray(_cutVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _cutVertexCount);
        }

        // Draw Start/End Markers
        if (ShowMarkers && _markersVertexCount > 0)
        {
            GL.LineWidth(3.0f);
            GL.BindVertexArray(_markersVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, _markersVertexCount);
        }

        GL.BindVertexArray(0);
    }

    public void Render(ShaderProgram? shader, Matrix4 mvp)
    {
        Render(mvp);
    }

    private static void AddSegmentVertices(List<float> buffer, ToolpathSegment segment, Vector4 color, float startDist, float endDist)
    {
        // Start vertex
        buffer.Add(segment.Start.X);
        buffer.Add(segment.Start.Y);
        buffer.Add(segment.Start.Z);
        buffer.Add(color.X);
        buffer.Add(color.Y);
        buffer.Add(color.Z);
        buffer.Add(color.W);
        buffer.Add(startDist);

        // End vertex
        buffer.Add(segment.End.X);
        buffer.Add(segment.End.Y);
        buffer.Add(segment.End.Z);
        buffer.Add(color.X);
        buffer.Add(color.Y);
        buffer.Add(color.Z);
        buffer.Add(color.W);
        buffer.Add(endDist);
    }

    private static void AddCrossMarker(List<float> buffer, ToolpathPoint pt, Vector4 color, float size, float distance)
    {
        float s = size * 0.5f;

        // X line
        buffer.Add(pt.X - s); buffer.Add(pt.Y); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);
        buffer.Add(pt.X + s); buffer.Add(pt.Y); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);

        // Y line
        buffer.Add(pt.X); buffer.Add(pt.Y - s); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);
        buffer.Add(pt.X); buffer.Add(pt.Y + s); buffer.Add(pt.Z);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);

        // Z line
        buffer.Add(pt.X); buffer.Add(pt.Y); buffer.Add(pt.Z - s);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);
        buffer.Add(pt.X); buffer.Add(pt.Y); buffer.Add(pt.Z + s);
        buffer.Add(color.X); buffer.Add(color.Y); buffer.Add(color.Z); buffer.Add(color.W);
        buffer.Add(distance);
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

        // Location 2: aDistance (float)
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, StrideBytes, 7 * sizeof(float));

        GL.BindVertexArray(0);
    }

    private static void UploadBufferData(int vao, int vbo, List<float> data)
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        float[] array = data.ToArray();
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * sizeof(float), array, BufferUsageHint.StaticDraw);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_cutVbo != 0) GL.DeleteBuffer(_cutVbo);
            if (_cutVao != 0) GL.DeleteVertexArray(_cutVao);
            if (_hatchVbo != 0) GL.DeleteBuffer(_hatchVbo);
            if (_hatchVao != 0) GL.DeleteVertexArray(_hatchVao);
            if (_rapidVbo != 0) GL.DeleteBuffer(_rapidVbo);
            if (_rapidVao != 0) GL.DeleteVertexArray(_rapidVao);
            if (_markersVbo != 0) GL.DeleteBuffer(_markersVbo);
            if (_markersVao != 0) GL.DeleteVertexArray(_markersVao);

            _shader?.Dispose();
            _shader = null;

            _disposed = true;
        }
    }
}
