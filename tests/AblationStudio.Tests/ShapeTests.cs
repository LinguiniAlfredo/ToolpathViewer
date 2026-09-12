using OpenTK.Mathematics;
using AblationStudio.Core.Models;
using AblationStudio.Core.Parser;
using AblationStudio.Core.Shapes;
using AblationStudio.Rendering.Camera;
using Xunit;

namespace AblationStudio.Tests;

public sealed class ShapeTests
{
    [Fact]
    public void LineShape_PropertiesAndSegments_AreCorrect()
    {
        var line = new LineShape(0f, 0f, 0f, 30f, 40f, 0f);

        Assert.Equal(50f, line.Length, precision: 3);
        Assert.False(line.IsClosed);

        List<ToolpathSegment> segments = line.GenerateSegments().ToList();
        Assert.Single(segments);
        Assert.Equal(50f, segments[0].Length, precision: 3);
        Assert.Equal(SegmentType.Cut, segments[0].Type);

        // Hit test
        Assert.True(line.HitTest(15f, 20f, tolerance: 0.5f));
        Assert.False(line.HitTest(15f, 50f, tolerance: 0.5f));
    }

    [Fact]
    public void CircleShape_GeneratesClosedLoop_WithCorrectPerimeter()
    {
        const float radius = 10f;
        var circle = new CircleShape(0f, 0f, 0f, radius, segments: 64);

        Assert.Equal(20f, circle.Diameter, precision: 3);
        Assert.True(circle.IsClosed);

        List<ToolpathSegment> segments = circle.GenerateSegments().ToList();
        Assert.Equal(64, segments.Count);

        float totalLength = segments.Sum(s => s.Length);
        float expectedCircumference = 2.0f * MathF.PI * radius;
        // 64-segment polygon approximation is within 0.2% of true circle circumference
        Assert.InRange(totalLength, expectedCircumference * 0.99f, expectedCircumference * 1.01f);

        // Hit test
        Assert.True(circle.HitTest(0f, 0f, tolerance: 0.1f));
        Assert.True(circle.HitTest(10f, 0f, tolerance: 0.1f));
        Assert.False(circle.HitTest(15f, 15f, tolerance: 0.1f));
    }

    [Fact]
    public void RectangleShape_GeneratesClosedBoundary_WithCorrectPerimeter()
    {
        var rect = new RectangleShape(0f, 0f, 0f, width: 20f, height: 10f, rotation: 0f);

        Assert.True(rect.IsClosed);

        List<ToolpathSegment> segments = rect.GenerateSegments().ToList();
        Assert.Equal(4, segments.Count);

        float totalLength = segments.Sum(s => s.Length);
        Assert.Equal(60f, totalLength, precision: 3); // 2 * (20 + 10)

        // Hit test
        Assert.True(rect.HitTest(5f, 2f, tolerance: 0.5f));
        Assert.False(rect.HitTest(15f, 10f, tolerance: 0.5f));
    }

    [Fact]
    public void PolygonShape_GeneratesNSidedClosedPath()
    {
        var hexagon = new PolygonShape(0f, 0f, 0f, radius: 10f, sides: 6);

        Assert.True(hexagon.IsClosed);
        Assert.Equal(6, hexagon.Sides);

        List<ToolpathSegment> segments = hexagon.GenerateSegments().ToList();
        Assert.Equal(6, segments.Count);

        // In a regular hexagon inscribed in circle of radius R, side length = R
        foreach (ToolpathSegment seg in segments)
        {
            Assert.Equal(10f, seg.Length, precision: 2);
        }

        // Hit test
        Assert.True(hexagon.HitTest(0f, 0f, tolerance: 0.5f));
        Assert.False(hexagon.HitTest(25f, 25f, tolerance: 0.5f));
    }

