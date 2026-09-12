using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using ToolpathViewer.Rendering.Camera;
using ToolpathViewer.Rendering.Shaders;

namespace ToolpathViewer.Rendering.Renderers;

public sealed class OrientationGizmoRenderer : IDisposable
{
    private const int FloatStride = 7;
    private const int StrideBytes = FloatStride * sizeof(float);

    private int _vaoSolid;
    private int _vboSolid;
    private int _solidVertexCount;

    private int _vaoEdges;
    private int _vboEdges;
    private int _edgeVertexCount;

    private int _vaoLetters;
    private int _vboLetters;
    private int _letterVertexCount;

    private bool _disposed;

    public bool IsVisible { get; set; } = true;
    public int GizmoSize { get; set; } = 130;
    public int Margin { get; set; } = 16;

    public void Initialize()
    {
        if (_vaoSolid == 0)
        {
            _vaoSolid = GL.GenVertexArray();
            _vboSolid = GL.GenBuffer();
            _vaoEdges = GL.GenVertexArray();
            _vboEdges = GL.GenBuffer();
            _vaoLetters = GL.GenVertexArray();
            _vboLetters = GL.GenBuffer();
        }

        BuildStaticGeometry();
        InitializeLetterBuffer();
    }

    private void BuildStaticGeometry()
    {
        var solidVertices = new List<float>();
        var edgeVertices = new List<float>();

        const float cubeHalf = 0.22f;

        // 1. Central 3D Cube Faces
        BuildCubeFaces(solidVertices, cubeHalf);

        // 2. Central 3D Cube Edges
        BuildCubeEdges(edgeVertices, cubeHalf);

        // 3. Positive Axis-Aligned Arrows (Extruded outward from center)
        const float shaftRadius = 0.035f;
        const float shaftLength = 0.72f;
        const float headRadius = 0.095f;
        const float headLength = 0.30f;
        const int segments = 12;

        // +X Axis (Red)
        var redColor = new Vector4(0.96f, 0.26f, 0.21f, 1.0f);
        AddArrow(solidVertices, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
            redColor, shaftLength, shaftRadius, headLength, headRadius, segments);

        // +Y Axis (Green)
        var greenColor = new Vector4(0.27f, 0.75f, 0.35f, 1.0f);
        AddArrow(solidVertices, Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX,
            greenColor, shaftLength, shaftRadius, headLength, headRadius, segments);

        // +Z Axis (Blue)
        var blueColor = new Vector4(0.18f, 0.54f, 0.95f, 1.0f);
        AddArrow(solidVertices, Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY,
            blueColor, shaftLength, shaftRadius, headLength, headRadius, segments);

        // Upload Solid Geometry
        _solidVertexCount = solidVertices.Count / FloatStride;
        float[] solidArray = solidVertices.ToArray();
        GL.BindVertexArray(_vaoSolid);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboSolid);
        GL.BufferData(BufferTarget.ArrayBuffer, solidArray.Length * sizeof(float), solidArray, BufferUsageHint.StaticDraw);
        ConfigureAttributes();

