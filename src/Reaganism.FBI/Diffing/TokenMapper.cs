using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     Maps lines and words (tokens) to unique integer IDs.
/// </summary>
[PublicAPI]
public sealed class TokenMapper
{
    private readonly record struct SimpleRange(int Start, int End)
    {
        public int Length => End - Start;
    }

    [PublicAPI]
    public int MaxLineId
    {
        [PublicAPI] get => idToLine.Count;
    }

    [PublicAPI]
    public int MaxWordId
    {
        [PublicAPI] get => idToWord.Count;
    }

    private readonly List<string>               idToLine = [..cached_lines_to_ids];
    private readonly Dictionary<string, ushort> lineToId = new();

    private readonly List<string>            idToWord = [];
    private readonly Dictionary<int, ushort> wordToId = new();

    private ushort[] buf = new ushort[4096];

    private static readonly string[] cached_lines_to_ids;

    [PublicAPI]
    public TokenMapper() { }

    static TokenMapper()
    {
        cached_lines_to_ids = new string[0x80 + 1];
        {
            // Add a sentinel value at index 0.
            cached_lines_to_ids[0] = "\0";

            // Add ASCII characters as-is.
            for (var i = 0; i < 0x80; i++)
            {
                cached_lines_to_ids[i + 1] = ((char)i).ToString();
            }
        }
    }

    /// <summary>
    ///     Adds a line to the mapper and returns its unique identifier.
    /// </summary>
    /// <param name="line">The line to add.</param>
    /// <returns>The unique ID for the line.</returns>
    [PublicAPI]
    public ushort AddLine(string line)
    {
        if (lineToId.TryGetValue(line, out var id))
        {
            return id;
        }

        lineToId.Add(line, id = (ushort)idToLine.Count);
        idToLine.Add(line);
        return id;
    }

    /// <summary>
    ///     Adds a word to the mapper and returns its unique identifier.
    /// </summary>
    /// <param name="word">The word to add.</param>
    /// <returns>The unique ID for the word.</returns>
    [PublicAPI]
    public ushort AddWord(string word)
    {
        /*if (word.Length == 1 && word[0] <= 0x80)
        {
            // Use ASCII characters as-is.
            return word[0];
        }

        if (wordToId.TryGetValue(word, out var id))
        {
            return id;
        }

        wordToId.Add(word, id = (ushort)idToWord.Count);
        idToWord.Add(word);
        return id;*/

        return AddWord(word, new SimpleRange(0, word.Length));
    }

    private ushort AddWord(string word, SimpleRange range)
    {
        if (range.Length == 1 && word[range.Start] <= 0x80)
        {
            // Use ASCII characters as-is.
            return word[range.Start];
        }

        var hash = GetSlicedStringHashCode(word, range);
        {
            Debug.Assert(GetSlicedStringHashCode(word, range) == word[range.Start..range.End].GetHashCode());
        }
        if (wordToId.TryGetValue(hash, out var id))
        {
            return id;
        }

        wordToId.Add(hash, id = (ushort)idToWord.Count);
        idToWord.Add(word[range.Start..range.End]);
        return id;
    }

    /// <summary>
    ///     Converts a line of text into a string of identifiers representing
    ///     its words.
    /// </summary>
    /// <param name="line">The line of text to convert.</param>
    /// <returns>A string of comma-separated identifiers.</returns>
    [PublicAPI]
    public string WordsToIds(string line)
    {
        var b = 0;

        // Inlined from EnumerateWords (see commented declaration for reasons).
        for (var i = 0; i < line.Length;)
        {
            var start = i;
            var c     = line[i++];

            // Search for different "word" types: words, numbers, whitespace,
            // and symbols.
            if (char.IsLetter(c))
            {
                // If we start with a character, begin resolving an entire word.
                // A word must start with a letter and may contain letters or
                // digits.
                while (i < line.Length && char.IsLetterOrDigit(line, i))
                {
                    i++;
                }
            }
            else if (char.IsDigit(c))
            {
                // If we start with a digit, begin resolving an entire number.
                // A number must start with a digit and may contain only digits.
                while (i < line.Length && char.IsDigit(line, i))
                {
                    i++;
                }
            }
            else if (c is ' ' or '\t')
            {
                // If we start with whitespace, begin resolving all contiguous
                // whitespace characters of that type.  To maintain
                // compatibility with Chicken-Bones/DiffPatch diffs, we only
                // handle spaces and tabs.
                while (i < line.Length && line[i] == c)
                {
                    i++;
                }
            }

            // Return the resolved range.  If a character is not a supported
            // whitespace character, a letter, or a digit, it also falls through
            // here.  This means that symbols will consist of only a single
            // character.
            // yield return new Range(start, i);
            {
                if (b >= buf.Length)
                {
                    Array.Resize(ref buf, buf.Length * 2);
                }

                buf[b++] = AddWord(line, new SimpleRange(start, i));
            }
        }

        return new string(Cast<ushort, char>(new ReadOnlySpan<ushort>(buf, 0, b)));
        // return new string(buf, 0, b);
    }

