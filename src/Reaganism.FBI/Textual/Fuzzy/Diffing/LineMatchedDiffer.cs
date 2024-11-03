using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Textual.Fuzzy.Matching;
using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy.Diffing;

[PublicAPI]
public class LineMatchedDiffer(FuzzyTokenMapper? mapper = null) : PatienceDiffer(mapper)
{
    [PublicAPI]
    public int MaxMatchOffset
    {
        get => fuzzyLineMatcher.MaxMatchOffset;
        set => fuzzyLineMatcher.MaxMatchOffset = value;
    }

    [PublicAPI]
    public float MinMatchScore
    {
        get => fuzzyLineMatcher.MinMatchScore;
        set => fuzzyLineMatcher.MinMatchScore = value;
    }

    private readonly FuzzyLineMatcher fuzzyLineMatcher = new()
    {
        MaxMatchOffset = FuzzyMatchMatrix.DEFAULT_MAX_OFFSET,
        MinMatchScore  = FuzzyLineMatcher.DEFAULT_MIN_MATCH_SCORE,
    };

    public override int[] Match(
        IReadOnlyCollection<Utf16String> originalLines,
        IReadOnlyCollection<Utf16String> modifiedLines
    )
    {
        var matches        = base.Match(originalLines, modifiedLines);
        var wordModeLines1 = originalLines.Select(Mapper.WordsToIds).ToArray();
        var wordModeLines2 = modifiedLines.Select(Mapper.WordsToIds).ToArray();

        fuzzyLineMatcher.MatchLinesByWords(matches, wordModeLines1, wordModeLines2);
        return matches;
    }
}