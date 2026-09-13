using AblationStudio.Core.History;
using AblationStudio.Core.History.Actions;
using AblationStudio.Core.Models;
using AblationStudio.Core.Shapes;
using Xunit;

namespace AblationStudio.Tests;

public sealed class UndoRedoTests
{
    [Fact]
    public void AddShape_Undo_RemovesShapeFromDocument()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(10f, 20f, 0f, 5f);
        doc.AddShape(circle);

        var action = new AddShapeAction(doc, circle, doc.Shapes.Count - 1);
        doc.UndoManager.RecordAction(action);

        Assert.Single(doc.Shapes);
        Assert.True(doc.UndoManager.CanUndo);

        doc.UndoManager.Undo();

        Assert.Empty(doc.Shapes);
        Assert.Null(doc.SelectedShape);
        Assert.False(doc.UndoManager.CanUndo);
        Assert.True(doc.UndoManager.CanRedo);
    }

    [Fact]
    public void AddShape_Redo_RestoresShapeAtOriginalIndex()
    {
        var doc = new ShapeDocument();
        var circle1 = new CircleShape(0f, 0f, 0f, 5f);
        var circle2 = new CircleShape(10f, 10f, 0f, 8f);
        doc.AddShape(circle1);
        doc.AddShape(circle2);

        var action = new AddShapeAction(doc, circle2, 1);
        doc.UndoManager.RecordAction(action);

        doc.UndoManager.Undo();
        Assert.Single(doc.Shapes);

        doc.UndoManager.Redo();
        Assert.Equal(2, doc.Shapes.Count);
        Assert.Same(circle2, doc.Shapes[1]);
        Assert.Same(circle2, doc.SelectedShape);
    }

    [Fact]
    public void DeleteShape_Undo_RestoresShapeAtOriginalIndexAndSelection()
    {
        var doc = new ShapeDocument();
        var circle1 = new CircleShape(0f, 0f, 0f, 5f);
        var circle2 = new CircleShape(10f, 10f, 0f, 8f);
        var circle3 = new CircleShape(20f, 20f, 0f, 12f);

        doc.AddShape(circle1);
        doc.AddShape(circle2);
        doc.AddShape(circle3);

        doc.SelectedShape = circle2;
        int deleteIndex = doc.Shapes.IndexOf(circle2);
        var action = new DeleteShapeAction(doc, circle2, deleteIndex, wasSelected: true);
        doc.UndoManager.RecordAction(action);
        doc.RemoveShape(circle2);

        Assert.Equal(2, doc.Shapes.Count);
        Assert.DoesNotContain(circle2, doc.Shapes);

        doc.UndoManager.Undo();

        Assert.Equal(3, doc.Shapes.Count);
        Assert.Same(circle2, doc.Shapes[1]);
        Assert.Same(circle2, doc.SelectedShape);
    }

    [Fact]
    public void DeleteShape_Redo_RemovesShapeAgain()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        var action = new DeleteShapeAction(doc, circle, 0);
        doc.UndoManager.RecordAction(action);
        doc.RemoveShape(circle);

        doc.UndoManager.Undo();
        Assert.Single(doc.Shapes);

        doc.UndoManager.Redo();
        Assert.Empty(doc.Shapes);
    }

    [Fact]
    public void ClearDocument_Undo_RestoresAllShapesInOriginalZOrder()
    {
        var doc = new ShapeDocument();
        var s1 = new LineShape(0f, 0f, 0f, 10f, 10f, 0f);
        var s2 = new RectangleShape(5f, 5f, 0f, 20f, 10f, 0f);
        var s3 = new CircleShape(0f, 0f, 0f, 15f);

        doc.AddShape(s1);
        doc.AddShape(s2);
        doc.AddShape(s3);
        doc.SelectedShape = s2;

        var action = new ClearShapesAction(doc, doc.Shapes, doc.SelectedShape);
        doc.UndoManager.RecordAction(action);
        doc.Clear();

        Assert.Empty(doc.Shapes);
        Assert.Null(doc.SelectedShape);

        doc.UndoManager.Undo();

        Assert.Equal(3, doc.Shapes.Count);
        Assert.Same(s1, doc.Shapes[0]);
        Assert.Same(s2, doc.Shapes[1]);
        Assert.Same(s3, doc.Shapes[2]);
        Assert.Same(s2, doc.SelectedShape);
    }

    [Fact]
    public void ClearDocument_Redo_ClearsDocumentAgain()
    {
        var doc = new ShapeDocument();
        var s1 = new LineShape(0f, 0f, 0f, 10f, 10f, 0f);
        doc.AddShape(s1);

        var action = new ClearShapesAction(doc, doc.Shapes, s1);
        doc.UndoManager.RecordAction(action);
        doc.Clear();

        doc.UndoManager.Undo();
        Assert.Single(doc.Shapes);

        doc.UndoManager.Redo();
        Assert.Empty(doc.Shapes);
    }

    [Fact]
    public void MoveShape_Undo_RestoresOriginalPosition()
    {
        var doc = new ShapeDocument();
        var rect = new RectangleShape(0f, 0f, 0f, 20f, 10f, 0f);
        doc.AddShape(rect);

        ToolpathShape beforeSnapshot = rect.Clone();
        rect.Translate(15f, 25f, 0f);
        ToolpathShape afterSnapshot = rect.Clone();

        var action = new ModifyShapeAction(rect, beforeSnapshot, afterSnapshot, "Move Rectangle", document: doc);
        doc.UndoManager.RecordAction(action);

        Assert.Equal(15f, rect.PositionX);
        Assert.Equal(25f, rect.PositionY);

        doc.UndoManager.Undo();

        Assert.Equal(0f, rect.PositionX);
        Assert.Equal(0f, rect.PositionY);

        doc.UndoManager.Redo();

        Assert.Equal(15f, rect.PositionX);
        Assert.Equal(25f, rect.PositionY);
    }

    [Fact]
    public void ScaleShape_Undo_RestoresOriginalDimensions()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 10f);
        doc.AddShape(circle);

        ToolpathShape beforeSnapshot = circle.Clone();
        circle.Scale(2.0f, 0f, 0f);
        ToolpathShape afterSnapshot = circle.Clone();

        var action = new ModifyShapeAction(circle, beforeSnapshot, afterSnapshot, "Scale Circle", document: doc);
        doc.UndoManager.RecordAction(action);

        Assert.Equal(20f, circle.Radius);

        doc.UndoManager.Undo();

        Assert.Equal(10f, circle.Radius);

        doc.UndoManager.Redo();

        Assert.Equal(20f, circle.Radius);
    }

    [Fact]
    public void RotateShape_Undo_RestoresOriginalRotation()
    {
        var doc = new ShapeDocument();
        var rect = new RectangleShape(0f, 0f, 0f, 20f, 10f, 0f);
        doc.AddShape(rect);

        ToolpathShape beforeSnapshot = rect.Clone();
        rect.Rotate(45f, 0f, 0f);
        ToolpathShape afterSnapshot = rect.Clone();

        var action = new ModifyShapeAction(rect, beforeSnapshot, afterSnapshot, "Rotate Rectangle", document: doc);
        doc.UndoManager.RecordAction(action);

        Assert.Equal(45f, rect.RotationDegrees);

        doc.UndoManager.Undo();

        Assert.Equal(0f, rect.RotationDegrees);

        doc.UndoManager.Redo();

        Assert.Equal(45f, rect.RotationDegrees);
    }

    [Fact]
    public void InspectorEdit_ChangeRadius_CanBeUndone()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        // Direct property change via inspector triggers automatic recording
        circle.Radius = 15f;

        Assert.Equal(15f, circle.Radius);
        Assert.True(doc.UndoManager.CanUndo);

        doc.UndoManager.Undo();

        Assert.Equal(5f, circle.Radius);

        doc.UndoManager.Redo();

        Assert.Equal(15f, circle.Radius);
    }

    [Fact]
    public void InspectorEdit_ChangeHatchSettings_CanBeUndone()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 10f);
        doc.AddShape(circle);

        Assert.False(circle.Hatch.IsEnabled);

        circle.Hatch.IsEnabled = true;
        Assert.True(doc.UndoManager.CanUndo);

        doc.UndoManager.Undo();
        Assert.False(circle.Hatch.IsEnabled);

        doc.UndoManager.Redo();
        Assert.True(circle.Hatch.IsEnabled);
    }

    [Fact]
    public void InspectorEdit_ChangeName_CanBeUndone()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 10f) { Name = "InitialName" };
        doc.AddShape(circle);

        circle.Name = "UpdatedName";
        Assert.Equal("UpdatedName", circle.Name);

        doc.UndoManager.Undo();
        Assert.Equal("InitialName", circle.Name);

        doc.UndoManager.Redo();
        Assert.Equal("UpdatedName", circle.Name);
    }

    [Fact]
    public void RapidEdits_WithinCoalesceWindow_CoalesceIntoSingleUndoStep()
    {
        var doc = new ShapeDocument();
        doc.UndoManager.CoalesceWindow = TimeSpan.FromSeconds(2);
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        // Simulate rapid slider dragging: 5 -> 6 -> 7 -> 8
        circle.Radius = 6f;
        circle.Radius = 7f;
        circle.Radius = 8f;

        Assert.Equal(8f, circle.Radius);

        // Should only be one undo action because radius edits coalesced
        doc.UndoManager.Undo();

        Assert.Equal(5f, circle.Radius);
        Assert.False(doc.UndoManager.CanUndo);
    }

    [Fact]
    public void RapidEdits_ExceedingCoalesceWindow_CreateSeparateUndoSteps()
    {
        var doc = new ShapeDocument();
        doc.UndoManager.CoalesceWindow = TimeSpan.FromMilliseconds(10);
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        circle.Radius = 6f;
        Thread.Sleep(30);
        circle.Radius = 7f;

        Assert.Equal(7f, circle.Radius);

        doc.UndoManager.Undo();
        Assert.Equal(6f, circle.Radius);

        doc.UndoManager.Undo();
        Assert.Equal(5f, circle.Radius);
    }

    [Fact]
    public void FlushCoalescing_ForcesImmediateFinalization()
    {
        var doc = new ShapeDocument();
        doc.UndoManager.CoalesceWindow = TimeSpan.FromMinutes(1);
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        circle.Radius = 6f;
        doc.FlushPropertyCoalescing(); // Simulates focus-out
        circle.Radius = 7f;

        Assert.Equal(7f, circle.Radius);

        doc.UndoManager.Undo();
        Assert.Equal(6f, circle.Radius);

        doc.UndoManager.Undo();
        Assert.Equal(5f, circle.Radius);
    }

    [Fact]
    public void Undo_RestoresSelectionState_WhenShapeWasSelected()
    {
        var doc = new ShapeDocument();
        var c1 = new CircleShape(0f, 0f, 0f, 5f);
        var c2 = new CircleShape(10f, 10f, 0f, 10f);
        doc.AddShape(c1);
        doc.AddShape(c2);

        doc.SelectedShape = c1;
        c1.Radius = 8f;

        // Switch selection to c2
        doc.SelectedShape = c2;
        Assert.Same(c2, doc.SelectedShape);

        doc.UndoManager.Undo();

        // c1 should be selected upon undo because it was selected when c1.Radius changed
        Assert.Same(c1, doc.SelectedShape);
        Assert.Equal(5f, c1.Radius);
    }

    [Fact]
    public void DirtyState_UndoToSavePoint_ResetsIsModifiedToFalse()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        var action1 = new AddShapeAction(doc, circle, 0);
        doc.UndoManager.RecordAction(action1);

        doc.UndoManager.MarkSaved();
        Assert.False(doc.UndoManager.IsModified);

        // Edit property
        circle.Radius = 12f;
        Assert.True(doc.UndoManager.IsModified);

        // Undo back to save point
        doc.UndoManager.Undo();
        Assert.Equal(5f, circle.Radius);
        Assert.False(doc.UndoManager.IsModified);
    }

    [Fact]
    public void DirtyState_RedoAfterSavePoint_SetsIsModifiedToTrue()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        var action1 = new AddShapeAction(doc, circle, 0);
        doc.UndoManager.RecordAction(action1);
        doc.UndoManager.MarkSaved();

        circle.Radius = 12f;
        doc.UndoManager.Undo();
        Assert.False(doc.UndoManager.IsModified);

        doc.UndoManager.Redo();
        Assert.True(doc.UndoManager.IsModified);
    }

    [Fact]
    public void DirtyState_NewEditAfterSave_SetsIsModifiedToTrue()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        doc.UndoManager.MarkSaved();
        Assert.False(doc.UndoManager.IsModified);

        var rect = new RectangleShape(0f, 0f, 0f, 10f, 10f);
        doc.AddShape(rect);
        doc.UndoManager.RecordAction(new AddShapeAction(doc, rect, 1));

        Assert.True(doc.UndoManager.IsModified);
    }

    [Fact]
    public void StackLimit_ExceedingMaxSteps_TrimsOldestActions()
    {
        var mgr = new UndoRedoManager { MaxUndoSteps = 3 };

        var a1 = new DummyAction("A1");
        var a2 = new DummyAction("A2");
        var a3 = new DummyAction("A3");
        var a4 = new DummyAction("A4");

        mgr.RecordAction(a1);
        mgr.RecordAction(a2);
        mgr.RecordAction(a3);
        mgr.RecordAction(a4);

        // Only 3 actions should remain (a2, a3, a4)
        Assert.True(mgr.CanUndo);
        Assert.Equal("A4", mgr.UndoActionName);

        mgr.Undo();
        Assert.Equal("A3", mgr.UndoActionName);

        mgr.Undo();
        Assert.Equal("A2", mgr.UndoActionName);

        mgr.Undo();
        Assert.False(mgr.CanUndo);
    }

    [Fact]
    public void NewAction_ClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        var a1 = new DummyAction("A1");
        var a2 = new DummyAction("A2");
        var a3 = new DummyAction("A3");

        mgr.RecordAction(a1);
        mgr.RecordAction(a2);

        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.RecordAction(a3);
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void SuppressRecording_NoActionsRecordedDuringScope()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        using (doc.UndoManager.SuppressRecording())
        {
            circle.Radius = 25f;
            circle.PositionX = 30f;
        }

        Assert.False(doc.UndoManager.CanUndo);
        Assert.Equal(25f, circle.Radius);
    }

    [Fact]
    public void IsPerformingUndoRedo_PreventsCircularRecording()
    {
        var doc = new ShapeDocument();
        var circle = new CircleShape(0f, 0f, 0f, 5f);
        doc.AddShape(circle);

        circle.Radius = 15f;
        Assert.True(doc.UndoManager.CanUndo);

        // When Undo executes, circle.Radius will be set back to 5.
        // This mutation should NOT record a new action on the undo stack.
        doc.UndoManager.Undo();

        Assert.False(doc.UndoManager.CanUndo);
        Assert.True(doc.UndoManager.CanRedo);
    }

    [Fact]
    public void CompositeAction_MultipleOperations_UndoneAtomically()
    {
        var doc = new ShapeDocument();
        var c1 = new CircleShape(0f, 0f, 0f, 5f);
        var c2 = new CircleShape(10f, 10f, 0f, 8f);
        doc.AddShape(c1);
        doc.AddShape(c2);

        var a1 = new ModifyShapeAction(c1, c1.Clone(), new CircleShape(0f, 0f, 0f, 20f), "Change C1");
        c1.Radius = 20f;
        var a2 = new ModifyShapeAction(c2, c2.Clone(), new CircleShape(10f, 10f, 0f, 30f), "Change C2");
        c2.Radius = 30f;

        var composite = new CompositeAction([a1, a2], "Batch resize");
        doc.UndoManager.RecordAction(composite);

        Assert.Equal(20f, c1.Radius);
        Assert.Equal(30f, c2.Radius);

        doc.UndoManager.Undo();

        Assert.Equal(5f, c1.Radius);
        Assert.Equal(8f, c2.Radius);

        doc.UndoManager.Redo();

        Assert.Equal(20f, c1.Radius);
        Assert.Equal(30f, c2.Radius);
    }

    [Fact]
    public void CopyAllFrom_Circle_RestoresAllProperties()
    {
        var orig = new CircleShape(1f, 2f, 3f, 10f, 72)
        {
            Name = "MyCircle",
            LayerId = 3,
            CutType = SegmentType.Rapid
        };
        orig.Hatch.IsEnabled = true;
        orig.Hatch.Pattern = HatchPatternType.Spiral;
        orig.Hatch.Stepover = 1.5f;

        var target = new CircleShape(0f, 0f, 0f, 5f);
        target.CopyAllFrom(orig);

        Assert.Equal("MyCircle", target.Name);
        Assert.Equal(1f, target.PositionX);
        Assert.Equal(2f, target.PositionY);
        Assert.Equal(3f, target.PositionZ);
        Assert.Equal(10f, target.Radius);
        Assert.Equal(72, target.SegmentsCount);
        Assert.Equal(3, target.LayerId);
        Assert.Equal(SegmentType.Rapid, target.CutType);
        Assert.True(target.Hatch.IsEnabled);
        Assert.Equal(HatchPatternType.Spiral, target.Hatch.Pattern);
        Assert.Equal(1.5f, target.Hatch.Stepover);
    }

    [Fact]
    public void CopyAllFrom_Rectangle_RestoresAllProperties()
    {
        var orig = new RectangleShape(5f, 6f, 7f, 50f, 30f, 45f)
        {
            Name = "MyRect",
            LayerId = 2
        };

        var target = new RectangleShape(0f, 0f, 0f, 10f, 10f);
        target.CopyAllFrom(orig);

        Assert.Equal("MyRect", target.Name);
        Assert.Equal(5f, target.PositionX);
        Assert.Equal(6f, target.PositionY);
        Assert.Equal(7f, target.PositionZ);
        Assert.Equal(50f, target.Width);
        Assert.Equal(30f, target.Height);
        Assert.Equal(45f, target.RotationDegrees);
        Assert.Equal(2, target.LayerId);
    }

    [Fact]
    public void CopyAllFrom_Polygon_RestoresAllProperties()
    {
        var orig = new PolygonShape(2f, 3f, 4f, 15f, 6, 30f)
        {
            Name = "MyHexagon"
        };

        var target = new PolygonShape(0f, 0f, 0f, 5f, 3);
        target.CopyAllFrom(orig);

        Assert.Equal("MyHexagon", target.Name);
        Assert.Equal(2f, target.PositionX);
        Assert.Equal(3f, target.PositionY);
        Assert.Equal(4f, target.PositionZ);
        Assert.Equal(15f, target.Radius);
        Assert.Equal(6, target.Sides);
        Assert.Equal(30f, target.RotationDegrees);
    }

    [Fact]
    public void CopyAllFrom_Line_RestoresAllProperties()
    {
        var orig = new LineShape(1f, 2f, 3f, 10f, 20f, 30f)
        {
            Name = "MyLine"
        };

        var target = new LineShape(0f, 0f, 0f, 1f, 1f, 1f);
        target.CopyAllFrom(orig);

        Assert.Equal("MyLine", target.Name);
        Assert.Equal(1f, target.PositionX);
        Assert.Equal(2f, target.PositionY);
        Assert.Equal(3f, target.PositionZ);
        Assert.Equal(10f, target.EndX);
        Assert.Equal(20f, target.EndY);
        Assert.Equal(30f, target.EndZ);
    }

    [Fact]
    public void CopyAllFrom_Path_RestoresAllPropertiesAndContours()
    {
        var contour = new PathContour([new ToolpathPoint(0f, 0f, 0f), new ToolpathPoint(10f, 0f, 0f), new ToolpathPoint(10f, 10f, 0f)], isClosed: true);
        var orig = new PathShape(5f, 5f, 0f, [contour])
        {
            Name = "MyPath"
        };

        var target = new PathShape(0f, 0f, 0f, []);
        target.CopyAllFrom(orig);

        Assert.Equal("MyPath", target.Name);
        Assert.Equal(5f, target.PositionX);
        Assert.Equal(5f, target.PositionY);
        Assert.Single(target.Contours);
        Assert.Equal(3, target.Contours[0].PointsCount);
        Assert.True(target.Contours[0].IsClosed);
    }

    [Fact]
    public void CopyAllFrom_Text_RestoresAllPropertiesAndRebuildsContours()
    {
        var orig = new TextShape(10f, 20f, 0f, "TEST", "Arial", 14f, isBold: true, isItalic: true, letterSpacing: 2f, rotationDegrees: 30f)
        {
            Name = "MyText"
        };

        var target = new TextShape(0f, 0f, 0f, "ABC");
        target.CopyAllFrom(orig);

        Assert.Equal("MyText", target.Name);
        Assert.Equal(10f, target.PositionX);
        Assert.Equal(20f, target.PositionY);
        Assert.Equal("TEST", target.Text);
        Assert.Equal("Arial", target.FontFamily);
        Assert.Equal(14f, target.FontSize);
        Assert.True(target.IsBold);
        Assert.True(target.IsItalic);
        Assert.Equal(2f, target.LetterSpacing);
        Assert.Equal(30f, target.RotationDegrees);
    }

    [Fact]
    public void DuplicateShape_Undo_RemovesDuplicatedShape()
    {
        var doc = new ShapeDocument();
        var orig = new CircleShape(0f, 0f, 0f, 10f) { Name = "Circle" };
        doc.AddShape(orig);

        var copy = orig.Clone();
        copy.Translate(2f, 2f, 0f);
        doc.AddShape(copy);

        var action = new AddShapeAction(doc, copy, doc.Shapes.Count - 1, $"Duplicate {orig.Name}");
        doc.UndoManager.RecordAction(action);

        Assert.Equal(2, doc.Shapes.Count);
        Assert.Equal("Duplicate Circle", doc.UndoManager.UndoActionName);

        doc.UndoManager.Undo();
        Assert.Single(doc.Shapes);
        Assert.Same(orig, doc.Shapes[0]);
    }

    private sealed class DummyAction(string description) : IUndoableAction
    {
        public string Description { get; } = description;
        public int UndoCount { get; private set; }
        public int RedoCount { get; private set; }

        public void Undo() => UndoCount++;
        public void Redo() => RedoCount++;
    }
}
