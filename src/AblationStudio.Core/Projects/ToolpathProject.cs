using AblationStudio.Core.Shapes;

namespace AblationStudio.Core.Projects;

public sealed class ToolpathProject
{
    public const string CurrentSchemaVersion = "1.0";
    public const string ProjectExtension = ".abs";
    public const string ProjectFileFilter = "Ablation Studio Project (*.abs)|*.abs|All Files (*.*)|*.*";
    public const string MachineFileFilter = "Toolpath File (*.h)|*.h|All Files (*.*)|*.*";

    public string SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string Name { get; set; } = "Untitled";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public ProjectProcessSettings Settings { get; set; } = new();
    public List<ShapeDto> Shapes { get; set; } = [];

    public static ToolpathProject FromShapeDocument(
        ShapeDocument document,
        string projectName = "Untitled",
        ProjectProcessSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var project = new ToolpathProject
        {
            Name = string.IsNullOrWhiteSpace(projectName) ? "Untitled" : projectName,
            Settings = settings ?? new ProjectProcessSettings(),
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow
        };

        foreach (ToolpathShape shape in document.Shapes)
        {
            project.Shapes.Add(ShapeDto.FromShape(shape));
        }

        return project;
    }

    public void ApplyTo(ShapeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Clear();
        foreach (ShapeDto dto in Shapes)
        {
            ToolpathShape shape = dto.ToShape();
            document.AddShape(shape);
        }
    }
}

public sealed class ProjectProcessSettings
{
    public string Units { get; set; } = "mm";
    public float DefaultFeedrate { get; set; } = 50.0f; // mm/s
    public int DefaultLayerId { get; set; } = 1;
    public bool ReturnHomeAtEnd { get; set; } = true;
}
