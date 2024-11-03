using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;

using Reaganism.FBI.Textual.Fuzzy.Diffing;
using Reaganism.FBI.Utilities;

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

    private static unsafe void DiffFileFbi(DifferSettings settings, string relativePath)
    {
        // Is this size excessive?
        const long max_file_bytes_for_stack = 1024 * 100;

        Utf16String originalText;
        {
            var originalPath = Path.Combine(settings.OriginalDirectory, relativePath).Replace('\\', '/');
            var originalInfo = new FileInfo(originalPath);
            if (originalInfo.Length <= max_file_bytes_for_stack)
            {
                using var fs = new FileStream(originalPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var pBytes = (Span<byte>)stackalloc byte[(int)originalInfo.Length];
                var pChars = (Span<char>)stackalloc char[(int)originalInfo.Length];
                _ = fs.Read(pBytes);
                Encoding.UTF8.GetChars(pBytes, pChars);

                originalText = Utf16String.FromSpan(pChars);
            }
            else
            {
                originalText = Utf16String.FromString(File.ReadAllText(originalPath));
            }
        }

        Utf16String modifiedText;
        {
            var modifiedPath = Path.Combine(settings.ModifiedDirectory, relativePath).Replace('\\', '/');
            var modifiedInfo = new FileInfo(modifiedPath);
            if (modifiedInfo.Length <= max_file_bytes_for_stack)
            {
                using var fs = new FileStream(modifiedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var pBytes = (Span<byte>)stackalloc byte[(int)modifiedInfo.Length];
                var pChars = (Span<char>)stackalloc char[(int)modifiedInfo.Length];
                _ = fs.Read(pBytes);
                Encoding.UTF8.GetChars(pBytes, pChars);

                modifiedText = Utf16String.FromSpan(pChars);
            }
            else
            {
                modifiedText = Utf16String.FromString(File.ReadAllText(modifiedPath));
            }
        }

        _ = FuzzyDiffer.DiffTexts(
            new LineMatchedDiffer(),
            SplitText(originalText),
            SplitText(modifiedText)
        );
    }

    private static unsafe List<Utf16String> SplitText(Utf16String text)
    {
        var span = text.Span;

        var lineCount = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (span[i] == '\n')
            {
                lineCount++;
            }
        }

        var ranges = (Span<Range>)stackalloc Range[lineCount];
        if (span.Split(ranges, '\n') != ranges.Length)
        {
            throw new Exception("Line count mismatch");
        }

        var result = new List<Utf16String>(ranges.Length);

        for (var i = 0; i < ranges.Length; i++)
        {
            var (start, length) = ranges[i].GetOffsetAndLength(text.Length);

            if (span[start + length - 1] == '\r')
            {
                length--;
            }
            else if (start + length > 2 && span[start + length - 2] == '\r' && span[start + length - 1] == '\n')
            {
                length -= 2;
            }

            result.Add(text.Slice(start, length));
        }

        return result;
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

[MemoryDiagnoser]
public class TerrariaSourceCodeSingleProjectDiffBenchmark
{
    private static readonly DifferSettings settings = new(
        "TerrariaClientWindows",
        "TerrariaClientLinux"
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

[MemoryDiagnoser]
public class TerrariaSourceCodeMultipleProjectsDiffBenchmark
{
    private static readonly DifferSettings[] settings =
    [
        new(
            "TerrariaClientWindows",
            "TerrariaClientLinux"
        ),
        new(
            "TerrariaClientWindows",
            "TerrariaClientMac"
        ),
        new(
            "TerrariaClientWindows",
            "TerrariaServerWindows"
        ),
        new(
            "TerrariaServerWindows",
            "TerrariaServerLinux"
        ),
        new(
            "TerrariaServerWindows",
            "TerrariaServerMac"
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