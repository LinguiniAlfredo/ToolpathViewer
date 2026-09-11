using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ToolpathViewer.Core.Models;
using ToolpathViewer.Core.Parser;
using ToolpathViewer.Rendering.Camera;

namespace ToolpathViewer.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private Toolpath _loadedToolpath = Toolpath.Empty;
    private string _currentFilePath = string.Empty;
    private string _statusText = "Ready. Click 'Open Toolpath' to load a file.";
    private bool _isLoading;
    private bool _showCuts = true;
    private bool _showRapids = true;
    private bool _showGrid = true;
    private bool _showAxes = true;
    private bool _showBoundingBox = true;
    private float _lineWidth = 2.0f;
    private bool _isDarkTheme = true;

    public event Action<Toolpath>? ToolpathLoaded;
    public event Action? RequestRender;
    public event Action<ViewPreset>? PresetRequested;
    public event Action? FitViewRequested;
    public event Action<bool>? ThemeChangeRequested;

    public Toolpath LoadedToolpath
    {
        get => _loadedToolpath;
        private set
        {
            if (SetProperty(ref _loadedToolpath, value))
            {
                OnPropertyChanged(nameof(HasLoadedFile));
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(FileSizeText));
                OnPropertyChanged(nameof(TotalSegments));
                OnPropertyChanged(nameof(CutSegmentsCount));
                OnPropertyChanged(nameof(RapidSegmentsCount));
                OnPropertyChanged(nameof(TotalCutLengthText));
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
        ? "3D Toolpath Viewer"
        : $"3D Toolpath Viewer - {LoadedToolpath.Name}";

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
    public int RapidSegmentsCount => LoadedToolpath.Statistics.RapidSegmentsCount;

    public string TotalCutLengthText => $"{LoadedToolpath.Statistics.TotalCutLength:F2} mm";
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

    // Commands
    public ICommand OpenFileCommand { get; }
    public ICommand ReloadCommand { get; }
    public ICommand FitViewCommand { get; }
    public ICommand SetPresetCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand LoadSampleCommand { get; }

    public MainViewModel()
    {
        OpenFileCommand = new RelayCommand(ExecuteOpenFile);
        ReloadCommand = new RelayCommand(async () => await ExecuteReloadAsync(), () => !string.IsNullOrEmpty(CurrentFilePath));
        FitViewCommand = new RelayCommand(() => FitViewRequested?.Invoke(), () => HasLoadedFile);
        SetPresetCommand = new RelayCommand<string>(ExecuteSetPreset);
        ToggleThemeCommand = new RelayCommand(() => IsDarkTheme = !IsDarkTheme);
        LoadSampleCommand = new RelayCommand<string>(async path => await ExecuteLoadSampleAsync(path));
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

            ToolpathLoaded?.Invoke(toolpath);
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
        if (Enum.TryParse(presetName, true, out ViewPreset preset))
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
}
