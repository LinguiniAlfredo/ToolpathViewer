using System.IO;
using System.Windows;
using ToolpathViewer.App.ViewModels;
using ToolpathViewer.Rendering.Camera;
using Wpf.Ui.Appearance;

namespace ToolpathViewer.App;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ToolpathLoaded += OnToolpathLoaded;
            vm.RequestRender += OnRequestRender;
            vm.FitViewRequested += OnFitViewRequested;
            vm.PresetRequested += OnPresetRequested;
            vm.ThemeChangeRequested += OnThemeChangeRequested;

            UpdateViewportToggles(vm);

            // Auto-load example Box4mm.h if available
            string defaultSample = @"c:\Users\m_del\Source\vibe_test\example_toolpaths\Box4mm.h";
            if (File.Exists(defaultSample))
            {
                _ = vm.LoadFileAsync(defaultSample);
            }
        }
    }

    private void OnToolpathLoaded(ToolpathViewer.Core.Models.Toolpath toolpath)
    {
        Viewport.LoadToolpath(toolpath);
    }

    private void OnRequestRender()
    {
        if (DataContext is MainViewModel vm)
        {
            UpdateViewportToggles(vm);
        }
    }

    private void UpdateViewportToggles(MainViewModel vm)
    {
        Viewport.Renderer.ToolpathRenderer.ShowCuts = vm.ShowCuts;
        Viewport.Renderer.ToolpathRenderer.ShowRapids = vm.ShowRapids;
        Viewport.Renderer.ToolpathRenderer.LineWidth = vm.LineWidth;
        Viewport.Renderer.GridRenderer.IsVisible = vm.ShowGrid;
        Viewport.Renderer.AxesRenderer.IsVisible = vm.ShowAxes;
        Viewport.Renderer.BoundingBoxRenderer.IsVisible = vm.ShowBoundingBox;
        Viewport.RequestRedraw();
    }

    private void OnFitViewRequested()
    {
        Viewport.FitView();
    }

    private void OnPresetRequested(ViewPreset preset)
    {
        Viewport.SetPreset(preset);
    }

    private void OnThemeChangeRequested(bool isDark)
    {
        ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light);
        Viewport.SetTheme(isDark);
    }
}