    [Fact]
    public void ShapeDocument_CompilesToolpath_WithRapidTransitionsBetweenShapes()
    {
        var doc = new ShapeDocument();

        var rect = new RectangleShape(0f, 0f, 0f, 10f, 10f);
        var circle = new CircleShape(50f, 50f, 0f, 5f, segments: 16);

        doc.AddShape(rect);
        doc.AddShape(circle);

        Toolpath toolpath = doc.CompileToolpath("TestDoc");

        Assert.Equal(2, doc.Shapes.Count);
        // 4 cut segments for rect + 1 rapid transition to circle + 16 cut segments for circle = 21 segments
        Assert.Equal(21, toolpath.Segments.Count);
        Assert.Equal(20, toolpath.Statistics.CutSegmentsCount);
        Assert.Equal(1, toolpath.Statistics.RapidSegmentsCount);
    }

    [Fact]
    public void ShapeDocument_ExportToHCode_ProducesValidGCodeFormat()
    {
        var doc = new ShapeDocument();
        doc.AddShape(new LineShape(0f, 0f, 0f, 10f, 0f, 0f));

        string hCode = doc.ExportToHCode("TestExport.h");

        Assert.Contains("HCH 1 1", hCode);
        Assert.Contains("SL X0.0000 Y0.0000 Z0.0000 M05", hCode);
        Assert.Contains("SL X10.0000 Y0.0000 Z0.0000 M03", hCode);
    }

    [Fact]
    public void ShapeDocument_ExportToHCode_BeginsAtOrigin_RapidsToFirstLocation_AndEndsAtOrigin()
    {
        var doc = new ShapeDocument();
        // Circle centered at (20, 30, 0) with radius 10, first point at (30, 30, 0)
        doc.AddShape(new CircleShape(20f, 30f, 0f, 10f, segments: 16));

        string hCode = doc.ExportToHCode("CircleExport.h");
        string[] lines = hCode.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        // Find motion commands (lines starting with SL)
        List<string> slCommands = lines.Where(l => l.Trim().StartsWith("SL")).ToList();

        Assert.True(slCommands.Count >= 3, "Expected at least origin init, first rapid, and return to origin.");

        // First SL command must establish origin at (0, 0, 0) with laser off (M05)
        Assert.Equal("SL X0.0000 Y0.0000 Z0.0000 M05", slCommands[0].Trim());

        // Second SL command must be a rapid move (M05) to the first marking location (30, 30, 0)
        Assert.Equal("SL X30.0000 Y30.0000 Z0.0000 M05", slCommands[1].Trim());

        // Final SL command must be a rapid move (M05) back to origin (0, 0, 0)
        Assert.Equal("SL X0.0000 Y0.0000 Z0.0000 M05", slCommands[^1].Trim());

        // Parse back using ToolpathParser to verify full round-trip
        Toolpath parsed = ToolpathParser.ParseText(hCode, "CircleExport.h");
        Assert.Equal(18, parsed.Segments.Count); // 16 cut segments + 2 rapid segments (home->shape and shape->home)
        Assert.Equal(16, parsed.Statistics.CutSegmentsCount);
        Assert.Equal(2, parsed.Statistics.RapidSegmentsCount);

        // Verify initial rapid segment from (0,0,0) to (30,30,0)
        ToolpathSegment firstRapid = parsed.Segments[0];
        Assert.Equal(SegmentType.Rapid, firstRapid.Type);
        Assert.Equal(0f, firstRapid.Start.X, precision: 3);
        Assert.Equal(0f, firstRapid.Start.Y, precision: 3);
        Assert.Equal(30f, firstRapid.End.X, precision: 3);
        Assert.Equal(30f, firstRapid.End.Y, precision: 3);

        // Verify final rapid segment from (30,30,0) back to (0,0,0)
        ToolpathSegment lastRapid = parsed.Segments[^1];
        Assert.Equal(SegmentType.Rapid, lastRapid.Type);
        Assert.Equal(30f, lastRapid.Start.X, precision: 3);
        Assert.Equal(30f, lastRapid.Start.Y, precision: 3);
        Assert.Equal(0f, lastRapid.End.X, precision: 3);
        Assert.Equal(0f, lastRapid.End.Y, precision: 3);
    }

