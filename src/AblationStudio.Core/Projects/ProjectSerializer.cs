using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AblationStudio.Core.Projects;

public static class ProjectSerializer
{
    private const string MetadataBeginMarker = "; ABLATION_STUDIO_PROJECT_BEGIN";
    private const string MetadataEndMarker = "; ABLATION_STUDIO_PROJECT_END";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private static readonly JsonSerializerOptions CompactSerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string SerializeToJson(ToolpathProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.ModifiedAtUtc = DateTime.UtcNow;
        return JsonSerializer.Serialize(project, SerializerOptions);
    }

    public static ToolpathProject DeserializeFromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var project = JsonSerializer.Deserialize<ToolpathProject>(json, SerializerOptions);
        return project ?? throw new InvalidOperationException("Deserialization returned null.");
    }

    public static async Task SaveProjectAsync(
        ToolpathProject project,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        project.ModifiedAtUtc = DateTime.UtcNow;
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(project, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8, cancellationToken);
    }

    public static async Task<ToolpathProject> LoadProjectAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Project file not found: {filePath}", filePath);
        }

        string json = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return DeserializeFromJson(json);
    }

    public static string EmbedMetadataInHCode(string hCode, ToolpathProject project)
    {
        ArgumentNullException.ThrowIfNull(hCode);
        ArgumentNullException.ThrowIfNull(project);

        string compactJson = JsonSerializer.Serialize(project, CompactSerializerOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(compactJson);
        string base64 = Convert.ToBase64String(bytes);

        var sb = new StringBuilder();
        sb.AppendLine(MetadataBeginMarker);
        // Chunk base64 in 76-character comment lines for cleanliness
        const int chunkSize = 76;
        for (int i = 0; i < base64.Length; i += chunkSize)
        {
            int length = Math.Min(chunkSize, base64.Length - i);
            sb.Append("; ");
            sb.AppendLine(base64.Substring(i, length));
        }
        sb.AppendLine(MetadataEndMarker);
        sb.Append(hCode);

        return sb.ToString();
    }

    public static bool TryExtractMetadataFromHCode(string hCode, [NotNullWhen(true)] out ToolpathProject? project)
    {
        project = null;
        if (string.IsNullOrWhiteSpace(hCode))
        {
            return false;
        }

        int beginIdx = hCode.IndexOf(MetadataBeginMarker, StringComparison.OrdinalIgnoreCase);
        if (beginIdx < 0)
        {
            return false;
        }

        int endIdx = hCode.IndexOf(MetadataEndMarker, beginIdx, StringComparison.OrdinalIgnoreCase);
        if (endIdx < 0)
        {
            return false;
        }

        int contentStart = beginIdx + MetadataBeginMarker.Length;
        string block = hCode.Substring(contentStart, endIdx - contentStart);

        var base64Builder = new StringBuilder();
        using var reader = new StringReader(block);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith(';'))
            {
                trimmed = trimmed[1..].Trim();
            }

            if (!string.IsNullOrEmpty(trimmed))
            {
                base64Builder.Append(trimmed);
            }
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(base64Builder.ToString());
            string json = Encoding.UTF8.GetString(bytes);
            project = DeserializeFromJson(json);
            return project is not null;
        }
        catch
        {
            project = null;
            return false;
        }
    }
}
