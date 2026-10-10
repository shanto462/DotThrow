using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using DotThrow.Benchmarks;

// Run benchmarks, for example:
//   dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*"
//   dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --filter "*Guard*" --job short
// Check that every variant computes the same result, without measuring:
//   dotnet run -c Release --project benchmarks/DotThrow.Benchmarks -- --check
if (args is ["--check"])
{
    foreach (var type in new[] { typeof(GuardBenchmarks), typeof(ValidationBenchmarks), typeof(CheckBenchmarks) })
    {
        var instance = Activator.CreateInstance(type)!;
        type.GetMethod("Setup")!.Invoke(instance, null);
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<BenchmarkAttribute>() is not null))
        {
            var category = method.GetCustomAttribute<BenchmarkCategoryAttribute>()!.Categories[0];
            var result = method.Invoke(instance, null);
            if (result is Task<int> task)
            {
                result = await task.ConfigureAwait(false);
            }

            Console.WriteLine($"{category,-40} {method.Name,-32} {result}");
        }
    }

    return;
}

BenchmarkSwitcher.FromAssembly(typeof(GuardBenchmarks).Assembly).Run(args);
