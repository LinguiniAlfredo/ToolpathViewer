using System.Globalization;
using AblationStudio.Core.Models;

namespace AblationStudio.Core.Parser;

public sealed class ToolpathParser
{
    public static async Task<Toolpath> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Toolpath file not found: {filePath}", filePath);
        }

        using var streamReader = new StreamReader(filePath);
        var segments = await ParseInternalAsync(streamReader, cancellationToken);
        string name = Path.GetFileName(filePath);

        return new Toolpath(name, filePath, segments);
    }

    public static Toolpath ParseText(string content, string name = "unnamed")
    {
        using var stringReader = new StringReader(content);
        var segments = ParseInternal(stringReader);
        return new Toolpath(name, string.Empty, segments);
    }

    public static Toolpath Parse(TextReader reader, string name = "unnamed")
    {
        var segments = ParseInternal(reader);
        return new Toolpath(name, string.Empty, segments);
    }

    private static List<ToolpathSegment> ParseInternal(TextReader reader)
    {
        var segments = new List<ToolpathSegment>();
        var state = new ParserState();

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            ProcessLine(line.AsSpan(), segments, ref state);
        }

        return segments;
    }

    private static async Task<List<ToolpathSegment>> ParseInternalAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var segments = new List<ToolpathSegment>();
        var state = new ParserState();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            ProcessLine(line.AsSpan(), segments, ref state);
        }

        return segments;
    }

    private static void ProcessLine(ReadOnlySpan<char> line, List<ToolpathSegment> segments, ref ParserState state)
    {
        // Strip trailing comment starting with ';'
        int commentIndex = line.IndexOf(';');
        if (commentIndex >= 0)
        {
            line = line[..commentIndex];
        }

        line = line.Trim();
        if (line.IsEmpty)
        {
            return;
        }

        // Check command prefix
        if (line.StartsWith("HCH", StringComparison.OrdinalIgnoreCase))
        {
            ParseHatchCommand(line, ref state);
            return;
        }

        if (line.StartsWith("SL", StringComparison.OrdinalIgnoreCase))
        {
            ParseStraightLineCommand(line, segments, ref state);
        }
    }

    private static void ParseHatchCommand(ReadOnlySpan<char> line, ref ParserState state)
    {
        // Format: HCH <layerId> [patternId]
        ReadOnlySpan<char> remaining = line[3..].Trim();
        int spaceIndex = remaining.IndexOfAny(' ', '\t');
        ReadOnlySpan<char> layerSpan = spaceIndex >= 0 ? remaining[..spaceIndex] : remaining;

        if (int.TryParse(layerSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int layerId))
        {
            state.CurrentLayerId = layerId;
        }
    }

    private static void ParseStraightLineCommand(
        ReadOnlySpan<char> line,
        List<ToolpathSegment> segments,
        ref ParserState state)
    {
        // Format: SL [X<float>] [Y<float>] [Z<float>] [M<int>]
        ReadOnlySpan<char> tokens = line[2..].Trim();
        float targetX = state.CurrentX;
        float targetY = state.CurrentY;
        float targetZ = state.CurrentZ;
        SegmentType? moveTypeOverride = null;
        bool hasCoordinatesInLine = false;

        while (!tokens.IsEmpty)
        {
            // Find next token delimiter
            int nextSpace = tokens.IndexOfAny(' ', '\t');
            ReadOnlySpan<char> token = nextSpace >= 0 ? tokens[..nextSpace].Trim() : tokens.Trim();
            tokens = nextSpace >= 0 ? tokens[nextSpace..].TrimStart() : ReadOnlySpan<char>.Empty;

            if (token.IsEmpty)
            {
                continue;
            }

            char prefix = char.ToUpperInvariant(token[0]);
            ReadOnlySpan<char> valueSpan = token[1..];

            switch (prefix)
            {
                case 'X':
                    if (float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                    {
                        targetX = x;
                        hasCoordinatesInLine = true;
                    }
                    break;

                case 'Y':
                    if (float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        targetY = y;
                        hasCoordinatesInLine = true;
                    }
                    break;

                case 'Z':
                    if (float.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        targetZ = z;
                        hasCoordinatesInLine = true;
                    }
                    break;

                case 'M':
                    if (int.TryParse(valueSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mCode))
                    {
                        SegmentType mode = mCode switch
                        {
                            3 => SegmentType.Cut,
                            5 => SegmentType.Rapid,
                            _ => state.CurrentMode
                        };
                        state.CurrentMode = mode;
                        moveTypeOverride = mode;
                    }
                    break;
            }
        }

        var nextPoint = new ToolpathPoint(targetX, targetY, targetZ);

        if (!state.HasPosition)
        {
            // First point establishes the starting coordinate
            state.CurrentX = targetX;
            state.CurrentY = targetY;
            state.CurrentZ = targetZ;
            state.HasPosition = true;
            return;
        }

        var startPoint = new ToolpathPoint(state.CurrentX, state.CurrentY, state.CurrentZ);
        SegmentType effectiveType = moveTypeOverride ?? state.CurrentMode;

        // If coordinates changed, record segment
        if (hasCoordinatesInLine && startPoint.DistanceTo(nextPoint) > 1e-6f)
        {
            segments.Add(new ToolpathSegment(startPoint, nextPoint, effectiveType, state.CurrentLayerId));
            state.CurrentX = targetX;
            state.CurrentY = targetY;
            state.CurrentZ = targetZ;
        }
    }

    private struct ParserState
    {
        public float CurrentX = 0f;
        public float CurrentY = 0f;
        public float CurrentZ = 0f;
        public SegmentType CurrentMode = SegmentType.Rapid;
        public int CurrentLayerId = 1;
        public bool HasPosition = false;

        public ParserState()
        {
        }
    }
}
