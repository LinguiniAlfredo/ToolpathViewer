using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AblationStudio.Core.Shapes;

public sealed class HatchSettings : INotifyPropertyChanged
{
    public const float MinStepover = 0.01f;

    private bool _isEnabled;
    private HatchPatternType _pattern = HatchPatternType.ZigZag;
    private float _stepover = 0.5f;
    private float _angleDegrees;
    private bool _crossHatch;
    private bool _keepBoundary = true;
    private int _lineSkip = 1;
    private bool _autoLineSkip;
    private bool _spiralInward;
    private bool _followProfileOutward;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? PropertyChanging;
    public event Action? SettingsChanged;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnSettingsModified();
            }
        }
    }

    public HatchPatternType Pattern
    {
        get => _pattern;
        set
        {
            if (SetProperty(ref _pattern, value))
            {
                OnSettingsModified();
            }
        }
    }

    public float Stepover
    {
        get => _stepover;
        set
        {
            float val = MathF.Max(MinStepover, value);
            if (SetProperty(ref _stepover, val))
            {
                OnSettingsModified();
            }
        }
    }

    public float AngleDegrees
    {
        get => _angleDegrees;
        set
        {
            float val = (value % 360f + 360f) % 360f;
            if (SetProperty(ref _angleDegrees, val))
            {
                OnSettingsModified();
            }
        }
    }

    public bool CrossHatch
    {
        get => _crossHatch;
        set
        {
            if (SetProperty(ref _crossHatch, value))
            {
                OnSettingsModified();
            }
        }
    }

    public bool KeepBoundary
    {
        get => _keepBoundary;
        set
        {
            if (SetProperty(ref _keepBoundary, value))
            {
                OnSettingsModified();
            }
        }
    }

    public int LineSkip
    {
        get => _lineSkip;
        set
        {
            int val = Math.Max(1, value);
            if (SetProperty(ref _lineSkip, val))
            {
                OnSettingsModified();
            }
        }
    }

    public bool AutoLineSkip
    {
        get => _autoLineSkip;
        set
        {
            if (SetProperty(ref _autoLineSkip, value))
            {
                OnSettingsModified();
            }
        }
    }

    public bool SpiralInward
    {
        get => _spiralInward;
        set
        {
            if (SetProperty(ref _spiralInward, value))
            {
                OnSettingsModified();
            }
        }
    }

    public bool FollowProfileOutward
    {
        get => _followProfileOutward;
        set
        {
            if (SetProperty(ref _followProfileOutward, value))
            {
                OnSettingsModified();
            }
        }
    }

    public HatchSettings Clone() => new()
    {
        _isEnabled = _isEnabled,
        _pattern = _pattern,
        _stepover = _stepover,
        _angleDegrees = _angleDegrees,
        _crossHatch = _crossHatch,
        _keepBoundary = _keepBoundary,
        _lineSkip = _lineSkip,
        _autoLineSkip = _autoLineSkip,
        _spiralInward = _spiralInward,
        _followProfileOutward = _followProfileOutward
    };

    public void CopyFrom(HatchSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _isEnabled = other.IsEnabled;
        _pattern = other.Pattern;
        _stepover = other.Stepover;
        _angleDegrees = other.AngleDegrees;
        _crossHatch = other.CrossHatch;
        _keepBoundary = other.KeepBoundary;
        _lineSkip = other.LineSkip;
        _autoLineSkip = other.AutoLineSkip;
        _spiralInward = other.SpiralInward;
        _followProfileOutward = other.FollowProfileOutward;

        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(Pattern));
        OnPropertyChanged(nameof(Stepover));
        OnPropertyChanged(nameof(AngleDegrees));
        OnPropertyChanged(nameof(CrossHatch));
        OnPropertyChanged(nameof(KeepBoundary));
        OnPropertyChanged(nameof(LineSkip));
        OnPropertyChanged(nameof(AutoLineSkip));
        OnPropertyChanged(nameof(SpiralInward));
        OnPropertyChanged(nameof(FollowProfileOutward));
        OnSettingsModified();
    }

    private void OnSettingsModified()
    {
        SettingsChanged?.Invoke();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        PropertyChanging?.Invoke(propertyName ?? string.Empty);
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
