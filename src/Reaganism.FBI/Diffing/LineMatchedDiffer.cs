using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Matching;

namespace Reaganism.FBI.Diffing;

[PublicAPI]
public class LineMatchedDiffer(TokenMapper? tokenMapper = null) : PatienceDiffer(tokenMapper)
{
    [PublicAPI]
    public int MaxMatchOffset { [PublicAPI] get; [PublicAPI] set; } = MatchMatrix.DEFAULT_MAX_OFFSET;

    [PublicAPI]
    public float MinMatchScore { [PublicAPI] get; [PublicAPI] set; } = FuzzyLineMatcher.DEFAULT_MIN_MATCH_SCORE;

    [PublicAPI]
    public override int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines)
    {
        var matches = base.Match(originalLines, modifiedLines);
        var wordModeLines1 = originalLines.Select(TokenMapper.WordsToIds).ToArray();
        var wordModeLines2 = modifiedLines.Select(TokenMapper.WordsToIds).ToArray();

        new FuzzyLineMatcher
        {
            MinMatchScore  = MinMatchScore,
            MaxMatchOffset = MaxMatchOffset,
        }.MatchLinesByWords(matches, wordModeLines1, wordModeLines2);

        return matches;
    }
}