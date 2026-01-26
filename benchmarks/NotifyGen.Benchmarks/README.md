# NotifyGen Benchmarks

Performance benchmarks for the NotifyGen source generator using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Running Benchmarks

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific benchmark class
dotnet run -c Release -- --filter *SetterBenchmarks*
dotnet run -c Release -- --filter *GeneratorBenchmarks*

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
| `IncrementalRebuild_1ClassChange` | Time to rebuild when only 1 class changes |

**Expected result:** Incremental rebuilds should be significantly faster than full generation due to caching.

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
