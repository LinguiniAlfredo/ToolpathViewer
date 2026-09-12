using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using AblationStudio.Core.Shapes;
using AblationStudio.Core.Simulation;
using AblationStudio.Rendering.Camera;

namespace AblationStudio.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private Toolpath _loadedToolpath = Toolpath.Empty;
    private string _currentFilePath = string.Empty;
    private string _statusText = "Ready. Select a shape tool to draw or click 'Open Toolpath'.";
    private bool _isLoading;
    private bool _showCuts = true;
    private bool _showHatch = true;
    private bool _showRapids = true;
    private bool _showGrid = true;
    private bool _showAxes = true;
    private bool _showBoundingBox = true;
    private float _lineWidth = 2.0f;
    private bool _isDarkTheme = true;
    private ShapeToolType _activeTool = ShapeToolType.Select;

    private ToolpathSimulator? _simulator;
    private float _simulationFeedrate = 50.0f; // mm/s
    private long _simulationLastTimestamp;
    private bool _isSimulationHooked;

    public ShapeDocument CustomShapes { get; } = new();

    public ShapeToolType ActiveTool
    {
        get => _activeTool;
        set => SetProperty(ref _activeTool, value);
    }

    public ToolpathShape? SelectedShape => CustomShapes.SelectedShape;
    public bool HasSelectedShape => SelectedShape is not null;

    public event Action<Toolpath, bool>? ToolpathLoaded;
    public event Action? RequestRender;
    public event Action<ViewPreset>? PresetRequested;
    public event Action? FitViewRequested;
    public event Action<bool>? ThemeChangeRequested;
    public event Action<ToolpathPoint?, SegmentType, float>? SimulationUpdated;

    public Toolpath LoadedToolpath
    {
        get => _loadedToolpath;
        private set
        {
            if (SetProperty(ref _loadedToolpath, value))
            {
                UnhookSimulationLoop();
                _simulator = new ToolpathSimulator(value)
                {
                    Feedrate = _simulationFeedrate
                };
                UpdateSimulationProperties();
                EmitSimulationPosition();

                OnPropertyChanged(nameof(HasLoadedFile));
                OnPropertyChanged(nameof(HasSimulation));
                (TogglePlaySimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (FitViewCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportToolpathCommand as RelayCommand)?.RaiseCanExecuteChanged();
                CommandManager.InvalidateRequerySuggested();

                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(FileSizeText));
                OnPropertyChanged(nameof(TotalSegments));
                OnPropertyChanged(nameof(CutSegmentsCount));
                OnPropertyChanged(nameof(HatchSegmentsCount));
                OnPropertyChanged(nameof(RapidSegmentsCount));
                OnPropertyChanged(nameof(TotalCutLengthText));
                OnPropertyChanged(nameof(TotalHatchLengthText));
                OnPropertyChanged(nameof(TotalLaserLengthText));
                OnPropertyChanged(nameof(TotalRapidLengthText));
                OnPropertyChanged(nameof(TotalLengthText));
                OnPropertyChanged(nameof(DimensionsText));
                OnPropertyChanged(nameof(BoundsXText));
                OnPropertyChanged(nameof(BoundsYText));
                OnPropertyChanged(nameof(BoundsZText));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public string CurrentFilePath
    {
        get => _currentFilePath;
        private set => SetProperty(ref _currentFilePath, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasLoadedFile => LoadedToolpath.Segments.Count > 0;

    public string WindowTitle => string.IsNullOrEmpty(LoadedToolpath.Name)
        ? "Ablation Studio"
        : $"Ablation Studio - {LoadedToolpath.Name}";

    public string FileName => string.IsNullOrEmpty(LoadedToolpath.Name) ? "None" : LoadedToolpath.Name;

    public string FileSizeText
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentFilePath) || !File.Exists(CurrentFilePath))
            {
                return "-";
            }

            var info = new FileInfo(CurrentFilePath);
            return $"{info.Length} bytes";
        }
    }

    public int TotalSegments => LoadedToolpath.Statistics.TotalSegments;
    public int CutSegmentsCount => LoadedToolpath.Statistics.CutSegmentsCount;
    public int HatchSegmentsCount => LoadedToolpath.Statistics.HatchSegmentsCount;
    public int RapidSegmentsCount => LoadedToolpath.Statistics.RapidSegmentsCount;

    public string TotalCutLengthText => $"{LoadedToolpath.Statistics.TotalCutLength:F2} mm";
    public string TotalHatchLengthText => $"{LoadedToolpath.Statistics.TotalHatchLength:F2} mm";
    public string TotalLaserLengthText => $"{LoadedToolpath.Statistics.TotalLaserLength:F2} mm";
    public string TotalRapidLengthText => $"{LoadedToolpath.Statistics.TotalRapidLength:F2} mm";
    public string TotalLengthText => $"{LoadedToolpath.Statistics.TotalLength:F2} mm";

    public string DimensionsText
    {
        get
        {
            BoundingBox3D b = LoadedToolpath.BoundingBox;
            return b.IsEmpty ? "-" : $"{b.SizeX:F2} × {b.SizeY:F2} × {b.SizeZ:F2} mm";
        }
    }

    public string BoundsXText
    {
        get
        {
            BoundingBox3D b = LoadedToolpath.BoundingBox;
            return b.IsEmpty ? "-" : $"[{b.MinX:F2}, {b.MaxX:F2}]";
        }
    }

    public string BoundsYText
    {
        get
        {
            BoundingBox3D b = LoadedToolpath.BoundingBox;
            return b.IsEmpty ? "-" : $"[{b.MinY:F2}, {b.MaxY:F2}]";
        }
    }

    public string BoundsZText
    {
        get
        {
            BoundingBox3D b = LoadedToolpath.BoundingBox;
            return b.IsEmpty ? "-" : $"[{b.MinZ:F2}, {b.MaxZ:F2}]";
        }
    }

    public bool ShowCuts
    {
        get => _showCuts;
        set
        {
            if (SetProperty(ref _showCuts, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool ShowHatch
    {
        get => _showHatch;
        set
        {
            if (SetProperty(ref _showHatch, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool ShowRapids
    {
        get => _showRapids;
        set
        {
            if (SetProperty(ref _showRapids, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            if (SetProperty(ref _showGrid, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool ShowAxes
    {
        get => _showAxes;
        set
        {
            if (SetProperty(ref _showAxes, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool ShowBoundingBox
    {
        get => _showBoundingBox;
        set
        {
            if (SetProperty(ref _showBoundingBox, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public float LineWidth
    {
        get => _lineWidth;
        set
        {
            if (SetProperty(ref _lineWidth, value))
            {
                RequestRender?.Invoke();
            }
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
            {
                ThemeChangeRequested?.Invoke(value);
            }
        }
    }

    // Simulation Properties
    public bool HasSimulation => LoadedToolpath.Segments.Count > 0;
    public bool IsSimulationPlaying => _simulator?.State == SimulationState.Playing;
    public bool IsSimulationActive => _simulator is not null && _simulator.State != SimulationState.Stopped;

    public float SimulationFeedrate
    {
        get => _simulationFeedrate;
        set
        {
            if (SetProperty(ref _simulationFeedrate, MathF.Max(1.0f, value)))
            {
                if (_simulator is not null)
                {
                    _simulator.Feedrate = _simulationFeedrate;
                }
                OnPropertyChanged(nameof(SimulationFeedrateText));
            }
        }
    }

    public string SimulationFeedrateText =>
        $"{SimulationFeedrate:F0} mm/s ({(SimulationFeedrate * 60f):N0} mm/min)";

    public float SimulationProgress
    {
        get => _simulator?.ProgressFraction ?? 0f;
        set
        {
            if (_simulator is not null)
            {
                _simulator.SeekToFraction(value);
                UpdateSimulationProperties();
                EmitSimulationPosition();
            }
        }
    }

    public string SimulationPositionText => _simulator is not null && _simulator.State != SimulationState.Stopped
        ? $"X: {_simulator.CurrentPosition.X:F2}   Y: {_simulator.CurrentPosition.Y:F2}   Z: {_simulator.CurrentPosition.Z:F2}"
        : "-";

    public string SimulationProgressText => _simulator is not null && _simulator.TotalDistance > 0f
        ? $"{_simulator.CurrentDistance:F1} / {_simulator.TotalDistance:F1} mm ({(_simulator.ProgressFraction * 100f):F0}%)"
        : "-";

    public string SimulationStateText => _simulator?.State switch
    {
        SimulationState.Playing => _simulator.CurrentSegmentType switch
        {
            SegmentType.Cut => "Laser ON (Profile - M03)",
            SegmentType.Hatch => "Laser ON (Hatch - M03)",
            _ => "Rapid Move (M05)"
        },
        SimulationState.Paused => _simulator.CurrentSegmentType switch
        {
            SegmentType.Cut => "Paused (Profile - M03)",
            SegmentType.Hatch => "Paused (Hatch - M03)",
            _ => "Paused (Rapid - M05)"
        },
        SimulationState.Completed => "Completed",
        _ => "Idle"
    };

    public SegmentType SimulationSegmentType => _simulator?.CurrentSegmentType ?? SegmentType.Rapid;

    // Commands
    public ICommand NewFileCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand FitViewCommand { get; }
    public ICommand SetPresetCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand LoadSampleCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand TogglePlaySimulationCommand { get; }
    public ICommand StopSimulationCommand { get; }

    public ICommand SelectToolCommand { get; }
    public ICommand DeleteSelectedShapeCommand { get; }
    public ICommand ClearAllShapesCommand { get; }
    public ICommand DeselectShapeCommand { get; }
    public ICommand DuplicateSelectedShapeCommand { get; }
    public ICommand ExportToolpathCommand { get; }

    public MainViewModel()
    {
        NewFileCommand = new RelayCommand(ExecuteNewFile);
        OpenFileCommand = new RelayCommand(ExecuteOpenFile);
        ReloadCommand = new RelayCommand(async () => await ExecuteReloadAsync(), () => !string.IsNullOrEmpty(CurrentFilePath));
        FitViewCommand = new RelayCommand(() => FitViewRequested?.Invoke(), () => HasLoadedFile);
        SetPresetCommand = new RelayCommand<string>(ExecuteSetPreset);
        ToggleThemeCommand = new RelayCommand(() => IsDarkTheme = !IsDarkTheme);
        LoadSampleCommand = new RelayCommand<string>(async path => await ExecuteLoadSampleAsync(path));
        ExitCommand = new RelayCommand(ExecuteExit);

        CustomShapes.SelectionChanged += _ =>
        {
            OnPropertyChanged(nameof(SelectedShape));
            OnPropertyChanged(nameof(HasSelectedShape));
            RequestRender?.Invoke();
        };

        CustomShapes.DocumentChanged += OnCustomShapesDocumentChanged;

        SelectToolCommand = new RelayCommand<ShapeToolType>(tool => ActiveTool = tool);
        DeleteSelectedShapeCommand = new RelayCommand(ExecuteDeleteSelectedShape, () => HasSelectedShape);
        ClearAllShapesCommand = new RelayCommand(() => CustomShapes.Clear(), () => CustomShapes.Shapes.Count > 0);
        DeselectShapeCommand = new RelayCommand(() => CustomShapes.SelectedShape = null, () => HasSelectedShape);
        DuplicateSelectedShapeCommand = new RelayCommand(ExecuteDuplicateSelectedShape, () => HasSelectedShape);
        ExportToolpathCommand = new RelayCommand(ExecuteExportToolpath, () => CustomShapes.Shapes.Count > 0 || HasLoadedFile);

        TogglePlaySimulationCommand = new RelayCommand(ExecuteTogglePlaySimulation, () => HasSimulation);
        StopSimulationCommand = new RelayCommand(ExecuteStopSimulation, () => HasSimulation);
    }

    public void ExecuteTogglePlaySimulation()
    {
        if (_simulator is null || !HasSimulation)
        {
            return;
        }

        if (_simulator.State == SimulationState.Playing)
        {
            _simulator.Pause();
            UnhookSimulationLoop();
        }
        else
        {
            _simulator.Feedrate = _simulationFeedrate;
            _simulator.Play();
            HookSimulationLoop();
        }

        UpdateSimulationProperties();
        EmitSimulationPosition();
    }

    public void ExecuteStopSimulation()
    {
        if (_simulator is null)
        {
            return;
        }

        _simulator.Stop();
        UnhookSimulationLoop();
        UpdateSimulationProperties();
        EmitSimulationPosition();
    }

    private void HookSimulationLoop()
    {
        if (!_isSimulationHooked)
        {
            _simulationLastTimestamp = Stopwatch.GetTimestamp();
            System.Windows.Media.CompositionTarget.Rendering += OnSimulationRenderTick;
            _isSimulationHooked = true;
        }
    }

    private void UnhookSimulationLoop()
    {
        if (_isSimulationHooked)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnSimulationRenderTick;
            _isSimulationHooked = false;
        }
    }

    private void OnSimulationRenderTick(object? sender, EventArgs e)
    {
        if (_simulator is null || _simulator.State != SimulationState.Playing)
        {
            UnhookSimulationLoop();
            UpdateSimulationProperties();
            return;
        }

        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = (double)(now - _simulationLastTimestamp) / Stopwatch.Frequency;
        _simulationLastTimestamp = now;

        elapsedSeconds = Math.Clamp(elapsedSeconds, 0.0, 0.1);
        _simulator.Update((float)elapsedSeconds);

        UpdateSimulationProperties();
        EmitSimulationPosition();

        if (_simulator.State != SimulationState.Playing)
        {
            UnhookSimulationLoop();
        }
    }

    private void EmitSimulationPosition()
    {
        float extent = LoadedToolpath.BoundingBox.IsEmpty ? 10f : LoadedToolpath.BoundingBox.MaxExtent;

        if (_simulator is null || _simulator.State == SimulationState.Stopped)
        {
            SimulationUpdated?.Invoke(null, SegmentType.Rapid, extent);
        }
        else
        {
            SimulationUpdated?.Invoke(_simulator.CurrentPosition, _simulator.CurrentSegmentType, extent);
        }
    }

    private void UpdateSimulationProperties()
    {
        OnPropertyChanged(nameof(IsSimulationPlaying));
        OnPropertyChanged(nameof(IsSimulationActive));
        OnPropertyChanged(nameof(SimulationProgress));
        OnPropertyChanged(nameof(SimulationPositionText));
        OnPropertyChanged(nameof(SimulationProgressText));
        OnPropertyChanged(nameof(SimulationStateText));
        OnPropertyChanged(nameof(SimulationSegmentType));
    }

    private void ExecuteNewFile()
    {
        CustomShapes.Clear();
        LoadedToolpath = Toolpath.Empty;
        CurrentFilePath = string.Empty;
        ActiveTool = ShapeToolType.Select;
        ToolpathLoaded?.Invoke(LoadedToolpath, false);
        StatusText = "New canvas ready. Select a shape tool to begin drawing.";
        RequestRender?.Invoke();
        FitViewRequested?.Invoke();
    }

    private static void ExecuteExit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void ExecuteOpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Toolpath File",
            Filter = "Toolpath Files (*.h;*.txt)|*.h;*.txt|All Files (*.*)|*.*",
            InitialDirectory = @"c:\Users\m_del\Source\vibe_test\example_toolpaths"
        };

        if (dialog.ShowDialog() == true)
        {
            _ = LoadFileAsync(dialog.FileName);
        }
    }

    public async Task LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusText = $"File not found: {filePath}";
            return;
        }

        try
        {
            IsLoading = true;
            StatusText = $"Loading {Path.GetFileName(filePath)}...";

            Toolpath toolpath = await ToolpathParser.ParseFileAsync(filePath);

            CurrentFilePath = filePath;
            LoadedToolpath = toolpath;

            ToolpathLoaded?.Invoke(toolpath, true);
            StatusText = $"Loaded {toolpath.Name}: {toolpath.Segments.Count} segments ({toolpath.Statistics.CutSegmentsCount} cut, {toolpath.Statistics.RapidSegmentsCount} rapid).";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading file: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteReloadAsync()
    {
        if (!string.IsNullOrEmpty(CurrentFilePath))
        {
            await LoadFileAsync(CurrentFilePath);
        }
    }

    private void ExecuteSetPreset(string? presetName)
    {
        if (Enum.TryParse<ViewPreset>(presetName, out ViewPreset preset))
        {
            PresetRequested?.Invoke(preset);
        }
    }

    private async Task ExecuteLoadSampleAsync(string? sampleFileName)
    {
        if (string.IsNullOrWhiteSpace(sampleFileName))
        {
            return;
        }

        string fullPath = Path.Combine(@"c:\Users\m_del\Source\vibe_test\example_toolpaths", sampleFileName);
        if (File.Exists(fullPath))
        {
            await LoadFileAsync(fullPath);
        }
    }

    private void OnCustomShapesDocumentChanged()
    {
        if (CustomShapes.Shapes.Count > 0)
        {
            LoadedToolpath = CustomShapes.CompileToolpath("CustomShapes.h");
            CurrentFilePath = string.Empty;
            ToolpathLoaded?.Invoke(LoadedToolpath, false);
            StatusText = $"Custom toolpath: {CustomShapes.Shapes.Count} shapes, {LoadedToolpath.Segments.Count} segments ({LoadedToolpath.Statistics.TotalCutLength:F2} mm cut).";
        }
        else
        {
            LoadedToolpath = Toolpath.Empty;
            CurrentFilePath = string.Empty;
            ToolpathLoaded?.Invoke(LoadedToolpath, false);
            StatusText = "Ready. Select a shape tool to draw or click 'Open Toolpath'.";
        }
        RequestRender?.Invoke();
    }

    private void ExecuteDeleteSelectedShape()
    {
        if (SelectedShape is not null)
        {
            CustomShapes.RemoveShape(SelectedShape);
        }
    }

    private void ExecuteDuplicateSelectedShape()
    {
        if (SelectedShape is not null)
        {
            ToolpathShape copy = SelectedShape.Clone();
            copy.Translate(2.0f, 2.0f, 0f);
            CustomShapes.AddShape(copy);
        }
    }

    private void ExecuteExportToolpath()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Toolpath File",
            Filter = "Toolpath Files (*.h)|*.h|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = string.IsNullOrEmpty(LoadedToolpath.Name) ? "CustomShapes.h" : LoadedToolpath.Name
        };

        if (dialog.ShowDialog() == true)
        {
            string hCode = CustomShapes.Shapes.Count > 0
                ? CustomShapes.ExportToHCode(Path.GetFileName(dialog.FileName))
                : ExportToolpathToH(LoadedToolpath, Path.GetFileName(dialog.FileName));

            File.WriteAllText(dialog.FileName, hCode);
            StatusText = $"Exported toolpath to {Path.GetFileName(dialog.FileName)}.";
        }
    }

    private static string ExportToolpathToH(Toolpath toolpath, string name)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"; Exported toolpath - {name}");
        sb.AppendLine("; Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");

        if (toolpath.Segments.Count == 0)
        {
            sb.AppendLine("HCH 1 1 ;Layer 1");
            sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");
            return sb.ToString();
        }

        int firstLayer = toolpath.Segments[0].LayerId;
        sb.AppendLine($"HCH {firstLayer} 1 ;Layer {firstLayer}");
        sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");

        var origin = new ToolpathPoint(0f, 0f, 0f);
        ToolpathPoint firstStart = toolpath.Segments[0].Start;

        if (firstStart.DistanceTo(origin) > 0.001f)
        {
            sb.AppendLine($"SL X{firstStart.X:F4} Y{firstStart.Y:F4} Z{firstStart.Z:F4} M05");
        }

        int currentLayer = firstLayer;
        foreach (ToolpathSegment seg in toolpath.Segments)
        {
            if (seg.LayerId != currentLayer)
            {
                currentLayer = seg.LayerId;
                sb.AppendLine($"HCH {currentLayer} 1 ;Layer {currentLayer}");
            }

            string cmd = (seg.Type == SegmentType.Cut || seg.Type == SegmentType.Hatch) ? "M03" : "M05";
            sb.AppendLine($"SL X{seg.End.X:F4} Y{seg.End.Y:F4} Z{seg.End.Z:F4} {cmd}");
        }

        ToolpathSegment lastSeg = toolpath.Segments[^1];
        if (lastSeg.Type != SegmentType.Rapid || lastSeg.End.DistanceTo(origin) > 0.001f)
        {
            sb.AppendLine("SL X0.0000 Y0.0000 Z0.0000 M05");
        }

        return sb.ToString();
    }
}
