using System;
using System.Collections.Generic;
using System.Linq;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     
/// </summary>
public sealed class TokenMapper
{
    private readonly List<string>            lineToString = [];
    private readonly Dictionary<string, int> stringToLine = new();

    private readonly List<string>            wordToString = [];
    private readonly Dictionary<string, int> stringToWord = new();

    private int[] buf = new int[4096];

    public TokenMapper()
    {
        lineToString.Add("\0");

        for (var i = 0; i < 0x80; i++)
        {
            wordToString.Add(((char)i).ToString());
        }
    }

    public int AddLine(string line)
    {
        if (stringToLine.TryGetValue(line, out var id))
        {
            return id;
        }

        stringToLine.Add(line, id = lineToString.Count);
        lineToString.Add(line);
        return id;
    }

    public int AddWord(string word)
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

        stringToWord.Add(word, id = wordToString.Count);
        wordToString.Add(word);
        return id;
    }

    public string WordsToIds(string line)
    {
        var b = 0;

        foreach (var r in EnumerateWords(line))
        {
            string word = line[r];

            if (b >= buf.Length)
            {
                Array.Resize(ref buf, buf.Length * 2);
            }

            buf[b++] = AddWord(word);
        }

        return string.Join(',', buf.Take(b));
    }

    public string LinesToIds(IEnumerable<string> lines)
    {
        return string.Join(',', lines.Select(AddLine));
    }

    public string GetWord(int id)
    {
        return wordToString[id];
    }

    private IEnumerable<Range> EnumerateWords(string line)
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