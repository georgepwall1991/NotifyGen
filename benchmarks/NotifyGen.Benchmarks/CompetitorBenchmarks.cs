using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using NotifyGen.Benchmarks.Models;

namespace NotifyGen.Benchmarks;

/// <summary>
/// Benchmarks comparing NotifyGen against other popular INPC implementations:
/// - CommunityToolkit.Mvvm (Microsoft's source generator)
/// - Prism (BindableBase with SetProperty)
/// - Fody PropertyChanged (IL weaving)
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[SimpleJob(RuntimeMoniker.Net100)]
[MemoryDiagnoser]
[RankColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CompetitorBenchmarks
{
    private NotifyGenViewModel _notifyGen = null!;
    private CommunityToolkitViewModel _communityToolkit = null!;
    private PrismViewModel _prism = null!;
    private FodyViewModel _fody = null!;
    private int _counter;

    [GlobalSetup]
    public void Setup()
    {
        _notifyGen = new NotifyGenViewModel();
        _communityToolkit = new CommunityToolkitViewModel();
        _prism = new PrismViewModel();
        _fody = new FodyViewModel();
        _counter = 0;
    }

    #region String Property Setters

    [BenchmarkCategory("StringSetter"), Benchmark(Baseline = true)]
    public void NotifyGen_StringSetter()
    {
        _notifyGen.Name = $"Name{_counter++}";
    }

    [BenchmarkCategory("StringSetter"), Benchmark]
    public void CommunityToolkit_StringSetter()
    {
        _communityToolkit.Name = $"Name{_counter++}";
    }

    [BenchmarkCategory("StringSetter"), Benchmark]
    public void Prism_StringSetter()
    {
        _prism.Name = $"Name{_counter++}";
    }

    [BenchmarkCategory("StringSetter"), Benchmark]
    public void Fody_StringSetter()
    {
        _fody.Name = $"Name{_counter++}";
    }

    #endregion

    #region Int Property Setters

    [BenchmarkCategory("IntSetter"), Benchmark(Baseline = true)]
    public void NotifyGen_IntSetter()
    {
        _notifyGen.Age = _counter++;
    }

    [BenchmarkCategory("IntSetter"), Benchmark]
    public void CommunityToolkit_IntSetter()
    {
        _communityToolkit.Age = _counter++;
    }

    [BenchmarkCategory("IntSetter"), Benchmark]
    public void Prism_IntSetter()
    {
        _prism.Age = _counter++;
    }

    [BenchmarkCategory("IntSetter"), Benchmark]
    public void Fody_IntSetter()
    {
        _fody.Age = _counter++;
    }

    #endregion

    #region Equality Guard (Same Value - No Event)

    [BenchmarkCategory("EqualityGuard"), Benchmark(Baseline = true)]
    public void NotifyGen_EqualityGuard()
    {
        _notifyGen.Name = _notifyGen.Name;
    }

    [BenchmarkCategory("EqualityGuard"), Benchmark]
    public void CommunityToolkit_EqualityGuard()
    {
        _communityToolkit.Name = _communityToolkit.Name;
    }

    [BenchmarkCategory("EqualityGuard"), Benchmark]
    public void Prism_EqualityGuard()
    {
        _prism.Name = _prism.Name;
    }

    [BenchmarkCategory("EqualityGuard"), Benchmark]
    public void Fody_EqualityGuard()
    {
        _fody.Name = _fody.Name;
    }

    #endregion

    #region Property Getters

    [BenchmarkCategory("Getter"), Benchmark(Baseline = true)]
    public string NotifyGen_Getter()
    {
        return _notifyGen.Name;
    }

    [BenchmarkCategory("Getter"), Benchmark]
    public string CommunityToolkit_Getter()
    {
        return _communityToolkit.Name;
    }

    [BenchmarkCategory("Getter"), Benchmark]
    public string Prism_Getter()
    {
        return _prism.Name;
    }

    [BenchmarkCategory("Getter"), Benchmark]
    public string Fody_Getter()
    {
        return _fody.Name;
    }

    #endregion
}
