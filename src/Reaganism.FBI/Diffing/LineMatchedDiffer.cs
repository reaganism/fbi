using System.Collections.Generic;
using System.Linq;

using Reaganism.FBI.Matching;

namespace Reaganism.FBI.Diffing;

public class LineMatchedDiffer(TokenMapper? tokenMapper = null) : PatienceDiffer(tokenMapper)
{
    private string[]? WordModeLines1 { get; set; }

    private string[]? WordModeLines2 { get; set; }

    public int MaxMatchOffset { get; set; }

    public float MinMatchScore { get; set; }

    public override int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines)
    {
        var matches = base.Match(originalLines, modifiedLines);
        WordModeLines1 = originalLines.Select(TokenMapper.WordsToIds).ToArray();
        WordModeLines2 = modifiedLines.Select(TokenMapper.WordsToIds).ToArray();

        new FuzzyLineMatcher
        {
            MinMatchScore  = MinMatchScore,
            MaxMatchOffset = MaxMatchOffset,
        }.MatchLinesByWords(matches, WordModeLines1, WordModeLines2);

        return matches;
    }
}