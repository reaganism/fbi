using System;
using System.Collections.Generic;
using System.Linq;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     Maps lines and words (tokens) to unique integer IDs.
/// </summary>
public sealed class TokenMapper
{
    public int MaxLineId => lineToString.Count;

    public int MaxWordId => wordToString.Count;

    private readonly List<string>               lineToString = [];
    private readonly Dictionary<string, ushort> stringToLine = new();

    private readonly List<string>               wordToString = [];
    private readonly Dictionary<string, ushort> stringToWord = new();

    private char[] buf = new char[4096];

    public TokenMapper()
    {
        // Add a sentinel value at index 0.
        lineToString.Add("\0");

        // Add ASCII characters as-is.
        for (var i = 0; i < 0x80; i++)
        {
            wordToString.Add(((char)i).ToString());
        }
    }

    /// <summary>
    ///     Adds a line to the mapper and returns its unique identifier.
    /// </summary>
    /// <param name="line">The line to add.</param>
    /// <returns>The unique ID for the line.</returns>
    public ushort AddLine(string line)
    {
        if (stringToLine.TryGetValue(line, out var id))
        {
            return id;
        }

        stringToLine.Add(line, id = (ushort)lineToString.Count);
        lineToString.Add(line);
        return id;
    }

    /// <summary>
    ///     Adds a word to the mapper and returns its unique identifier.
    /// </summary>
    /// <param name="word">The word to add.</param>
    /// <returns>The unique ID for the word.</returns>
    public ushort AddWord(string word)
    {
        if (word.Length == 1 && word[0] <= 0x80)
        {
            // Use ASCII characters as-is.
            return word[0];
        }

        if (stringToWord.TryGetValue(word, out var id))
        {
            return id;
        }

        stringToWord.Add(word, id = (ushort)wordToString.Count);
        wordToString.Add(word);
        return id;
    }

    /// <summary>
    ///     Converts a line of text into a string of identifiers representing
    ///     its words.
    /// </summary>
    /// <param name="line">The line of text to convert.</param>
    /// <returns>A string of comma-separated identifiers.</returns>
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

        return string.Join(',', buf.Take(b));
    }

    /// <summary>
    ///     Converts a collection of lines into a string of identifiers.
    /// </summary>
    /// <param name="lines">The collection of lines to convert.</param>
    /// <returns>A string of comma-separated identifiers.</returns>
    public string LinesToIds(IEnumerable<string> lines)
    {
        return string.Join(',', lines.Select(AddLine));
    }

    /// <summary>
    ///     Retrieves the word corresponding to a given identifier.
    /// </summary>
    /// <param name="id">The identifier for the word.</param>
    /// <returns>The word associated with the identifier.</returns>
    public string GetWord(ushort id)
    {
        return wordToString[id];
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

            if (char.IsLetter(c))
            {
                while (i < line.Length && char.IsLetterOrDigit(line, i))
                {
                    i++;
                }
            }
            else if (char.IsDigit(c))
            {
                while (i < line.Length && char.IsDigit(line, i))
                {
                    i++;
                }
            }
            else if (c is ' ' or '\t')
            {
                while (i < line.Length && line[i] == c)
                {
                    i++;
                }
            }

            yield return new Range(start, i);
        }
    }
}