    [Fact]
    public void ShapeDocument_ExportToHCode_MultipleShapes_TransitionsBetweenAndReturnsHome()
    {
        var doc = new ShapeDocument();
        // Shape 1: Line from (10, 10, 0) to (20, 10, 0)
        doc.AddShape(new LineShape(10f, 10f, 0f, 20f, 10f, 0f));
        // Shape 2: Rectangle at (40, 40, 0), width 10, height 10
        doc.AddShape(new RectangleShape(40f, 40f, 0f, 10f, 10f));

        string hCode = doc.ExportToHCode("MultiShapeExport.h");
        Toolpath parsed = ToolpathParser.ParseText(hCode, "MultiShapeExport.h");

        // 1 cut for line + 4 cuts for rectangle = 5 cuts
        Assert.Equal(5, parsed.Statistics.CutSegmentsCount);
        // 1 rapid from home to line + 1 rapid from line to rectangle + 1 rapid from rectangle to home = 3 rapids
        Assert.Equal(3, parsed.Statistics.RapidSegmentsCount);

        // Rapid 1: (0,0,0) -> (10,10,0)
        Assert.Equal(SegmentType.Rapid, parsed.Segments[0].Type);
        Assert.Equal(0f, parsed.Segments[0].Start.X);
        Assert.Equal(10f, parsed.Segments[0].End.X);

        // Rapid 2: (20,10,0) -> (35,35,0) [rectangle corner]
        Assert.Equal(SegmentType.Rapid, parsed.Segments[2].Type);
        Assert.Equal(20f, parsed.Segments[2].Start.X);
        Assert.Equal(10f, parsed.Segments[2].Start.Y);

        // Rapid 3: rectangle end -> (0,0,0)
        Assert.Equal(SegmentType.Rapid, parsed.Segments[^1].Type);
        Assert.Equal(0f, parsed.Segments[^1].End.X);
        Assert.Equal(0f, parsed.Segments[^1].End.Y);
    }

    [Fact]
    public void ShapeDocument_Clear_ResetsAllShapes()
    {
        var doc = new ShapeDocument();
        doc.AddShape(new LineShape(0f, 0f, 0f, 5f, 5f, 0f));
        doc.AddShape(new CircleShape(10f, 10f, 0f, 3f));
        Assert.Equal(2, doc.Shapes.Count);

        doc.Clear();
        Assert.Empty(doc.Shapes);
        Assert.Null(doc.SelectedShape);

        Toolpath empty = doc.CompileToolpath("Empty");
        Assert.Empty(empty.Segments);
    }

    [Fact]
    public void Camera3D_ScreenPointToRay_CenterHitsTarget()
    {
        var camera = new AblationStudio.Rendering.Camera.Camera3D
        {
            Target = OpenTK.Mathematics.Vector3.Zero,
            Distance = 25f,
            Pitch = 35.264f,
            Yaw = 45f,
            AspectRatio = 800f / 600f
        };

        var (rayOrigin, rayDir) = camera.ScreenPointToRay(400f, 300f, 800, 600);
        bool hit = camera.IntersectRayPlaneZ(rayOrigin, rayDir, 0f, out OpenTK.Mathematics.Vector3 hitPoint);

        Assert.True(hit);
        Assert.True(MathF.Abs(hitPoint.X) < 0.01f, $"HitPoint.X was {hitPoint.X}");
        Assert.True(MathF.Abs(hitPoint.Y) < 0.01f, $"HitPoint.Y was {hitPoint.Y}");
        Assert.True(MathF.Abs(hitPoint.Z) < 0.01f, $"HitPoint.Z was {hitPoint.Z}");
    }

