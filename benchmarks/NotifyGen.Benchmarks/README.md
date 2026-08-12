# NotifyGen Benchmarks

Performance benchmarks for the NotifyGen source generator using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific benchmark class
dotnet run -c Release -f net10.0 -- --filter *SetterBenchmarks*
dotnet run -c Release -f net10.0 -- --filter *GeneratorBenchmarks*
dotnet run -c Release -f net10.0 -- --filter *ComputedProperty*

# Quick validation run
dotnet run -c Release -- --filter * --job short
```

## Benchmark Categories

### SetterBenchmarks

Compares NotifyGen-generated property setters against hand-written implementations:

| Benchmark | Description |
|-----------|-------------|
| `GeneratedSetter_String` | Generated setter for string property |
| `HandWrittenSetter_String` | Hand-written setter for comparison |
| `GeneratedSetter_Int` | Generated setter for int property |
| `HandWrittenSetter_Int` | Hand-written setter for comparison |
| `GeneratedSetter_SameValue_NoEvent` | Equality guard - no event when value unchanged |
| `HandWrittenSetter_SameValue_NoEvent` | Hand-written equality guard for comparison |

**Expected result:** Generated setters should have identical performance to hand-written code.

### GeneratorBenchmarks

Measures source generator compilation performance at different scales:

| Benchmark | Description |
|-----------|-------------|
| `Generate_1Class` | Generator execution time for 1 `[Notify]` class |
| `Generate_10Classes` | Generator execution time for 10 `[Notify]` classes |
| `Generate_100Classes` | Generator execution time for 100 `[Notify]` classes |
| `Generate_100Classes_WithNotifyComputed` | 100 types × 5 `[NotifyComputed]` properties |
| `Generate_100Classes_WithExplicitNotifyAlso` | Same graph written as `[NotifyAlso]` |
| `IncrementalRebuild_ComputedGetterChange` | Second pass after one computed getter body edit |
| `IncrementalRebuild_1ClassChange` | Time to rebuild when only 1 class changes |

**Expected result:** Incremental rebuilds should be significantly faster than full generation due to caching.

### ComputedPropertyBenchmarks

Runtime fan-out for a `FullName` dependent:

| Benchmark | Description |
|-----------|-------------|
| `NotifyGen_NotifyComputed` | `[NotifyComputed]` on `FullName` |
| `NotifyGen_ExplicitNotifyAlso` | Same graph via `[NotifyAlso]` |
| `CommunityToolkit_NotifyPropertyChangedFor` | Toolkit `[NotifyPropertyChangedFor]` |
| `Fody_DependsOn` | Fody `[DependsOn]` |
| `*_SameValue` | Equality guard — no events |

**Expected result:** NotifyComputed and explicit NotifyAlso allocate the same (shared emission path).

## Measured 2.1.0 results

Machine: Apple M4, macOS 27, .NET SDK 10.0.302 / runtime 10.0.10, BenchmarkDotNet 0.13.12, `IterationCount=10`, `WarmupCount=3`, `net10.0` Release. Do not treat these as CI gates.

### Runtime fan-out (`ComputedPropertyBenchmarks`)

| Method | Category | Mean | Allocated |
|--------|----------|-----:|----------:|
| NotifyGen_NotifyComputed | Changed | 35.72 ns | 48 B |
| NotifyGen_ExplicitNotifyAlso | Changed | 55.09 ns | 48 B |
| CommunityToolkit_NotifyPropertyChangedFor | Changed | 45.07 ns | 48 B |
| Fody_DependsOn | Changed | 44.33 ns | 48 B |
| NotifyGen_NotifyComputed_SameValue | SameValue | 0.20 ns | 0 B |
| NotifyGen_ExplicitNotifyAlso_SameValue | SameValue | 0.19 ns | 0 B |

Computed vs explicit share the `AlsoNotify` setter path and allocate identically. Means on this short job overlap once error bars are included (explicit error was ±11 ns).

### Generator (`GeneratorBenchmarks`)

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Generate_100Classes | 2.39 ms | 5,392 KB |
| Generate_100Classes_WithNotifyComputed | 5.35 ms | 8,175 KB |
| Generate_100Classes_WithExplicitNotifyAlso | 8.18 ms | 11,577 KB |
| IncrementalRebuild_ComputedGetterChange (generate + edit in one method) | 8.77 ms | 12,001 KB |

100-class `[NotifyComputed]` generate is **0.65×** the equivalent explicit-`[NotifyAlso]` compile (under the 1.25× budget). A standalone second-pass incremental run is noisy across BenchmarkDotNet process launches; treat the in-method generate+edit row as the comparable figure.

## Interpreting Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation
- **Allocated**: Memory allocated per operation (should be 0 for setters with same value)

## Example Output

```
BenchmarkDotNet v0.13.12, macOS
Apple M1 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 9.0.100

| Method                           | Mean      | Allocated |
|--------------------------------- |----------:|----------:|
| GeneratedSetter_String           |  12.34 ns |      32 B |
| HandWrittenSetter_String         |  12.31 ns |      32 B |
| GeneratedSetter_SameValue_NoEvent|   2.15 ns |       0 B |
```