    /// <summary>
    ///     Converts a collection of lines into a string of identifiers.
    /// </summary>
    /// <param name="lines">The collection of lines to convert.</param>
    /// <returns>A string of comma-separated identifiers.</returns>
    [PublicAPI]
    public string LinesToIds(IEnumerable<string> lines)
    {
        return new string(lines.Select(AddLine).Select(x => (char)x).ToArray());
    }

    /// <summary>
    ///     Retrieves the word corresponding to a given identifier.
    /// </summary>
    /// <param name="id">The identifier for the word.</param>
    /// <returns>The word associated with the identifier.</returns>
    [PublicAPI]
    public string GetWord(ushort id)
    {
        return idToWord[id];
    }

    // EnumerateWords is inlined into WordsToIds directly to avoid heap
    // allocations and additional overhead (WordsToIds is an immensely hot code
    // path).
    /*
    /// <summary>
    ///     Enumerates the words in a line, yielding ranges for each word or
    ///     symbol.
    /// </summary>
    /// <param name="line">The line of text to process.</param>
    /// <returns>A collection of ranges representing words or symbols.</returns>
    private static IEnumerable<Range> EnumerateWords(string line)
    {
        for (var i = 0; i < line.Length;)
        {
            var start = i;
            var c     = line[i++];

            // Search for different "word" types: words, numbers, whitespace,
            // and symbols.
            if (char.IsLetter(c))
            {
                // If we start with a character, begin resolving an entire word.
                // A word must start with a letter and may contain letters or
                // digits.
                while (i < line.Length && char.IsLetterOrDigit(line, i))
                {
                    i++;
                }
            }
            else if (char.IsDigit(c))
            {
                // If we start with a digit, begin resolving an entire number.
                // A number must start with a digit and may contain only digits.
                while (i < line.Length && char.IsDigit(line, i))
                {
                    i++;
                }
            }
            else if (c is ' ' or '\t')
            {
                // If we start with whitespace, begin resolving all contiguous
                // whitespace characters of that type.  To maintain
                // compatibility with Chicken-Bones/DiffPatch diffs, we only
                // handle spaces and tabs.
                while (i < line.Length && line[i] == c)
                {
                    i++;
                }
            }

            // Return the resolved range.  If a character is not a supported
            // whitespace character, a letter, or a digit, it also falls through
            // here.  This means that symbols will consist of only a single
            // character.
            yield return new Range(start, i);
        }
    }
    */

    // Simplified implementation of MemoryMarshal::Cast for read-only spans
    // WITHOUT handling different sizes/lengths or ensuring types don't contain
    // references, since we only use it for converting ushort spans to char
    // spans, which are both blittable structs with the same size.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<TTo> Cast<TFrom, TTo>(ReadOnlySpan<TFrom> span)
        where TFrom : struct
        where TTo : struct
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<TFrom, TTo>(ref MemoryMarshal.GetReference(span)),
            span.Length
        );
    }

    // Mirrors the .NET 8 String::GetHashCode implementation but specifically
    // computes only the hash of a specific character range within the string.
    // We do this to avoid needing to allocate a string instance if the value
    // has already been previously added to a TokenMapper instance.
    private static unsafe int GetSlicedStringHashCode(string value, SimpleRange range)
    {
        var seed = Marvin.DefaultSeed;
        fixed (char* pValue = value)
        {
            var     pChar   = pValue + range.Start;
            ref var charRef = ref *pChar;

            return Marvin.ComputeHash32(
                ref Unsafe.As<char, byte>(ref charRef),
                (uint)range.Length * 2, // In bytes, not chars.
                (uint)seed,
                (uint)(seed >> 32)
            );
        }
    }
}