using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using JetBrains.Annotations;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     Maps lines and words (tokens) to unique integer IDs.
/// </summary>
[PublicAPI]
public sealed class TokenMapper
{
    /// <summary>
    ///     Simple struct for handling ranges. It is unlike <see cref="Range"/>
    ///     in that it contains integer indices (no wrapping struct) and does
    ///     not have the option to be relative to the end instead of the start.
    ///     <br />
    ///     It also provides a simpler <see cref="Length"/> property.
    /// </summary>
    /// <param name="Start">The start of the range.</param>
    /// <param name="End">The end of the range.</param>
    private readonly record struct SimpleRange(int Start, int End)
    {
        /// <summary>
        ///     The length of the range.
        /// </summary>
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
    private readonly Dictionary<string, ushort> lineToId = [];

    private readonly List<string>            idToWord = [];
    private readonly Dictionary<int, ushort> wordToId = [];

    private readonly Dictionary<string, string> wordsToIdsCache = [];

    private char[] buf = new char[4096];

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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort AddWord(string word)
    {
        return AddWord(word, new SimpleRange(0, word.Length));
    }

    private ushort AddWord(string word, SimpleRange range)
    {
        if (range.Length == 1 && word[range.Start] <= 0x80)
        {
            // Use ASCII characters as-is.
            return word[range.Start];
        }

#if false
        var hash = GetSlicedStringHashCode(word, range);
        {
            System.Diagnostics.Debug.Assert(
                GetSlicedStringHashCode(word, range) == word[range.Start..range.End].GetHashCode()
            );
        }
#else
        var hash = GetSlicedStringHashCode(word, range);
#endif

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
        if (wordsToIdsCache.TryGetValue(line, out var cached))
        {
            return cached;
        }

        var bufLength = 0;
        var length    = line.Length;
        for (var i = 0; i < length;)
        {
            var start = i;
            var curr  = line[i++];

            // Search for different "word" types: words, numbers, whitespace,
            // and symbols.
            if (char.IsLetter(curr))
            {
                // If we start with a character, begin resolving an entire word.
                // A word must start with a letter and may contain letters or
                // digits.
                while (i < length && char.IsLetterOrDigit(line, i))
                {
                    i++;
                }
            }
            else if (char.IsDigit(curr))
            {
                // If we start with a digit, begin resolving an entire number.
                // A number must start with a digit and may contain only digits.
                while (i < length && char.IsDigit(line, i))
                {
                    i++;
                }
            }
            else if (curr is ' ' or '\t')
            {
                // If we start with whitespace, begin resolving all contiguous
                // whitespace characters of that type.  To maintain
                // compatibility with Chicken-Bones/DiffPatch diffs, we only
                // handle spaces and tabs.
                while (i < length && line[i] == curr)
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
                if (bufLength >= buf.Length)
                {
                    Array.Resize(ref buf, buf.Length * 2);
                }

                buf[bufLength++] = (char)AddWord(line, new SimpleRange(start, i));
            }
        }

        return wordsToIdsCache[line] = new string(buf, 0, bufLength);
    }

    /// <summary>
    ///     Converts a collection of lines into a string of identifiers.
    /// </summary>
    /// <param name="lines">The collection of lines to convert.</param>
    /// <returns>A string of comma-separated identifiers.</returns>
    [PublicAPI]
    public string LinesToIds(IEnumerable<string> lines)
    {
        return new string(lines.Select(x => (char)AddLine(x)).ToArray());
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