    [Fact]
    public void Shapes_GetBounds_HaveCorrectCoordinateOrderingAndExtents()
    {
        // CircleShape: centered at (10, 20, 5), radius 15
        var circle = new CircleShape(10f, 20f, 5f, 15f);
        BoundingBox3D circleBounds = circle.GetBounds();
        Assert.Equal(-5f, circleBounds.MinX, precision: 3);
        Assert.Equal(5f, circleBounds.MinY, precision: 3);
        Assert.Equal(5f, circleBounds.MinZ, precision: 3);
        Assert.Equal(25f, circleBounds.MaxX, precision: 3);
        Assert.Equal(35f, circleBounds.MaxY, precision: 3);
        Assert.Equal(5f, circleBounds.MaxZ, precision: 3);
        Assert.Equal(30f, circleBounds.SizeX, precision: 3);
        Assert.Equal(30f, circleBounds.SizeY, precision: 3);

        // RectangleShape: centered at (0, 0, 0), width 40, height 20
        var rect = new RectangleShape(0f, 0f, 0f, 40f, 20f);
        BoundingBox3D rectBounds = rect.GetBounds();
        Assert.Equal(-20f, rectBounds.MinX, precision: 3);
        Assert.Equal(-10f, rectBounds.MinY, precision: 3);
        Assert.Equal(0f, rectBounds.MinZ, precision: 3);
        Assert.Equal(20f, rectBounds.MaxX, precision: 3);
        Assert.Equal(10f, rectBounds.MaxY, precision: 3);
        Assert.Equal(0f, rectBounds.MaxZ, precision: 3);
        Assert.Equal(40f, rectBounds.SizeX, precision: 3);
        Assert.Equal(20f, rectBounds.SizeY, precision: 3);

        // LineShape: from (5, 10, -2) to (25, 40, 8)
        var line = new LineShape(5f, 10f, -2f, 25f, 40f, 8f);
        BoundingBox3D lineBounds = line.GetBounds();
        Assert.Equal(5f, lineBounds.MinX, precision: 3);
        Assert.Equal(10f, lineBounds.MinY, precision: 3);
        Assert.Equal(-2f, lineBounds.MinZ, precision: 3);
        Assert.Equal(25f, lineBounds.MaxX, precision: 3);
        Assert.Equal(40f, lineBounds.MaxY, precision: 3);
        Assert.Equal(8f, lineBounds.MaxZ, precision: 3);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(45f)]
    [InlineData(90f)]
    [InlineData(135f)]
    [InlineData(180f)]
    [InlineData(270f)]
    public void Orbit_TowardsTopPerspective_DoesNotSnapOrProduceNaN(float yaw)
    {
        var camera = new Camera3D
        {
            Yaw = yaw,
            Distance = 25f,
            Target = Vector3.Zero
        };

        Matrix4 prevView = Matrix4.Identity;

        // Sweep pitch across previous snap zone (80° to 89.9°) in fine steps
        for (float pitch = 80.0f; pitch <= 89.9f; pitch += 0.1f)
        {
            camera.Pitch = pitch;
            Matrix4 view = camera.GetViewMatrix();

            // 1. Verify no NaNs or Infinities
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Assert.False(float.IsNaN(view[r, c]), $"NaN at ({r},{c}) for Yaw={yaw}, Pitch={pitch}");
                    Assert.False(float.IsInfinity(view[r, c]), $"Infinity at ({r},{c}) for Yaw={yaw}, Pitch={pitch}");
                }
            }

            // 2. Verify Up and Right vectors are unit length and orthogonal
            Vector3 right = camera.Right;
            Vector3 up = camera.Up;

            Assert.Equal(1.0f, right.Length, precision: 4);
            Assert.Equal(1.0f, up.Length, precision: 4);
            Assert.True(MathF.Abs(Vector3.Dot(right, up)) < 1e-4f, $"Right and Up not orthogonal for Yaw={yaw}, Pitch={pitch}");