        // Upload Edge Geometry
        _edgeVertexCount = edgeVertices.Count / FloatStride;
        float[] edgeArray = edgeVertices.ToArray();
        GL.BindVertexArray(_vaoEdges);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboEdges);
        GL.BufferData(BufferTarget.ArrayBuffer, edgeArray.Length * sizeof(float), edgeArray, BufferUsageHint.StaticDraw);
        ConfigureAttributes();

        GL.BindVertexArray(0);
    }

    private static void BuildCubeFaces(List<float> vertices, float s)
    {
        // Directional face colors with subtle axis tints
        var colorPosX = new Vector4(0.50f, 0.28f, 0.28f, 1.0f);
        var colorNegX = new Vector4(0.32f, 0.34f, 0.38f, 1.0f);
        var colorPosY = new Vector4(0.28f, 0.48f, 0.30f, 1.0f);
        var colorNegY = new Vector4(0.32f, 0.34f, 0.38f, 1.0f);
        var colorPosZ = new Vector4(0.28f, 0.36f, 0.50f, 1.0f);
        var colorNegZ = new Vector4(0.32f, 0.34f, 0.38f, 1.0f);

        // +X Face
        AddQuad(vertices,
            new Vector3(s, -s, -s), new Vector3(s, s, -s), new Vector3(s, s, s), new Vector3(s, -s, s),
            Vector3.UnitX, colorPosX);

        // -X Face
        AddQuad(vertices,
            new Vector3(-s, s, -s), new Vector3(-s, -s, -s), new Vector3(-s, -s, s), new Vector3(-s, s, s),
            -Vector3.UnitX, colorNegX);

        // +Y Face
        AddQuad(vertices,
            new Vector3(s, s, -s), new Vector3(-s, s, -s), new Vector3(-s, s, s), new Vector3(s, s, s),
            Vector3.UnitY, colorPosY);

        // -Y Face
        AddQuad(vertices,
            new Vector3(-s, -s, -s), new Vector3(s, -s, -s), new Vector3(s, -s, s), new Vector3(-s, -s, s),
            -Vector3.UnitY, colorNegY);

        // +Z Face
        AddQuad(vertices,
            new Vector3(-s, -s, s), new Vector3(s, -s, s), new Vector3(s, s, s), new Vector3(-s, s, s),
            Vector3.UnitZ, colorPosZ);

        // -Z Face
        AddQuad(vertices,
            new Vector3(-s, s, -s), new Vector3(s, s, -s), new Vector3(s, -s, -s), new Vector3(-s, -s, -s),
            -Vector3.UnitZ, colorNegZ);
    }

    private static void BuildCubeEdges(List<float> vertices, float s)
    {
        var edgeColor = new Vector4(0.85f, 0.88f, 0.95f, 0.90f);

        Vector3[] p =
        [
            new(-s, -s, -s), new(s, -s, -s), new(s, s, -s), new(-s, s, -s),
            new(-s, -s, s),  new(s, -s, s),  new(s, s, s),  new(-s, s, s)
        ];

        // Bottom face
        AddLine(vertices, p[0], p[1], edgeColor);
        AddLine(vertices, p[1], p[2], edgeColor);
        AddLine(vertices, p[2], p[3], edgeColor);
        AddLine(vertices, p[3], p[0], edgeColor);

        // Top face
        AddLine(vertices, p[4], p[5], edgeColor);
        AddLine(vertices, p[5], p[6], edgeColor);
        AddLine(vertices, p[6], p[7], edgeColor);
        AddLine(vertices, p[7], p[4], edgeColor);

        // Vertical pillars
        AddLine(vertices, p[0], p[4], edgeColor);
        AddLine(vertices, p[1], p[5], edgeColor);
        AddLine(vertices, p[2], p[6], edgeColor);
        AddLine(vertices, p[3], p[7], edgeColor);
    }

    private static void AddArrow(
        List<float> vertices,
        Vector3 direction,
        Vector3 perp1,
        Vector3 perp2,
        Vector4 baseColor,
        float shaftLength,
        float shaftRadius,
        float headLength,
        float headRadius,
        int segments)
    {
        float totalLength = shaftLength + headLength;
        Vector3 tip = direction * totalLength;
        Vector3 shaftEnd = direction * shaftLength;

        for (int i = 0; i < segments; i++)
        {
            float theta0 = (float)(i * 2.0 * Math.PI / segments);
            float theta1 = (float)((i + 1) * 2.0 * Math.PI / segments);

            Vector3 r0 = MathF.Cos(theta0) * perp1 + MathF.Sin(theta0) * perp2;
            Vector3 r1 = MathF.Cos(theta1) * perp1 + MathF.Sin(theta1) * perp2;

            // 1. Shaft Cylinder Quad (2 triangles)
            Vector3 s0 = r0 * shaftRadius;
            Vector3 s1 = r1 * shaftRadius;
            Vector3 s0End = shaftEnd + s0;
            Vector3 s1End = shaftEnd + s1;

            Vector3 facetNormal = Vector3.Normalize(r0 + r1);
            Vector4 shaftLitColor = ComputeLighting(baseColor, facetNormal);

            AddVertex(vertices, s0, shaftLitColor);
            AddVertex(vertices, s0End, shaftLitColor);
            AddVertex(vertices, s1End, shaftLitColor);

            AddVertex(vertices, s0, shaftLitColor);
            AddVertex(vertices, s1End, shaftLitColor);
            AddVertex(vertices, s1, shaftLitColor);

            // 2. Arrowhead Base Disk (flat back face)
            Vector3 h0 = shaftEnd + r0 * headRadius;
            Vector3 h1 = shaftEnd + r1 * headRadius;

            Vector4 baseDiskLitColor = ComputeLighting(baseColor, -direction);
            AddVertex(vertices, shaftEnd, baseDiskLitColor);
            AddVertex(vertices, h1, baseDiskLitColor);
            AddVertex(vertices, h0, baseDiskLitColor);

            // 3. Arrowhead Conical Side Triangle
            Vector3 coneNormal = Vector3.Normalize(facetNormal + direction * (headRadius / headLength));
            Vector4 coneLitColor = ComputeLighting(baseColor, coneNormal);

            AddVertex(vertices, tip, coneLitColor);
            AddVertex(vertices, h0, coneLitColor);
            AddVertex(vertices, h1, coneLitColor);
        }
    }

    private static void AddQuad(
        List<float> vertices,
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector3 normal,
        Vector4 color)
    {
        Vector4 litColor = ComputeLighting(color, normal);

        AddVertex(vertices, p0, litColor);
        AddVertex(vertices, p1, litColor);
        AddVertex(vertices, p2, litColor);

        AddVertex(vertices, p0, litColor);
        AddVertex(vertices, p2, litColor);
        AddVertex(vertices, p3, litColor);
    }

    private static void AddLine(List<float> vertices, Vector3 p0, Vector3 p1, Vector4 color)
    {
        AddVertex(vertices, p0, color);
        AddVertex(vertices, p1, color);
    }

    private static void AddVertex(List<float> vertices, Vector3 pos, Vector4 color)
    {
        vertices.Add(pos.X);
        vertices.Add(pos.Y);
        vertices.Add(pos.Z);
        vertices.Add(color.X);
        vertices.Add(color.Y);
        vertices.Add(color.Z);
        vertices.Add(color.W);
    }

    private static Vector4 ComputeLighting(Vector4 color, Vector3 normal)
    {
        var lightDir = Vector3.Normalize(new Vector3(1.2f, 1.6f, 2.4f));
        float diff = MathF.Max(0f, Vector3.Dot(normal, lightDir));
        float factor = 0.45f + 0.55f * diff;
        return new Vector4(color.X * factor, color.Y * factor, color.Z * factor, color.W);
    }

    private void InitializeLetterBuffer()
    {
        // 8 lines * 2 vertices = 16 vertices total
        const int totalLetterVertices = 16;
        _letterVertexCount = totalLetterVertices;

        GL.BindVertexArray(_vaoLetters);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboLetters);
        GL.BufferData(BufferTarget.ArrayBuffer, totalLetterVertices * StrideBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        ConfigureAttributes();
        GL.BindVertexArray(0);
    }

    private void UpdateBillboardLetters(Vector3 camRight, Vector3 camUp)
    {
        var vertices = new float[16 * FloatStride];
        int offset = 0;

        const float labelDistance = 1.18f;
        const float letterW = 0.045f;
        const float letterH = 0.055f;

        // 'X' Letter (Red)
        var red = new Vector4(0.98f, 0.30f, 0.25f, 1.0f);
        Vector3 posX = Vector3.UnitX * labelDistance;
        AddLetterLine(vertices, ref offset,
            posX - letterW * camRight - letterH * camUp,
            posX + letterW * camRight + letterH * camUp, red);
        AddLetterLine(vertices, ref offset,
            posX - letterW * camRight + letterH * camUp,
            posX + letterW * camRight - letterH * camUp, red);

        // 'Y' Letter (Green)
        var green = new Vector4(0.30f, 0.88f, 0.38f, 1.0f);
        Vector3 posY = Vector3.UnitY * labelDistance;
        Vector3 junction = posY;
        AddLetterLine(vertices, ref offset,
            junction,
            posY - letterW * camRight + letterH * camUp, green);
        AddLetterLine(vertices, ref offset,
            junction,
            posY + letterW * camRight + letterH * camUp, green);
        AddLetterLine(vertices, ref offset,
            junction,
            posY - letterH * camUp, green);

        // 'Z' Letter (Blue)
        var blue = new Vector4(0.28f, 0.65f, 1.0f, 1.0f);
        Vector3 posZ = Vector3.UnitZ * labelDistance;
        Vector3 zTopLeft = posZ - letterW * camRight + letterH * camUp;
        Vector3 zTopRight = posZ + letterW * camRight + letterH * camUp;
        Vector3 zBottomLeft = posZ - letterW * camRight - letterH * camUp;
        Vector3 zBottomRight = posZ + letterW * camRight - letterH * camUp;

        AddLetterLine(vertices, ref offset, zTopLeft, zTopRight, blue);
        AddLetterLine(vertices, ref offset, zTopRight, zBottomLeft, blue);
        AddLetterLine(vertices, ref offset, zBottomLeft, zBottomRight, blue);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vboLetters);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, vertices.Length * sizeof(float), vertices);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    private static void AddLetterLine(float[] buffer, ref int offset, Vector3 p0, Vector3 p1, Vector4 color)
    {
        buffer[offset++] = p0.X;
        buffer[offset++] = p0.Y;
        buffer[offset++] = p0.Z;
        buffer[offset++] = color.X;
        buffer[offset++] = color.Y;
        buffer[offset++] = color.Z;
        buffer[offset++] = color.W;

        buffer[offset++] = p1.X;
        buffer[offset++] = p1.Y;
        buffer[offset++] = p1.Z;
        buffer[offset++] = color.X;
        buffer[offset++] = color.Y;
        buffer[offset++] = color.Z;
        buffer[offset++] = color.W;
    }

    private static void ConfigureAttributes()
    {
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, StrideBytes, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, StrideBytes, 3 * sizeof(float));
    }

    public void Render(ShaderProgram shader, Camera3D camera, int viewportWidth, int viewportHeight)
    {
        if (!IsVisible || _vaoSolid == 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        int size = Math.Min(GizmoSize, Math.Min(viewportWidth, viewportHeight) / 3);
        if (size < 40)
        {
            return;
        }

        // Bottom-Right Corner in OpenGL window coordinates (origin (0,0) is bottom-left)
        int gizmoX = viewportWidth - size - Margin;
        int gizmoY = Margin;

        if (gizmoX < 0 || gizmoY < 0)
        {
            return;
        }

        // 1. Clear depth only for the gizmo area to avoid occlusion by scene geometry
        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(gizmoX, gizmoY, size, size);
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.Disable(EnableCap.ScissorTest);

        // 2. Set sub-viewport for the gizmo
        GL.Viewport(gizmoX, gizmoY, size, size);

        // 3. Compute View Matrix matching main camera orientation
        Vector3 dir = camera.Position - camera.Target;
        if (dir.LengthSquared < 1e-6f)
        {
            dir = Vector3.UnitZ;
        }
        dir = Vector3.Normalize(dir);

        const float camDistance = 5.0f;
        Vector3 eye = dir * camDistance;
        Matrix4 view = Matrix4.LookAt(eye, Vector3.Zero, camera.Up);

        // Orthographic projection so parallel cube lines remain parallel
        const float orthoExtent = 1.45f;
        Matrix4 proj = Matrix4.CreateOrthographic(orthoExtent * 2.0f, orthoExtent * 2.0f, 0.1f, 20.0f);
        Matrix4 mvp = view * proj;

        // Camera billboard vectors in world space
        Vector3 camRight = new(view.Row0.X, view.Row1.X, view.Row2.X);
        Vector3 camUp = new(view.Row0.Y, view.Row1.Y, view.Row2.Y);

        shader.Use();
        shader.SetUniformMatrix4("uMvp", ref mvp);

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        // 4. Draw Solid Triangles (Cube + Arrow Shafts + Arrowheads)
        GL.BindVertexArray(_vaoSolid);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _solidVertexCount);

        // 5. Draw Cube Edges
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.LineWidth(1.5f);
        GL.BindVertexArray(_vaoEdges);
        GL.DrawArrays(PrimitiveType.Lines, 0, _edgeVertexCount);

        // 6. Draw Billboarded Letters ('X', 'Y', 'Z')
        UpdateBillboardLetters(camRight, camUp);
        GL.LineWidth(2.0f);
        GL.BindVertexArray(_vaoLetters);
        GL.DrawArrays(PrimitiveType.Lines, 0, _letterVertexCount);

        // 7. Restore Main Viewport
        GL.Viewport(0, 0, viewportWidth, viewportHeight);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_vboSolid != 0) GL.DeleteBuffer(_vboSolid);
            if (_vaoSolid != 0) GL.DeleteVertexArray(_vaoSolid);
            if (_vboEdges != 0) GL.DeleteBuffer(_vboEdges);
            if (_vaoEdges != 0) GL.DeleteVertexArray(_vaoEdges);
            if (_vboLetters != 0) GL.DeleteBuffer(_vboLetters);
            if (_vaoLetters != 0) GL.DeleteVertexArray(_vaoLetters);
            _disposed = true;
        }
    }
}
