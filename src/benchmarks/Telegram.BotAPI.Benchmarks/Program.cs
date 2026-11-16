using BenchmarkDotNet.Running;

BenchmarkSwitcher
    .FromAssembly(typeof(DiscriminatorBenchmarks).Assembly)
    .Run(args);
