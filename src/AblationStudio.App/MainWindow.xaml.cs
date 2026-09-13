using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

            LostFocus += (s, args) =>
            {
                if (args.OriginalSource is TextBox or Slider or ComboBox)
                {
                    vm.CustomShapes.FlushPropertyCoalescing();
                }
            };

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
                if (DataContext is MainViewModel vm)
                {
                    vm.CustomShapes.FlushPropertyCoalescing();
                }
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

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Suppress standalone Alt / F10 activating WPF menu mode or toggling focus between viewport and menu
        if (e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt || e.SystemKey == Key.F10))
        {
            e.Handled = true;
            return;
        }

        if (IsTextInputActive())
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && DataContext is MainViewModel vmCtrl)
        {
            if (e.Key == Key.A)
            {
                if (vmCtrl.SelectAllCommand.CanExecute(null))
                {
                    vmCtrl.SelectAllCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.None && DataContext is MainViewModel vm)
        {
            switch (e.Key)
            {
                case Key.V:
                    vm.ActiveTool = ShapeToolType.Select;
                    e.Handled = true;
                    break;
                case Key.L:
                    vm.ActiveTool = ShapeToolType.Line;
                    e.Handled = true;
                    break;
                case Key.R:
                    vm.ActiveTool = ShapeToolType.Rectangle;
                    e.Handled = true;
                    break;
                case Key.C:
                    vm.ActiveTool = ShapeToolType.Circle;
                    e.Handled = true;
                    break;
                case Key.P:
                    vm.ActiveTool = ShapeToolType.Polygon;
                    e.Handled = true;
                    break;
                case Key.T:
                    vm.ActiveTool = ShapeToolType.Text;
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (vm.DeleteSelectedShapeCommand.CanExecute(null))
                    {
                        vm.DeleteSelectedShapeCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (vm.DeselectShapeCommand.CanExecute(null))
                    {
                        vm.DeselectShapeCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);

        // Suppress standalone Alt / F10 key release activating menu navigation
        if ((e.Key == Key.System && (e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt || e.SystemKey == Key.F10))
            || e.Key is Key.LeftAlt or Key.RightAlt or Key.F10)
        {
            e.Handled = true;
        }
    }

    private static bool IsTextInputActive()
    {
        IInputElement focused = Keyboard.FocusedElement;
        if (focused is null)
        {
            return false;
        }

        if (focused is TextBoxBase or TextBox or PasswordBox or ComboBox)
        {
            return true;
        }

        if (focused is DependencyObject dep)
        {
            DependencyObject? current = dep;
            while (current is not null)
            {
                if (current is TextBoxBase or TextBox or PasswordBox or ComboBox)
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        return false;
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files is { Length: > 0 } && DataContext is MainViewModel vm)
            {
                foreach (string file in files)
                {
                    await vm.OpenFileOrProjectAsync(file);
                }
            }
            e.Handled = true;
        }
    }
}