            // 3. Verify continuity: change between consecutive 0.1° steps must be smooth, without discontinuous jumps
            if (pitch > 80.0f)
            {
                float maxDiff = 0f;
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        float diff = MathF.Abs(view[r, c] - prevView[r, c]);
                        if (diff > maxDiff)
                        {
                            maxDiff = diff;
                        }
                    }
                }

                // In 0.1° step, matrix elements shouldn't change by more than ~0.05
                Assert.True(maxDiff < 0.1f, $"Discontinuous jump ({maxDiff:F4}) detected at Yaw={yaw}, Pitch={pitch}");
            }

            prevView = view;
        }
    }

    [Fact]
    public void ToggleButton_ContentPresenterTextElementForeground_SwitchesFromWhiteToBlackWhenChecked()
    {
        var thread = new Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            if (app.Resources.MergedDictionaries.Count == 0)
            {
                var themeDict = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark };
                var controlsDict = new Wpf.Ui.Markup.ControlsDictionary();
                app.Resources.MergedDictionaries.Add(themeDict);
                app.Resources.MergedDictionaries.Add(controlsDict);
            }

            string xaml = @"
            <ToggleButton xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                          xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
                <StackPanel Orientation=""Horizontal"">
                    <Border Width=""8"" Height=""8"" CornerRadius=""4"" Background=""#00D2FF"" Margin=""0,0,6,0"" VerticalAlignment=""Center"" />
                    <TextBlock Text=""Cuts (M03)""
                               Foreground=""{Binding RelativeSource={RelativeSource AncestorType=ContentPresenter}, Path=(TextElement.Foreground)}"" />
                </StackPanel>
            </ToggleButton>";

            var toggle = (System.Windows.Controls.Primitives.ToggleButton)System.Windows.Markup.XamlReader.Parse(xaml);
            var win = new System.Windows.Window { Content = toggle };
            win.Show();

            var sp = (System.Windows.Controls.StackPanel)toggle.Content;
            var tb = (System.Windows.Controls.TextBlock)sp.Children[1];

            // Inactive / unchecked should be white (#FFFFFFFF)
            Assert.Equal("#FFFFFFFF", tb.Foreground.ToString());

            // Active / checked should switch to black (#FF000000)
            toggle.IsChecked = true;
            toggle.UpdateLayout();
            Assert.Equal("#FF000000", tb.Foreground.ToString());

            win.Close();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void MenuItem_LineIcon_ResolvesThemeBrushAndRendersShape()
    {
        var thread = new Thread(() =>
        {
            var app = System.Windows.Application.Current ?? new System.Windows.Application();
            if (app.Resources.MergedDictionaries.Count == 0)
            {
                var themeDict = new Wpf.Ui.Markup.ThemesDictionary { Theme = Wpf.Ui.Appearance.ApplicationTheme.Dark };
                var controlsDict = new Wpf.Ui.Markup.ControlsDictionary();
                app.Resources.MergedDictionaries.Add(themeDict);
                app.Resources.MergedDictionaries.Add(controlsDict);
            }

            string xaml = @"
            <Menu xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                  xmlns:ui=""http://schemas.lepo.co/wpfui/2022/xaml"">
                <MenuItem Header=""Tools"">
                    <MenuItem Header=""Line Tool"">
                        <MenuItem.Icon>
                            <Grid Width=""16"" Height=""16"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"">
                                <Rectangle Width=""16"" Height=""2"" RadiusX=""1"" RadiusY=""1""
                                           Fill=""{DynamicResource TextFillColorPrimaryBrush}""
                                           HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                            </Grid>
                        </MenuItem.Icon>
                    </MenuItem>
                </MenuItem>
            </Menu>";

            var menu = (System.Windows.Controls.Menu)System.Windows.Markup.XamlReader.Parse(xaml);
            var win = new System.Windows.Window { Content = menu };
            win.Show();

            var topItem = (System.Windows.Controls.MenuItem)menu.Items[0];
            topItem.IsSubmenuOpen = true;
            topItem.UpdateLayout();

            var lineItem = (System.Windows.Controls.MenuItem)topItem.Items[0];
            var iconGrid = (System.Windows.Controls.Grid)lineItem.Icon;
            var rect = (System.Windows.Shapes.Rectangle)iconGrid.Children[0];

            Assert.NotNull(rect.Fill);
            Assert.Equal(16, rect.Width);
            Assert.Equal(2, rect.Height);

            win.Close();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Theory]
    [InlineData(ViewPreset.Isometric, 45f, 35.264f)]
    [InlineData(ViewPreset.Top, 0f, 89.9f)]
    [InlineData(ViewPreset.Front, 0f, 0f)]
    [InlineData(ViewPreset.Right, 90f, 0f)]
    public void GetPresetAngles_ValidPreset_ReturnsExpectedYawAndPitch(ViewPreset preset, float expectedYaw, float expectedPitch)
    {
        var (yaw, pitch) = Camera3D.GetPresetAngles(preset);
        Assert.Equal(expectedYaw, yaw, 3);
        Assert.Equal(expectedPitch, pitch, 3);
    }

    [Theory]
    [InlineData(0f, 90f, 90f)]
    [InlineData(90f, 0f, -90f)]
    [InlineData(10f, 350f, -20f)]
    [InlineData(350f, 10f, 20f)]
    [InlineData(45f, 0f, -45f)]
    [InlineData(180f, 180f, 0f)]
    public void ShortestAngleDistance_VariousAngles_ComputesShortestDelta(float from, float to, float expectedDelta)
    {
        float delta = Camera3D.ShortestAngleDistance(from, to);
        Assert.Equal(expectedDelta, delta, 3);
    }

    [Theory]
    [InlineData(350f, 10f, 0.0f, 350f)]
    [InlineData(350f, 10f, 0.5f, 0f)]
    [InlineData(350f, 10f, 1.0f, 10f)]
    [InlineData(10f, 350f, 0.5f, 0f)]
    [InlineData(45f, 0f, 1.0f, 0f)]
    public void InterpolateAngle_WrappingSeam_InterpolatesSeamlessly(float from, float to, float t, float expected)
    {
        float result = Camera3D.InterpolateAngle(from, to, t);
        Assert.Equal(expected, result, 3);
    }

    [Fact]
    public void EaseInOutCubic_BoundaryAndProgression_BehavesSmoothly()
    {
        Assert.Equal(0f, Camera3D.EaseInOutCubic(0f), 4);
        Assert.Equal(0.5f, Camera3D.EaseInOutCubic(0.5f), 4);
        Assert.Equal(1f, Camera3D.EaseInOutCubic(1f), 4);

        // Strictly monotonic between 0 and 1
        float prev = -1f;
        for (float t = 0f; t <= 1.0f; t += 0.05f)
        {
            float val = Camera3D.EaseInOutCubic(t);
            Assert.True(val >= prev, $"Not monotonic at t={t}: prev={prev}, curr={val}");
            Assert.InRange(val, 0f, 1f);
            prev = val;
        }
    }

    [Fact]
    public void HatchSettings_Stepover_ManualTextBoxBinding_UpdatesBothWays()
    {
        var thread = new Thread(() =>
        {
            var hatch = new HatchSettings { Stepover = 0.5f };

            var textBox = new System.Windows.Controls.TextBox();
            var binding = new System.Windows.Data.Binding(nameof(HatchSettings.Stepover))
            {
                Source = hatch,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
                StringFormat = "{0:0.###}"
            };
            textBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);

            // 1. Initial display
            Assert.Equal("0.5", textBox.Text);

            // 2. Simulated external slider change updates TextBox
            hatch.Stepover = 0.025f;
            Assert.Equal("0.025", textBox.Text);

            // 3. Simulated user manual entry updates hatch settings on commit
            textBox.Text = "0.03";
            var expr = System.Windows.Data.BindingOperations.GetBindingExpression(textBox, System.Windows.Controls.TextBox.TextProperty);
            expr?.UpdateSource();
            Assert.Equal(0.03f, hatch.Stepover, precision: 4);

            // 4. Clamping handles manual input below 0.01 mm
            textBox.Text = "0.002";
            expr?.UpdateSource();
            Assert.Equal(0.01f, hatch.Stepover, precision: 4);
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}





