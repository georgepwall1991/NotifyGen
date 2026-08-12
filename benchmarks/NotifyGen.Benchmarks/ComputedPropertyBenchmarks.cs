using System.ComponentModel;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using NotifyGen.Benchmarks.Models;

namespace NotifyGen.Benchmarks;

[SimpleJob(warmupCount: 3, iterationCount: 10)]
[MemoryDiagnoser]
[RankColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ComputedPropertyBenchmarks
{
    private NotifyGenComputedViewModel _computed = null!;
    private NotifyGenExplicitViewModel _explicit = null!;
    private CommunityToolkitComputedViewModel _toolkit = null!;
    private FodyComputedViewModel _fody = null!;
    private int _counter;

    [GlobalSetup]
    public void Setup()
    {
        _computed = new NotifyGenComputedViewModel();
        _explicit = new NotifyGenExplicitViewModel();
        _toolkit = new CommunityToolkitComputedViewModel();
        _fody = new FodyComputedViewModel();
        _computed.PropertyChanged += OnPropertyChanged;
        _explicit.PropertyChanged += OnPropertyChanged;
        _toolkit.PropertyChanged += OnPropertyChanged;
        _fody.PropertyChanged += OnPropertyChanged;
        _counter = 0;
    }

    private static void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
    }
    }

    [BenchmarkCategory("Changed"), Benchmark(Baseline = true)]
    public void NotifyGen_NotifyComputed()
    {
        _computed.FirstName = $"Ada{_counter++}";
    }

    [BenchmarkCategory("Changed"), Benchmark]
    public void NotifyGen_ExplicitNotifyAlso()
    {
        _explicit.FirstName = $"Ada{_counter++}";
    }

    [BenchmarkCategory("Changed"), Benchmark]
    public void CommunityToolkit_NotifyPropertyChangedFor()
    {
        _toolkit.FirstName = $"Ada{_counter++}";
    }

    [BenchmarkCategory("Changed"), Benchmark]
    public void Fody_DependsOn()
    {
        _fody.FirstName = $"Ada{_counter++}";
    }

    [BenchmarkCategory("SameValue"), Benchmark(Baseline = true)]
    public void NotifyGen_NotifyComputed_SameValue()
    {
        _computed.FirstName = _computed.FirstName;
    }

    [BenchmarkCategory("SameValue"), Benchmark]
    public void NotifyGen_ExplicitNotifyAlso_SameValue()
    {
        _explicit.FirstName = _explicit.FirstName;
    }

    [BenchmarkCategory("SameValue"), Benchmark]
    public void CommunityToolkit_SameValue()
    {
        _toolkit.FirstName = _toolkit.FirstName;
    }

    [BenchmarkCategory("SameValue"), Benchmark]
    public void Fody_SameValue()
    {
        _fody.FirstName = _fody.FirstName;
    }
}
