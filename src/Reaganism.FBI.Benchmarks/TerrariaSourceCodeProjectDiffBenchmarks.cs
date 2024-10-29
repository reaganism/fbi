using System.Collections.Concurrent;
using System.Diagnostics;

using BenchmarkDotNet.Attributes;

using Reaganism.FBI.Diffing;

namespace Reaganism.FBI.Benchmarks;

internal readonly record struct DifferSettings(
    string OriginalDirectory,
    string ModifiedDirectory
);

internal static class DiffHelper
{
    private static readonly string[] non_src_directories = [".git", ".vs", ".idea", "bin", "obj"];

    public static void DiffFbi(DifferSettings settings)
    {
        var actions = new List<Action>();

        foreach (var (filePath, relativePath) in EnumerateSourceFiles(settings.ModifiedDirectory))
        {
            if (!File.Exists(Path.Combine(settings.OriginalDirectory, relativePath)))
            {
                continue;
            }

            actions.Add(() => DiffFileFbi(settings, relativePath));
        }

        Execute(actions);
    }

    public static void DiffCodeChicken(DifferSettings settings)
    {
        var actions = new List<Action>();

        foreach (var (filePath, relativePath) in EnumerateSourceFiles(settings.ModifiedDirectory))
        {
            if (!File.Exists(Path.Combine(settings.OriginalDirectory, relativePath)))
            {
                continue;
            }

            actions.Add(() => DiffFileCodeChicken(settings, relativePath));
        }

        Execute(actions);
    }

    private static void DiffFileFbi(DifferSettings settings, string relativePath)
    {
        _ = Differ.DiffFiles(
            new LineMatchedDiffer(),
            Path.Combine(settings.OriginalDirectory, relativePath).Replace('\\', '/'),
            Path.Combine(settings.ModifiedDirectory, relativePath).Replace('\\', '/')
        );
    }

    private static void DiffFileCodeChicken(DifferSettings settings, string relativePath)
    {
        _ = CodeChicken.DiffPatch.Differ.DiffFiles(
            new CodeChicken.DiffPatch.LineMatchedDiffer(),
            Path.Combine(settings.OriginalDirectory, relativePath).Replace('\\', '/'),
            Path.Combine(settings.ModifiedDirectory, relativePath).Replace('\\', '/')
        );
    }

    private static string GetRelativePath(string directory, string fullPath)
    {
        // Sanitization: remove or add directory separator character as needed.
        {
            if (fullPath[^1] == Path.DirectorySeparatorChar)
            {
                fullPath = fullPath[..^1];
            }

            if (directory[^1] != Path.DirectorySeparatorChar)
            {
                directory += Path.DirectorySeparatorChar;
            }
        }

        if (fullPath + Path.DirectorySeparatorChar == directory)
        {
            return string.Empty;
        }

        Debug.Assert(fullPath.StartsWith(directory));
        {
            return fullPath[directory.Length..];
        }
    }

    private static IEnumerable<(string fullPath, string relativePath)> EnumerateFiles(string directory)
    {
        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                        .Select(x => (fullPath: x, relativePath: GetRelativePath(directory, x)));
    }

    private static IEnumerable<(string fullPath, string relativePath)> EnumerateSourceFiles(string directory)
    {
        return EnumerateFiles(directory).Where(x => !x.relativePath.Split('/', '\\').Any(non_src_directories.Contains));
    }

    private static void Execute(List<Action> actions)
    {
        Parallel.ForEach(
            Partitioner.Create(actions, EnumerablePartitionerOptions.NoBuffering),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            x => x()
        );
    }
}

public class TerrariaSourceCodeSingleProjectDiffBenchmark
{
    private static readonly DifferSettings settings = new(
        "benchmark-files/terraria-sources/TerrariaClientWindows",
        "benchmark-files/terraria-sources/TerrariaClientLinux"
    );

    [Benchmark]
    public void DiffFbi()
    {
        DiffHelper.DiffFbi(settings);
    }

    [Benchmark]
    public void DiffCodeChicken()
    {
        DiffHelper.DiffCodeChicken(settings);
    }
}

public class TerrariaSourceCodeMultipleProjectsDiffBenchmark
{
    private static readonly DifferSettings[] settings =
    [
        new(
            "benchmark-files/terraria-sources/TerrariaClientWindows",
            "benchmark-files/terraria-sources/TerrariaClientLinux"
        ),
        new(
            "benchmark-files/terraria-sources/TerrariaClientWindows",
            "benchmark-files/terraria-sources/TerrariaClientMac"
        ),
        new(
            "benchmark-files/terraria-sources/TerrariaClientWindows",
            "benchmark-files/terraria-sources/TerrariaServerWindows"
        ),
        new(
            "benchmark-files/terraria-sources/TerrariaServerWindows",
            "benchmark-files/terraria-sources/TerrariaServerLinux"
        ),
        new(
            "benchmark-files/terraria-sources/TerrariaServerWindows",
            "benchmark-files/terraria-sources/TerrariaServerMac"
        ),
    ];

    [Benchmark]
    public void DiffFbi()
    {
        foreach (var setting in settings)
        {
            DiffHelper.DiffFbi(setting);
        }
    }

    [Benchmark]
    public void DiffCodeChicken()
    {
        foreach (var setting in settings)
        {
            DiffHelper.DiffCodeChicken(setting);
        }
    }
}