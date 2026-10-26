```

BenchmarkDotNet v0.16.0-preview.2, Windows 11 (10.0.26200.9711/25H2/2025Update/HudsonValley2)
AMD Ryzen 7 5800X 3.80GHz, 1 CPU, 16 logical and 8 physical cores
Memory: 31,92 GB Total, 4,34 GB Available
.NET SDK 11.0.100-rc.3.26522.148
  [Host]             : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3
  ShortRun-.NET 10.0 : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v3
  ShortRun-.NET 11.0 : .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3

IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method      | Job                | Runtime   | Mean     | Error    | StdDev   | Gen0   | Allocated |
|------------ |------------------- |---------- |---------:|---------:|---------:|-------:|----------:|
| IngestBatch | ShortRun-.NET 10.0 | .NET 10.0 | 38.74 μs | 13.12 μs | 0.719 μs | 1.7090 |   28928 B |
| IngestBatch | ShortRun-.NET 11.0 | .NET 11.0 | 21.92 μs | 10.09 μs | 0.553 μs |      - |     144 B |
