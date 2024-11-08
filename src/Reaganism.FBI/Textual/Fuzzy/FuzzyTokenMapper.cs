using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     Maps words (tokens) to unique integer IDs; also handles entire lines.
/// </summary>
public sealed class FuzzyTokenMapper
{
    [PublicAPI]
    public int MaxLineId => idToLineCount;

    [PublicAPI]
    public int MaxWordId => idToWord.Count;

    private readonly Dictionary<Utf16String, ushort> lineToId = [];

    private readonly List<Utf16String>       idToWord = [];
    private readonly Dictionary<int, ushort> wordToid = [];

    private readonly Dictionary<Utf16String, string> wordsToIdsCache = [];

    private ushort idToLineCount = 0x80 + 1;
    private char[] buf           = new char[4096];

    /// <summary>
    ///     Adds a line to the mapper and returns its unique identifier.
    /// </summary>
    /// <param name="line">The line to add.</param>
    /// <returns>The unique ID for the line.</returns>
    [PublicAPI]
    public ushort AddLine(Utf16String line)
    {
        if (lineToId.TryGetValue(line, out var id))
        {
            return id;
        }

        lineToId.Add(line, id = idToLineCount++);
        return id;
    }

    [PublicAPI]
    public ushort AddWord(Utf16String word)
    {
        var span = word.Span;

        if (word.Length == 1 && span[0] <= 0x80)
        {
            // Use ASCII characters as-is.
            return span[0];
        }

        var hash = word.GetHashCode();
        if (wordToid.TryGetValue(hash, out var id))
        {
            return id;
        }

        wordToid.Add(hash, id = (ushort)idToWord.Count);
        idToWord.Add(word);
        return id;
    }

    [PublicAPI]
    public string WordsToIds(Utf16String line)
    {
        if (wordsToIdsCache.TryGetValue(line, out var cached))
        {
            return cached;
        }

        var bufLength = 0;
        var length    = line.Length;
        var span      = line.Span;
        for (var i = 0; i < length;)
        {
            var start = i;
            var curr  = span[i++];

            // Search for different "word" types: words, numbers, whitespace,
            // and symbols.
            if (char.IsLetter(curr))
            {
                // If we start with a character, begin resolving an entire word.
                // A word must start with a letter and may contain letters or
                // digits.
                while (i < length && char.IsLetterOrDigit(span[i]))
                {
                    i++;
                }
            }
            else if (char.IsDigit(curr))
            {
                // If we start with a digit, begin resolving an entire number.
                // A number must start with a digit and may contain only digits.
                while (i < length && char.IsDigit(span[i]))
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
                while (i < length && span[i] == curr)
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

                buf[bufLength++] = (char)AddWord(line[start..i]);
            }
        }

        return wordsToIdsCache[line] = new string(buf, 0, bufLength);
    }

    public string WordsToIds(string line)
    {
        return WordsToIds(Utf16String.FromString(line));
    }

    /// <summary>
    ///     Converts a collection of lines into a string of identifiers.
    /// </summary>
    /// <param name="lines">The collection of lines to convert.</param>
    [PublicAPI]
    public ushort[] LinesToIds(IEnumerable<Utf16String> lines)
    {
        return lines.Select(AddLine).ToArray();
    }

    /// <summary>
    ///     Converts a collection of lines into a string of identifiers.
    /// </summary>
    /// <param name="lines">The collection of lines to convert.</param>
    [PublicAPI]
    public ushort[] LinesToIds(IEnumerable<string> lines)
    {
        return lines.Select(x => AddLine(Utf16String.FromString(x))).ToArray();
    }

    /// <summary>
    ///     Retrieves the word corresponding to a given identifier.
    /// </summary>
    /// <param name="id">The identifier for the word.</param>
    /// <returns>The word associated with the identifier.</returns>
    [PublicAPI]
    public Utf16String GetWord(ushort id)
    {
        return idToWord[id];
    }
}