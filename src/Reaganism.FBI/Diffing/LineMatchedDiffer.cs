using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Matching;

namespace Reaganism.FBI.Diffing;

[PublicAPI]
public class LineMatchedDiffer(TokenMapper? tokenMapper = null, bool disposeMembers = true) : PatienceDiffer(tokenMapper, disposeMembers)
{
    [PublicAPI]
    public int MaxMatchOffset
    {
        [PublicAPI] get => fuzzyLineMatcher.MaxMatchOffset;
        [PublicAPI] set => fuzzyLineMatcher.MaxMatchOffset = value;
    }

    [PublicAPI]
    public float MinMatchScore
    {
        [PublicAPI] get => fuzzyLineMatcher.MinMatchScore;
        [PublicAPI] set => fuzzyLineMatcher.MinMatchScore = value;
    }

    private readonly FuzzyLineMatcher fuzzyLineMatcher = new()
    {
        MaxMatchOffset = MatchMatrix.DEFAULT_MAX_OFFSET,
        MinMatchScore  = FuzzyLineMatcher.DEFAULT_MIN_MATCH_SCORE,
    };

    [PublicAPI]
    public override int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines)
    {
        var matches        = base.Match(originalLines, modifiedLines);
        var wordModeLines1 = originalLines.Select(TokenMapper.WordsToIds).ToArray();
        var wordModeLines2 = modifiedLines.Select(TokenMapper.WordsToIds).ToArray();

        fuzzyLineMatcher.MatchLinesByWords(matches, wordModeLines1, wordModeLines2);

        return matches;
    }
}