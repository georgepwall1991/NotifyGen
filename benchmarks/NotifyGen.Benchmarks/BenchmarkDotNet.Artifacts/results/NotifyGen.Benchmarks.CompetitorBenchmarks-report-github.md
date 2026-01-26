```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D5101c) [Darwin 25.3.0]
Apple M4, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]   : .NET 9.0.12 (9.0.1225.60609), Arm64 RyuJIT AdvSIMD
  .NET 9.0 : .NET 9.0.12 (9.0.1225.60609), Arm64 RyuJIT AdvSIMD

Job=.NET 9.0  Runtime=.NET 9.0  

```
| Method                         | Categories    | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Rank | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |-------------- |-----------:|----------:|----------:|-----------:|------:|--------:|-----:|-------:|----------:|------------:|
| NotifyGen_EqualityGuard        | EqualityGuard |  0.0732 ns | 0.0144 ns | 0.0135 ns |  0.0689 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| CommunityToolkit_EqualityGuard | EqualityGuard |  0.5664 ns | 0.0064 ns | 0.0050 ns |  0.5649 ns |  8.13 |    1.47 |    4 |      - |         - |          NA |
| Prism_EqualityGuard            | EqualityGuard |  0.4869 ns | 0.0124 ns | 0.0110 ns |  0.4851 ns |  6.94 |    1.28 |    3 |      - |         - |          NA |
| Fody_EqualityGuard             | EqualityGuard |  0.4601 ns | 0.0027 ns | 0.0022 ns |  0.4595 ns |  6.65 |    1.13 |    2 |      - |         - |          NA |
|                                |               |            |           |           |            |       |         |      |        |           |             |
| NotifyGen_Getter               | Getter        |  0.0023 ns | 0.0052 ns | 0.0043 ns |  0.0000 ns |     ? |       ? |    1 |      - |         - |           ? |
| CommunityToolkit_Getter        | Getter        |  0.0082 ns | 0.0100 ns | 0.0093 ns |  0.0100 ns |     ? |       ? |    1 |      - |         - |           ? |
| Prism_Getter                   | Getter        |  0.0439 ns | 0.0301 ns | 0.0335 ns |  0.0531 ns |     ? |       ? |    2 |      - |         - |           ? |
| Fody_Getter                    | Getter        |  0.0010 ns | 0.0026 ns | 0.0023 ns |  0.0000 ns |     ? |       ? |    1 |      - |         - |           ? |
|                                |               |            |           |           |            |       |         |      |        |           |             |
| NotifyGen_IntSetter            | IntSetter     |  0.3767 ns | 0.0029 ns | 0.0024 ns |  0.3765 ns |  1.00 |    0.00 |    1 |      - |         - |          NA |
| CommunityToolkit_IntSetter     | IntSetter     |  0.8316 ns | 0.0031 ns | 0.0026 ns |  0.8325 ns |  2.21 |    0.02 |    3 |      - |         - |          NA |
| Prism_IntSetter                | IntSetter     |  4.7715 ns | 0.0250 ns | 0.0209 ns |  4.7629 ns | 12.67 |    0.09 |    4 | 0.0029 |      24 B |          NA |
| Fody_IntSetter                 | IntSetter     |  0.4812 ns | 0.0024 ns | 0.0020 ns |  0.4806 ns |  1.28 |    0.01 |    2 |      - |         - |          NA |
|                                |               |            |           |           |            |       |         |      |        |           |             |
| NotifyGen_StringSetter         | StringSetter  | 16.6758 ns | 0.0271 ns | 0.0240 ns | 16.6676 ns |  1.00 |    0.00 |    1 | 0.0057 |      48 B |        1.00 |
| CommunityToolkit_StringSetter  | StringSetter  | 18.4551 ns | 0.0248 ns | 0.0207 ns | 18.4565 ns |  1.11 |    0.00 |    2 | 0.0057 |      48 B |        1.00 |
| Prism_StringSetter             | StringSetter  | 25.6044 ns | 0.1075 ns | 0.0898 ns | 25.5629 ns |  1.54 |    0.01 |    3 | 0.0086 |      72 B |        1.50 |
| Fody_StringSetter              | StringSetter  | 18.4390 ns | 0.0361 ns | 0.0302 ns | 18.4442 ns |  1.11 |    0.00 |    2 | 0.0057 |      48 B |        1.00 |
