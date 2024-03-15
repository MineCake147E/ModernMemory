using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace ModernMemory.Benchmarks.Runner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher
            .FromAssembly(typeof(MoveMemoryBenchmarks).Assembly)
            .Run(args, DefaultConfig.Instance.WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(256)).AddDiagnoser(new DisassemblyDiagnoser(new(int.MaxValue)))
            );
            Console.Write("Press any key to exit:");
            Console.ReadKey();
        }
    }
}
