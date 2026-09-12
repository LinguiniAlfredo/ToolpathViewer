using AblationStudio.Core.Models;

namespace AblationStudio.Core.Simulation;

public enum SimulationState
{
    Stopped,
    Playing,
    Paused,
    Completed
}

public sealed class ToolpathSimulator
{
    private readonly Toolpath _toolpath;
    private readonly float[] _cumulativeDistances;
    private float _feedrate = 50.0f; // mm/s default
    private float _currentDistance;
    private SimulationState _state = SimulationState.Stopped;

    public Toolpath Toolpath => _toolpath;
    public SimulationState State => _state;
    public float TotalDistance => _toolpath.Statistics.TotalLength;

    public float Feedrate
    {
        get => _feedrate;
        set => _feedrate = MathF.Max(0.1f, value);
    }

    public float CurrentDistance => _currentDistance;

    public float ProgressFraction => TotalDistance > 1e-6f
        ? Math.Clamp(_currentDistance / TotalDistance, 0f, 1f)
        : 0f;

    public ToolpathPoint CurrentPosition { get; private set; } = ToolpathPoint.Zero;
    public SegmentType CurrentSegmentType { get; private set; } = SegmentType.Rapid;
    public int CurrentSegmentIndex { get; private set; }

    public ToolpathSimulator(Toolpath toolpath)
    {
        _toolpath = toolpath;

        if (toolpath.Segments.Count == 0)
        {
            _cumulativeDistances = [];
            CurrentPosition = ToolpathPoint.Zero;
            return;
        }

        _cumulativeDistances = new float[toolpath.Segments.Count + 1];
        _cumulativeDistances[0] = 0f;

        for (int i = 0; i < toolpath.Segments.Count; i++)
        {
            _cumulativeDistances[i + 1] = _cumulativeDistances[i] + toolpath.Segments[i].Length;
        }

        CurrentPosition = toolpath.Segments[0].Start;
        CurrentSegmentType = toolpath.Segments[0].Type;
        CurrentSegmentIndex = 0;
    }

    public void Play()
    {
        if (_toolpath.Segments.Count == 0)
        {
            return;
        }

        if (_state == SimulationState.Completed || _currentDistance >= TotalDistance - 1e-5f)
        {
            _currentDistance = 0f;
            UpdatePositionFromDistance(0f);
        }

        _state = SimulationState.Playing;
    }

    public void Pause()
    {
        if (_state == SimulationState.Playing)
        {
            _state = SimulationState.Paused;
        }
    }

    public void TogglePlay()
    {
        if (_state == SimulationState.Playing)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    public void Stop()
    {
        _state = SimulationState.Stopped;
        _currentDistance = 0f;

        if (_toolpath.Segments.Count > 0)
        {
            CurrentPosition = _toolpath.Segments[0].Start;
            CurrentSegmentType = _toolpath.Segments[0].Type;
            CurrentSegmentIndex = 0;
        }
        else
        {
            CurrentPosition = ToolpathPoint.Zero;
        }
    }

    public void SeekToFraction(float fraction)
    {
        float clampedFraction = Math.Clamp(fraction, 0f, 1f);
        SeekToDistance(clampedFraction * TotalDistance);
    }

    public void SeekToDistance(float distance)
    {
        _currentDistance = Math.Clamp(distance, 0f, TotalDistance);

        if (_currentDistance >= TotalDistance && TotalDistance > 0f)
        {
            _state = SimulationState.Completed;
        }
        else if ((_state == SimulationState.Completed || _state == SimulationState.Stopped) && _currentDistance > 0f)
        {
            _state = SimulationState.Paused;
        }

        UpdatePositionFromDistance(_currentDistance);
    }

    public bool Update(float deltaSeconds)
    {
        if (_state != SimulationState.Playing || _toolpath.Segments.Count == 0)
        {
            return false;
        }

        float advance = _feedrate * MathF.Max(0f, deltaSeconds);
        _currentDistance += advance;

        if (_currentDistance >= TotalDistance)
        {
            _currentDistance = TotalDistance;
            _state = SimulationState.Completed;
            ToolpathSegment lastSeg = _toolpath.Segments[^1];
            CurrentPosition = lastSeg.End;
            CurrentSegmentType = lastSeg.Type;
            CurrentSegmentIndex = _toolpath.Segments.Count - 1;
            return true;
        }

        UpdatePositionFromDistance(_currentDistance);
        return true;
    }

    private void UpdatePositionFromDistance(float distance)
    {
        if (_toolpath.Segments.Count == 0)
        {
            CurrentPosition = ToolpathPoint.Zero;
            return;
        }

        int idx = Array.BinarySearch(_cumulativeDistances, distance);
        int segmentIndex;

        if (idx >= 0)
        {
            segmentIndex = Math.Min(idx, _toolpath.Segments.Count - 1);
        }
        else
        {
            int complement = ~idx;
            segmentIndex = Math.Clamp(complement - 1, 0, _toolpath.Segments.Count - 1);
        }

        ToolpathSegment segment = _toolpath.Segments[segmentIndex];
        CurrentSegmentIndex = segmentIndex;
        CurrentSegmentType = segment.Type;

        float segStartDist = _cumulativeDistances[segmentIndex];
        float segLength = segment.Length;
        float t = segLength > 1e-6f ? Math.Clamp((distance - segStartDist) / segLength, 0f, 1f) : 1f;

        float x = segment.Start.X + (segment.End.X - segment.Start.X) * t;
        float y = segment.Start.Y + (segment.End.Y - segment.Start.Y) * t;
        float z = segment.Start.Z + (segment.End.Z - segment.Start.Z) * t;

        CurrentPosition = new ToolpathPoint(x, y, z);
    }
}
