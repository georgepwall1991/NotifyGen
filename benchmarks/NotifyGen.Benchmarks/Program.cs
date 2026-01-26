using BenchmarkDotNet.Running;
using NotifyGen.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(SetterBenchmarks).Assembly).Run(args);
