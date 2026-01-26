using System.ComponentModel;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace NotifyGen.Benchmarks;

/// <summary>
/// Benchmarks comparing NotifyGen generated setters vs hand-written implementations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class SetterBenchmarks
{
    private GeneratedViewModel _generated = null!;
    private HandWrittenViewModel _handWritten = null!;
    private int _counter;

    [GlobalSetup]
    public void Setup()
    {
        _generated = new GeneratedViewModel();
        _handWritten = new HandWrittenViewModel();
        _counter = 0;
    }

    [Benchmark(Baseline = true)]
    public void GeneratedSetter_String()
    {
        _generated.Name = $"Name{_counter++}";
    }

    [Benchmark]
    public void HandWrittenSetter_String()
    {
        _handWritten.Name = $"Name{_counter++}";
    }

    [Benchmark]
    public void GeneratedSetter_Int()
    {
        _generated.Age = _counter++;
    }

    [Benchmark]
    public void HandWrittenSetter_Int()
    {
        _handWritten.Age = _counter++;
    }

    [Benchmark]
    public void GeneratedSetter_SameValue_NoEvent()
    {
        _generated.Name = _generated.Name; // Should be no-op due to equality guard
    }

    [Benchmark]
    public void HandWrittenSetter_SameValue_NoEvent()
    {
        _handWritten.Name = _handWritten.Name; // Should be no-op due to equality guard
    }
}

/// <summary>
/// ViewModel using NotifyGen generated code.
/// </summary>
[Notify]
public partial class GeneratedViewModel
{
    private string _name = "";
    private int _age;
    private string? _email;
    private bool _isActive;
}

/// <summary>
/// Hand-written implementation for comparison.
/// </summary>
public class HandWrittenViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private int _age;
    private string? _email;
    private bool _isActive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_name, value)) return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public int Age
    {
        get => _age;
        set
        {
            if (EqualityComparer<int>.Default.Equals(_age, value)) return;
            _age = value;
            OnPropertyChanged();
        }
    }

    public string? Email
    {
        get => _email;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(_email, value)) return;
            _email = value;
            OnPropertyChanged();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (EqualityComparer<bool>.Default.Equals(_isActive, value)) return;
            _isActive = value;
            OnPropertyChanged();
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
