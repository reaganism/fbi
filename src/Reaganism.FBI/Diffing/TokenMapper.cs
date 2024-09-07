using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     Maps lines and words (tokens) to unique integer IDs.
/// </summary>
[PublicAPI]
public sealed class TokenMapper
{
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

    private readonly List<string>               idToWord = [];
    private readonly Dictionary<string, ushort> wordToId = new();

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
    public ushort AddWord(string word)
    {
        if (word.Length == 1 && word[0] <= 0x80)
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

        foreach (var r in EnumerateWords(line))
        {
            var word = line[r];

            if (b >= buf.Length)
            {
                Array.Resize(ref buf, buf.Length * 2);
            }

            buf[b++] = (char)AddWord(word);
        }

        return new string(buf, 0, b);
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
}