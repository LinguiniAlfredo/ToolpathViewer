using System.Globalization;
using AblationStudio.Core.Models;

namespace AblationStudio.Core.Parser;

public sealed class ClsToolpathParser
{
    private const int CreoColorRapid = 186;
    private const int CreoColorCut = 31;
    private const float DefaultMaxAngleStepRad = 5.0f * (MathF.PI / 180.0f); // 5 degrees
    private const int MinArcSegments = 4;
    private const int MaxArcSegments = 128;
    private const float PositionEpsilon = 1e-6f;

    public static async Task<Toolpath> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Toolpath file not found: {filePath}", filePath);
        }

        using var streamReader = new StreamReader(filePath);
        string defaultName = Path.GetFileName(filePath);
        (var segments, string? extractedName) = await ParseInternalAsync(streamReader, cancellationToken);
        string finalName = !string.IsNullOrWhiteSpace(extractedName) ? extractedName : defaultName;

        return new Toolpath(finalName, filePath, segments);
    }

    public static Toolpath ParseText(string content, string name = "unnamed")
    {
        using var stringReader = new StringReader(content);
        (var segments, string? extractedName) = ParseInternal(stringReader);
        string finalName = name != "unnamed" ? name : (!string.IsNullOrWhiteSpace(extractedName) ? extractedName : name);
        return new Toolpath(finalName, string.Empty, segments);
    }

    public static Toolpath Parse(TextReader reader, string name = "unnamed")
    {
        (var segments, string? extractedName) = ParseInternal(reader);
        string finalName = name != "unnamed" ? name : (!string.IsNullOrWhiteSpace(extractedName) ? extractedName : name);
        return new Toolpath(finalName, string.Empty, segments);
    }

    public static bool IsClsContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        using var reader = new StringReader(content);
        string? line;
        int checkedLines = 0;

        while ((line = reader.ReadLine()) is not null && checkedLines < 25)
        {
            ReadOnlySpan<char> span = line.Trim();
            if (span.IsEmpty)
            {
                continue;
            }

            checkedLines++;

            if (span.StartsWith("$$", StringComparison.Ordinal) ||
                span.StartsWith("TOOL PATH/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("TLDATA/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("MSYS/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("PAINT/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("GOTO/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("CIRCLE/", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("LOAD/WIRE", StringComparison.OrdinalIgnoreCase) ||
                span.StartsWith("SET/UPPER", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static (List<ToolpathSegment> Segments, string? ToolpathName) ParseInternal(TextReader reader)
    {
        var segments = new List<ToolpathSegment>();
        var state = new ParserState();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ProcessLine(line.AsSpan(), segments, ref state);
        }

        return (segments, state.ToolpathName);
    }

    private static async Task<(List<ToolpathSegment> Segments, string? ToolpathName)> ParseInternalAsync(
        TextReader reader,
        CancellationToken cancellationToken)
    {
        var segments = new List<ToolpathSegment>();
        var state = new ParserState();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            ProcessLine(line.AsSpan(), segments, ref state);
        }

        return (segments, state.ToolpathName);
    }

    private static void ProcessLine(ReadOnlySpan<char> line, List<ToolpathSegment> segments, ref ParserState state)
    {
        // Strip trailing comment starting with '$$'
        int commentIndex = line.IndexOf("$$", StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            line = line[..commentIndex];
        }

        line = line.Trim();
        if (line.IsEmpty)
        {
            return;
        }

        int slashIndex = line.IndexOf('/');
        ReadOnlySpan<char> command;
        ReadOnlySpan<char> args;

        if (slashIndex >= 0)
        {
            command = line[..slashIndex].Trim();
            args = line[(slashIndex + 1)..].Trim();
        }
        else
        {
            command = line.Trim();
            args = ReadOnlySpan<char>.Empty;
        }

        if (command.Equals("TOOL PATH", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.IsEmpty && state.ToolpathName is null)
            {
                state.ToolpathName = args.ToString();
            }
            return;
        }

        if (command.Equals("PAINT", StringComparison.OrdinalIgnoreCase))
        {
            ParsePaintCommand(args, ref state);
            return;
        }

        if (command.Equals("RAPID", StringComparison.OrdinalIgnoreCase))
        {
            state.CurrentMode = SegmentType.Rapid;
            return;
        }

        if (command.Equals("FEDRAT", StringComparison.OrdinalIgnoreCase))
        {
            state.CurrentMode = SegmentType.Cut;
            return;
        }

        if (command.Equals("CIRCLE", StringComparison.OrdinalIgnoreCase))
        {
            ParseCircleCommand(args, ref state);
            return;
        }

        if (command.Equals("GOTO", StringComparison.OrdinalIgnoreCase))
        {
            ParseGotoCommand(args, segments, ref state);
        }
    }

    private static void ParsePaintCommand(ReadOnlySpan<char> args, ref ParserState state)
    {
        // Format: COLOR,<number> or COLOR, <number>
        if (args.StartsWith("COLOR", StringComparison.OrdinalIgnoreCase))
        {
            ReadOnlySpan<char> remaining = args[5..].TrimStart();
            if (remaining.StartsWith(","))
            {
                remaining = remaining[1..].Trim();
                if (int.TryParse(remaining, NumberStyles.Integer, CultureInfo.InvariantCulture, out int colorCode))
                {
                    state.CurrentMode = colorCode switch
                    {
                        CreoColorRapid => SegmentType.Rapid,
                        CreoColorCut => SegmentType.Cut,
                        _ => state.CurrentMode
                    };
                }
            }
        }
    }

    private static void ParseCircleCommand(ReadOnlySpan<char> args, ref ParserState state)
    {
        // Format: CIRCLE/xc, yc, zc, i, j, k, r [, tol, max_step, ...]
        Span<float> values = stackalloc float[12];
        if (!TryParseFloats(args, values, out int count) || count < 7)
        {
            return;
        }

        state.HasPendingCircle = true;
        state.CircleCenterX = values[0];
        state.CircleCenterY = values[1];
        state.CircleCenterZ = values[2];
        state.CircleNormalI = values[3];
        state.CircleNormalJ = values[4];
        state.CircleNormalK = values[5];
        state.CircleRadius = values[6];
        state.CircleTolerance = count >= 8 ? values[7] : 0f;
    }

    private static void ParseGotoCommand(
        ReadOnlySpan<char> args,
        List<ToolpathSegment> segments,
        ref ParserState state)
    {
        // Format: GOTO/x, y, z [, i, j, k]
        Span<float> values = stackalloc float[6];
        if (!TryParseFloats(args, values, out int count) || count < 3)
        {
            return;
        }

        float targetX = values[0];
        float targetY = values[1];
        float targetZ = values[2];
        var targetPoint = new ToolpathPoint(targetX, targetY, targetZ);

        if (!state.HasPosition)
        {
            state.CurrentX = targetX;
            state.CurrentY = targetY;
            state.CurrentZ = targetZ;
            state.HasPosition = true;
            state.HasPendingCircle = false;
            return;
        }

        var startPoint = new ToolpathPoint(state.CurrentX, state.CurrentY, state.CurrentZ);

        if (state.HasPendingCircle)
        {
            TessellateArc(startPoint, targetPoint, segments, ref state);
            state.HasPendingCircle = false;
        }
        else
        {
            if (startPoint.DistanceTo(targetPoint) > PositionEpsilon)
            {
                segments.Add(new ToolpathSegment(startPoint, targetPoint, state.CurrentMode, state.CurrentLayerId));
            }
        }

        state.CurrentX = targetX;
        state.CurrentY = targetY;
        state.CurrentZ = targetZ;
    }

    private static void TessellateArc(
        ToolpathPoint start,
        ToolpathPoint end,
        List<ToolpathSegment> segments,
        ref ParserState state)
    {
        var center = new ToolpathPoint(state.CircleCenterX, state.CircleCenterY, state.CircleCenterZ);
        float nx = state.CircleNormalI;
        float ny = state.CircleNormalJ;
        float nz = state.CircleNormalK;

        float normalLen = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        if (normalLen > 1e-6f)
        {
            nx /= normalLen;
            ny /= normalLen;
            nz /= normalLen;
        }
        else
        {
            nx = 0f;
            ny = 0f;
            nz = 1f;
        }

        // Vector from center to start point
        float v0x = start.X - center.X;
        float v0y = start.Y - center.Y;
        float v0z = start.Z - center.Z;
        float r0 = MathF.Sqrt(v0x * v0x + v0y * v0y + v0z * v0z);

        if (r0 < 1e-6f)
        {
            // Degenerate circle center equals start point, fallback to straight line
            if (start.DistanceTo(end) > PositionEpsilon)
            {
                segments.Add(new ToolpathSegment(start, end, state.CurrentMode, state.CurrentLayerId));
            }
            return;
        }

        float radius = state.CircleRadius > 1e-6f ? state.CircleRadius : r0;

        // Planar orthonormal basis: e1 = v0 / r0, e2 = normal x e1
        float e1x = v0x / r0;
        float e1y = v0y / r0;
        float e1z = v0z / r0;

        float e2x = ny * e1z - nz * e1y;
        float e2y = nz * e1x - nx * e1z;
        float e2z = nx * e1y - ny * e1x;
        float e2Len = MathF.Sqrt(e2x * e2x + e2y * e2y + e2z * e2z);

        if (e2Len > 1e-6f)
        {
            e2x /= e2Len;
            e2y /= e2Len;
            e2z /= e2Len;
        }

        // Vector from center to end point
        float v1x = end.X - center.X;
        float v1y = end.Y - center.Y;
        float v1z = end.Z - center.Z;

        // Project v1 onto the e1 and e2 basis
        float proj1 = v1x * e1x + v1y * e1y + v1z * e1z;
        float proj2 = v1x * e2x + v1y * e2y + v1z * e2z;

        float phi = MathF.Atan2(proj2, proj1);

        if (MathF.Abs(phi) < 1e-6f)
        {
            if (start.DistanceTo(end) < PositionEpsilon)
            {
                // Full circle
                phi = -2.0f * MathF.PI;
            }
            else
            {
                segments.Add(new ToolpathSegment(start, end, state.CurrentMode, state.CurrentLayerId));
                return;
            }
        }

        // Compute step count based on chord tolerance or max angular step
        float angleStep = DefaultMaxAngleStepRad;
        if (state.CircleTolerance > 1e-6f && radius > state.CircleTolerance)
        {
            float cosHalf = Math.Clamp(1.0f - (state.CircleTolerance / radius), -1.0f, 1.0f);
            float tolAngle = 2.0f * MathF.Acos(cosHalf);
            if (tolAngle > 1e-4f)
            {
                angleStep = MathF.Min(angleStep, tolAngle);
            }
        }

        int stepCount = Math.Clamp((int)MathF.Ceiling(MathF.Abs(phi) / angleStep), MinArcSegments, MaxArcSegments);

        ToolpathPoint prevPoint = start;
        for (int k = 1; k <= stepCount; k++)
        {
            ToolpathPoint currPoint;
            if (k == stepCount)
            {
                currPoint = end;
            }
            else
            {
                float frac = (float)k / stepCount;
                float theta = phi * frac;
                float cosT = MathF.Cos(theta);
                float sinT = MathF.Sin(theta);

                float px = center.X + radius * (e1x * cosT + e2x * sinT);
                float py = center.Y + radius * (e1y * cosT + e2y * sinT);
                float pz = start.Z + (end.Z - start.Z) * frac;
                currPoint = new ToolpathPoint(px, py, pz);
            }

            if (prevPoint.DistanceTo(currPoint) > PositionEpsilon)
            {
                segments.Add(new ToolpathSegment(prevPoint, currPoint, state.CurrentMode, state.CurrentLayerId));
                prevPoint = currPoint;
            }
        }
    }

    private static bool TryParseFloats(ReadOnlySpan<char> span, Span<float> values, out int count)
    {
        count = 0;
        while (!span.IsEmpty && count < values.Length)
        {
            int commaIndex = span.IndexOf(',');
            ReadOnlySpan<char> token = commaIndex >= 0 ? span[..commaIndex].Trim() : span.Trim();
            span = commaIndex >= 0 ? span[(commaIndex + 1)..] : ReadOnlySpan<char>.Empty;

            if (token.IsEmpty)
            {
                continue;
            }

            if (float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                values[count++] = val;
            }
            else
            {
                return false;
            }
        }

        return count > 0;
    }

    private struct ParserState
    {
        public float CurrentX = 0f;
        public float CurrentY = 0f;
        public float CurrentZ = 0f;
        public SegmentType CurrentMode = SegmentType.Rapid;
        public int CurrentLayerId = 1;
        public bool HasPosition = false;
        public string? ToolpathName = null;

        public bool HasPendingCircle = false;
        public float CircleCenterX = 0f;
        public float CircleCenterY = 0f;
        public float CircleCenterZ = 0f;
        public float CircleNormalI = 0f;
        public float CircleNormalJ = 0f;
        public float CircleNormalK = 1f;
        public float CircleRadius = 0f;
        public float CircleTolerance = 0f;

        public ParserState()
        {
        }
    }
}
