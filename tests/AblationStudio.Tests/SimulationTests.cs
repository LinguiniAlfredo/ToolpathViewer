using AblationStudio.Core.Models;
using AblationStudio.Core.Simulation;
using Xunit;

namespace AblationStudio.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void Constructor_EmptyToolpath_InitializesWithZeroDistanceAndStoppedState()
    {
        var simulator = new ToolpathSimulator(Toolpath.Empty);

        Assert.Equal(0f, simulator.TotalDistance);
        Assert.Equal(0f, simulator.CurrentDistance);
        Assert.Equal(0f, simulator.ProgressFraction);
        Assert.Equal(SimulationState.Stopped, simulator.State);
        Assert.Equal(ToolpathPoint.Zero, simulator.CurrentPosition);
    }

    [Fact]
    public void Constructor_ValidToolpath_SetsPositionToFirstStartPoint()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(5f, 10f, 0f), new ToolpathPoint(15f, 10f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath);

        Assert.Equal(10f, simulator.TotalDistance, precision: 3);
        Assert.Equal(0f, simulator.CurrentDistance);
        Assert.Equal(SimulationState.Stopped, simulator.State);
        Assert.Equal(new ToolpathPoint(5f, 10f, 0f), simulator.CurrentPosition);
        Assert.Equal(SegmentType.Cut, simulator.CurrentSegmentType);
    }

    [Fact]
    public void PlayAndPause_StateTransitions_BehavesCorrectly()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(10f, 0f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath);

        simulator.Play();
        Assert.Equal(SimulationState.Playing, simulator.State);

        simulator.Pause();
        Assert.Equal(SimulationState.Paused, simulator.State);

        simulator.TogglePlay();
        Assert.Equal(SimulationState.Playing, simulator.State);

        simulator.TogglePlay();
        Assert.Equal(SimulationState.Paused, simulator.State);
    }

    [Fact]
    public void Stop_WhenActive_ResetsDistanceAndPosition()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(20f, 0f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath)
        {
            Feedrate = 10f
        };

        simulator.Play();
        simulator.Update(0.5f); // 5mm

        Assert.Equal(5f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(new ToolpathPoint(5f, 0f, 0f), simulator.CurrentPosition);

        simulator.Stop();
        Assert.Equal(SimulationState.Stopped, simulator.State);
        Assert.Equal(0f, simulator.CurrentDistance);
        Assert.Equal(new ToolpathPoint(0f, 0f, 0f), simulator.CurrentPosition);
    }

    [Fact]
    public void Update_AdvancesPositionProportionalToFeedrateAndDeltaTime()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(100f, 0f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath)
        {
            Feedrate = 50f // 50 mm/s
        };

        simulator.Play();
        bool moved = simulator.Update(0.4f); // 20 mm

        Assert.True(moved);
        Assert.Equal(20f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(0.2f, simulator.ProgressFraction, precision: 3);
        Assert.Equal(new ToolpathPoint(20f, 0f, 0f), simulator.CurrentPosition);
    }

    [Fact]
    public void Update_CrossesMultipleSegmentsAndUpdatesSegmentType()
    {
        var seg1 = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(10f, 0f, 0f), SegmentType.Cut);
        var seg2 = new ToolpathSegment(new ToolpathPoint(10f, 0f, 0f), new ToolpathPoint(10f, 20f, 0f), SegmentType.Rapid);
        var toolpath = new Toolpath("test.h", "test.h", [seg1, seg2]);
        var simulator = new ToolpathSimulator(toolpath)
        {
            Feedrate = 10f // 10 mm/s
        };

        simulator.Play();

        // 0.5s -> 5mm into seg1 (Cut)
        simulator.Update(0.5f);
        Assert.Equal(0, simulator.CurrentSegmentIndex);
        Assert.Equal(SegmentType.Cut, simulator.CurrentSegmentType);
        Assert.Equal(new ToolpathPoint(5f, 0f, 0f), simulator.CurrentPosition);

        // 1.0s more -> total 15mm: 5mm into seg2 (Rapid)
        simulator.Update(1.0f);
        Assert.Equal(1, simulator.CurrentSegmentIndex);
        Assert.Equal(SegmentType.Rapid, simulator.CurrentSegmentType);
        Assert.Equal(new ToolpathPoint(10f, 5f, 0f), simulator.CurrentPosition);
    }

    [Fact]
    public void Update_ReachingEnd_CompletesSimulationAndCanLoop()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(10f, 0f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath)
        {
            Feedrate = 20f
        };

        simulator.Play();
        simulator.Update(1.0f); // 20mm > 10mm total

        Assert.Equal(SimulationState.Completed, simulator.State);
        Assert.Equal(10f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(1.0f, simulator.ProgressFraction, precision: 3);
        Assert.Equal(new ToolpathPoint(10f, 0f, 0f), simulator.CurrentPosition);

        // Playing while completed restarts from 0
        simulator.Play();
        Assert.Equal(SimulationState.Playing, simulator.State);
        Assert.Equal(0f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(new ToolpathPoint(0f, 0f, 0f), simulator.CurrentPosition);
    }

    [Fact]
    public void SeekToFraction_ScrubsPositionAccurately()
    {
        var seg1 = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(20f, 0f, 0f), SegmentType.Cut);
        var seg2 = new ToolpathSegment(new ToolpathPoint(20f, 0f, 0f), new ToolpathPoint(20f, 20f, 0f), SegmentType.Rapid);
        var toolpath = new Toolpath("test.h", "test.h", [seg1, seg2]); // 40mm total
        var simulator = new ToolpathSimulator(toolpath);

        // Seek to 25% (10mm -> halfway through seg1)
        simulator.SeekToFraction(0.25f);
        Assert.Equal(10f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(new ToolpathPoint(10f, 0f, 0f), simulator.CurrentPosition);
        Assert.Equal(SegmentType.Cut, simulator.CurrentSegmentType);

        // Seek to 75% (30mm -> halfway through seg2)
        simulator.SeekToFraction(0.75f);
        Assert.Equal(30f, simulator.CurrentDistance, precision: 3);
        Assert.Equal(new ToolpathPoint(20f, 10f, 0f), simulator.CurrentPosition);
        Assert.Equal(SegmentType.Rapid, simulator.CurrentSegmentType);
    }

    [Fact]
    public void Update_TraversesHatchSegment_SetsSegmentTypeToHatch()
    {
        var seg1 = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(10f, 0f, 0f), SegmentType.Cut);
        var seg2 = new ToolpathSegment(new ToolpathPoint(10f, 0f, 0f), new ToolpathPoint(10f, 10f, 0f), SegmentType.Hatch);
        var seg3 = new ToolpathSegment(new ToolpathPoint(10f, 10f, 0f), new ToolpathPoint(0f, 0f, 0f), SegmentType.Rapid);
        var toolpath = new Toolpath("test.h", "test.h", [seg1, seg2, seg3]);
        var simulator = new ToolpathSimulator(toolpath)
        {
            Feedrate = 10f
        };

        simulator.Play();

        // 0.5s -> 5mm into seg1 (Cut / M03 Profile)
        simulator.Update(0.5f);
        Assert.Equal(SegmentType.Cut, simulator.CurrentSegmentType);

        // 1.0s more -> 15mm total -> 5mm into seg2 (Hatch / M03 Infill)
        simulator.Update(1.0f);
        Assert.Equal(SegmentType.Hatch, simulator.CurrentSegmentType);
        Assert.Equal(new ToolpathPoint(10f, 5f, 0f), simulator.CurrentPosition);
    }

    [Fact]
    public void ToolIndicatorRenderer_Colors_DistinguishesHatchFromRapidAndCut()
    {
        // Verify that ToolIndicatorRenderer defines distinct Cut, Hatch, and Rapid colors
        Assert.NotEqual(AblationStudio.Rendering.Renderers.ToolIndicatorRenderer.HatchColor,
                        AblationStudio.Rendering.Renderers.ToolIndicatorRenderer.RapidColor);
        Assert.NotEqual(AblationStudio.Rendering.Renderers.ToolIndicatorRenderer.HatchColor,
                        AblationStudio.Rendering.Renderers.ToolIndicatorRenderer.CutColor);
    }

    [Fact]
    public void SeekToDistance_FromStoppedStateWithPositiveDistance_TransitionsToPaused()
    {
        var seg = new ToolpathSegment(new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(50f, 0f, 0f), SegmentType.Cut);
        var toolpath = new Toolpath("test.h", "test.h", [seg]);
        var simulator = new ToolpathSimulator(toolpath);

        Assert.Equal(SimulationState.Stopped, simulator.State);

        simulator.SeekToDistance(15f);
        Assert.Equal(SimulationState.Paused, simulator.State);
        Assert.Equal(15f, simulator.CurrentDistance, precision: 3);

        simulator.Stop();
        Assert.Equal(SimulationState.Stopped, simulator.State);
        Assert.Equal(0f, simulator.CurrentDistance);
    }

    [Fact]
    public void ToolpathShaders_Sources_ContainProgressiveAttributesAndUniforms()
    {
        string vSource = AblationStudio.Rendering.Shaders.CommonShaders.ToolpathVertexShaderSource;
        string fSource = AblationStudio.Rendering.Shaders.CommonShaders.ToolpathFragmentShaderSource;

        Assert.Contains("aDistance", vSource);
        Assert.Contains("vDistance", vSource);

        Assert.Contains("uMaxDistance", fSource);
        Assert.Contains("uProgressiveMode", fSource);
        Assert.Contains("uGhostOpacity", fSource);
        Assert.Contains("discard", fSource);
    }

    [Fact]
    public void ToolpathRenderer_ProgressiveProperties_SetAndGetCorrectly()
    {
        using var renderer = new AblationStudio.Rendering.Renderers.ToolpathRenderer();

        Assert.False(renderer.IsProgressive);
        Assert.Equal(0f, renderer.CurrentDistance);
        Assert.False(renderer.ShowGhostPath);
        Assert.Equal(0.20f, renderer.GhostOpacity, precision: 3);

        renderer.IsProgressive = true;
        renderer.CurrentDistance = 42.5f;
        renderer.ShowGhostPath = true;
        renderer.GhostOpacity = 0.25f;

        Assert.True(renderer.IsProgressive);
        Assert.Equal(42.5f, renderer.CurrentDistance);
        Assert.True(renderer.ShowGhostPath);
        Assert.Equal(0.25f, renderer.GhostOpacity, precision: 3);
    }
}


