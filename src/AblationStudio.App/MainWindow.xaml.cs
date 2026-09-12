using System.Windows;
using AblationStudio.App.ViewModels;
using AblationStudio.Rendering.Camera;
using Wpf.Ui.Appearance;

namespace AblationStudio.App;

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
            vm.SimulationUpdated += (point, type, extent, currentDistance, isProgressive, showGhost) =>
            {
                Viewport.SetSimulationState(point, type, extent, currentDistance, isProgressive, showGhost);
            };

            Viewport.ShapeDocument = vm.CustomShapes;
            Viewport.CurrentTool = vm.ActiveTool;
            Viewport.ToolSwitched += tool => vm.ActiveTool = tool;
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.ActiveTool))
                {
                    Viewport.CurrentTool = vm.ActiveTool;
                }
            };

            UpdateViewportToggles(vm);
            Dispatcher.InvokeAsync(() => Viewport.RequestRedraw(), System.Windows.Threading.DispatcherPriority.Loaded);
            Dispatcher.InvokeAsync(() => Viewport.RequestRedraw(), System.Windows.Threading.DispatcherPriority.Render);
        }
    }

    private void OnToolpathLoaded(AblationStudio.Core.Models.Toolpath toolpath, bool autoFit)
    {
        Viewport.LoadToolpath(toolpath, autoFit);
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
        Viewport.Renderer.ToolpathRenderer.ShowHatch = vm.ShowHatch;
        Viewport.Renderer.ToolpathRenderer.ShowRapids = vm.ShowRapids;
        Viewport.Renderer.ToolpathRenderer.LineWidth = vm.LineWidth;
        Viewport.Renderer.ToolpathRenderer.ShowGhostPath = vm.ShowGhostTrail;
        Viewport.Renderer.GridRenderer.IsVisible = vm.ShowGrid;
        Viewport.Renderer.OrientationGizmo.IsVisible = vm.ShowAxes;
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

    private void OnNumericTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var binding = System.Windows.Data.BindingOperations.GetBindingExpression(textBox, System.Windows.Controls.TextBox.TextProperty);
                binding?.UpdateSource();
                System.Windows.Input.Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                var binding = System.Windows.Data.BindingOperations.GetBindingExpression(textBox, System.Windows.Controls.TextBox.TextProperty);
                binding?.UpdateTarget();
                System.Windows.Input.Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}