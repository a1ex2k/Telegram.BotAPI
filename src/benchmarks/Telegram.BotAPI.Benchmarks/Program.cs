using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(TelegramClientBenchmarks).Assembly).Run(args);
