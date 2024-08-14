using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using JetBrains.Annotations;

using Reaganism.FBI.Diffing;
using Reaganism.FBI.Matching;

namespace Reaganism.FBI.Patching;

[PublicAPI]
public sealed class Patcher
{
    [PublicAPI]
    public enum Mode
    {
        [PublicAPI]
        Exact,

        [PublicAPI]
        Offset,

        [PublicAPI]
        Fuzzy,
    }

    [PublicAPI]
    public sealed class Result(ReadOnlyPatch patch)
    {
        [PublicAPI]
        public ReadOnlyPatch Patch { [PublicAPI] get; } = patch;

        [PublicAPI]
        public bool Success { [PublicAPI] get; [PublicAPI] init; }

        [PublicAPI]
        public Mode Mode { [PublicAPI] get; [PublicAPI] init; }

        [PublicAPI]
        public int SearchOffset { [PublicAPI] get; [PublicAPI] set; }

        [PublicAPI]
        public ReadOnlyPatch? AppliedPatch { [PublicAPI] get; [PublicAPI] set; }

        [PublicAPI]
        public int Offset { [PublicAPI] get; [PublicAPI] set; }

        [PublicAPI]
        public bool OffsetWarning { [PublicAPI] get; [PublicAPI] set; }

        [PublicAPI]
        public float FuzzyQuality { [PublicAPI] get; [PublicAPI] set; }

        [PublicAPI]
        public string Summary()
        {
            var header = FBI.Patch.GetHeader(Patch, false);

            if (!Success)
            {
                return $"FAILURE: {header}";
            }

            switch (Mode)
            {
                case Mode.Offset:
                    return (OffsetWarning ? "WARNING" : "OFFSET") + $": {header} offset {Offset} lines";

                case Mode.Fuzzy:
                {
                    var q = (int)(FuzzyQuality * 100);
                    return $"FUZZY: {header} quality {q}%" + (Offset > 0 ? $" offset {Offset} lines" : "");
                }

                case Mode.Exact:
                    return $"EXACT: {header}";

                default:
                    throw new InvalidOperationException();
            }
        }
    }

    // TODO: Revise entire Patcher API; remove/refactor all of this.
    private sealed class WorkingPatch(ReadOnlyPatch patch)
    {
        public ReadOnlyPatch Patch { get; } = patch;

        public Result? Result { get; private set; }

        public string? LmContext { get; private set; }

        public string? LmPatched { get; private set; }

        public string[]? WmContext { get; private set; }

        public string[]? WmPatched { get; private set; }

        // public LineRange? KeepOutRange1 => Result?.AppliedPatch?.TrimmedRange1;

        public LineRange? KeepOutRange2 => Result?.AppliedPatch?.TrimmedRange2;

        public int? AppliedDelta => Result?.AppliedPatch?.Range2.Length - Result?.AppliedPatch?.Range1.Length;

        public void Succeed(Mode mode, ReadOnlyPatch appliedPatch)
        {
            Result = new Result(Patch)
            {
                Success      = true,
                Mode         = mode,
                AppliedPatch = appliedPatch,
            };
        }

        public void Fail()
        {
            Result = new Result(Patch)
            {
                Success = false,
            };
        }

        public void AddOffsetResult(int offset, int fileLength)
        {
            Debug.Assert(Result is not null);

            // Note that the offset is different to at - start2 because offset
            // is relative to the applied position of the last patch.
            Result.Offset        = offset;
            Result.OffsetWarning = offset > OffsetWarnDistance(Patch.Range1.Length, fileLength);
        }

        public void AddFuzzyResult(float fuzzyQuality)
        {
            Debug.Assert(Result is not null);

            Result.FuzzyQuality = fuzzyQuality;
        }

        public void LinesToIds(TokenMapper tokenMapper)
        {
            LmContext = tokenMapper.LinesToIds(Patch.ContextLines);
            LmPatched = tokenMapper.LinesToIds(Patch.PatchedLines);
        }

        public void WordsToIds(TokenMapper tokenMapper)
        {
            WmContext = Patch.ContextLines.Select(tokenMapper.WordsToIds).ToArray();
            WmPatched = Patch.PatchedLines.Select(tokenMapper.WordsToIds).ToArray();
        }
    }

    private struct MatchRunner(int loc, int dir, MatchMatrix[] mms, float penaltyPerLine)
    {
        private int       loc    = loc;
        private LineRange active = new(); // Used as a Range/Slice for the MM array.

        // Start penalty at -10% to give some room for finding the best
        // match if it's not "too far".
        private float penalty = -0.1f;

        public bool Step(ref float bestScore, ref int[]? bestMatch)
        {
            if (active.First == mms.Length)
            {
                return false;
            }

            if (bestScore > 1f - penalty)
            {
                // Not getting any better than this.
                return false;
            }

            // Activate matchers as we enter their working range.
            while (active.End < mms.Length && mms[active.End].WorkingRange.Contains(loc))
            {
                active = active with { End = active.End + 1 };
            }

            // Active MatchMatrix runs.
            for (var i = active.First; i <= active.Last; i++)
            {
                var mm = mms[i];
                if (!mm.Match(loc, out var score))
                {
                    Debug.Assert(i == active.First, "Match matrices out of order?");
                    active = active with { Start = active.First + 1 }; // First
                    continue;
                }

                if (penalty > 0)
                {
                    // Ignore penalty for the first 10%.
                    score -= penalty;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = mm.Path();
                }
            }

            loc     += dir;
            penalty += penaltyPerLine;
            return true;
        }
    }

    [PublicAPI]
    public class FuzzyMatchOptions
    {
        [PublicAPI]
        public int MaxMatchOffset { [PublicAPI] get; [PublicAPI] set; } = MatchMatrix.DEFAULT_MAX_OFFSET;

        [PublicAPI]
        public float MinMatchScore { [PublicAPI] get; [PublicAPI] set; } = FuzzyLineMatcher.DEFAULT_MIN_MATCH_SCORE;

        [PublicAPI]
        public bool EnableDistancePenalty { [PublicAPI] get; [PublicAPI] set; } = true;
    }

    [PublicAPI]
    public IEnumerable<Result?> Results
    {
        [PublicAPI] get => patches.Select(x => x.Result);
    }

    [PublicAPI]
    public string[] ResultLines
    {
        [PublicAPI] get => lines.ToArray();
    }

    [PublicAPI]
    public FuzzyMatchOptions FuzzyOptions { [PublicAPI] get; [PublicAPI] set; } = new();

    private LineRange ModifiedRange => new(0, lastAppliedPatch?.TrimmedRange2.End ?? 0);

    private readonly IReadOnlyList<WorkingPatch> patches;
    private readonly TokenMapper                 tokenMapper;
    private readonly List<string>                lines;

    private bool          applied;
    private string?       lmText;
    private List<string>? wmLines;

    // Last here means highest line number, not necessarily most recent.
    // Patches can only apply before `lastAppliedPatch` in fuzzy mode.
    private ReadOnlyPatch? lastAppliedPatch;

    // We maintain delta as the offset of the last patch (applied location -
    // expected location).  This way if a line is inserted, and all patches are
    // offset by 1, only the first patch is reported as offset.  Normally, this
    // is equivalent to `lastAppliedPatch?.AppliedOffset`, but if a patch fails,
    // we subtract its length delta from the search offset.
    private int searchOffset;

    [PublicAPI]
    public Patcher(IEnumerable<ReadOnlyPatch> patches, IEnumerable<string> lines, TokenMapper? tokenMapper = null)
    {
        this.patches     = patches.Select(x => new WorkingPatch(x)).ToList();
        this.lines       = lines.ToList();
        this.tokenMapper = tokenMapper ?? new TokenMapper();
    }

    [PublicAPI]
    public void Patch(Mode mode)
    {
        if (applied)
        {
            throw new InvalidOperationException("Already applied patch.");
        }

        applied = true;

        foreach (var patch in patches)
        {
            if (ApplyExact(patch))
            {
                continue;
            }

            if (mode >= Mode.Offset && ApplyOffset(patch))
            {
                continue;
            }

            if (mode >= Mode.Fuzzy && ApplyFuzzy(patch))
            {
                continue;
            }

            patch.Fail();
            patch.Result!.SearchOffset =  searchOffset;
            searchOffset               -= patch.Patch.Range2.Length - patch.Patch.Range1.Length;
        }
    }

    private void LinesToIds()
    {
        foreach (var patch in patches)
        {
            patch.LinesToIds(tokenMapper);
        }

        lmText = tokenMapper.LinesToIds(lines);
    }

    private void WordsToIds()
    {
        foreach (var patch in patches)
        {
            patch.WordsToIds(tokenMapper);
        }

        wmLines = lines.Select(x => tokenMapper.WordsToIds(x)).ToList();
    }

    private ReadOnlyPatch ApplyExactAt(int loc, WorkingPatch patch)
    {
        if (!patch.Patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.Patch.Range1.Length)))
        {
            // TODO: bro what
            throw new Exception("Patch engine failure.");
        }

        if (!CanApplySafelyAt(loc, patch.Patch))
        {
            throw new Exception("Patch affects another patch.");
        }

        lines.RemoveRange(loc, patch.Patch.Range1.Length);
        lines.InsertRange(loc, patch.Patch.PatchedLines);

        // Update `lineModeText`.
        if (lmText is not null)
        {
            lmText = string.Concat(lmText.Remove(loc), patch.LmPatched, lmText.AsSpan(loc + patch.Patch.Range1.Length));
        }

        // Update `wordModeLines`.
        if (wmLines is not null)
        {
            Debug.Assert(patch.WmPatched is not null);

            wmLines.RemoveRange(loc, patch.Patch.Range1.Length);
            wmLines.InsertRange(loc, patch.WmPatched);
        }

        var patchedDelta = patches.Where(x => x.KeepOutRange2?.End <= loc).Sum(x => x.AppliedDelta!.Value);
        var appliedPatch = patch.Patch;
        if (appliedPatch.Range2.Start != loc || appliedPatch.Range1.Start != loc - patchedDelta)
        {
            appliedPatch = patch.Patch with
            {
                Range1 = patch.Patch.Range1 with { Start = loc - patchedDelta },
                Range2 = patch.Patch.Range2 with { Start = loc },
            };
        }

        // Update the applied location for patches following this one in the
        // file but preceding it in the patch list.  Can only happen if fuzzy
        // matching causes a patch to move before one of the previously applied
        // patches.
        if (loc < ModifiedRange.End)
        {
            foreach (var p in patches.Where(x => x.KeepOutRange2?.Start > loc))
            {
                p.Result!.AppliedPatch = p.Result!.AppliedPatch!.Value with
                {
                    Range2 = p.Result!.AppliedPatch!.Value.Range2 with { Start = p.Result!.AppliedPatch!.Value.Range2.Start + patch.Patch.Range2.Length - patch.Patch.Range1.Length },
                };
            }
        }
        else
        {
            lastAppliedPatch = appliedPatch;
        }

        searchOffset = appliedPatch.Range2.Start - patch.Patch.Range2.Start;
        return appliedPatch;
    }

    private bool CanApplySafelyAt(int loc, ReadOnlyPatch patch)
    {
        if (loc >= ModifiedRange.End)
        {
            return true;
        }

        var range = new LineRange(loc, 0).WithLength(patch.Range1.Length);
        return patches.All(x => !x.KeepOutRange2?.Contains(range) ?? true);
    }

    private bool ApplyExact(WorkingPatch patch)
    {
        var loc = patch.Patch.Range2.Start + searchOffset;
        if (loc + patch.Patch.Range1.Length > lines.Count)
        {
            return false;
        }

        if (!patch.Patch.ContextLines.SequenceEqual(lines.GetRange(loc, patch.Patch.Range1.Length)))
        {
            return false;
        }

        patch.Succeed(Mode.Exact, ApplyExactAt(loc, patch));
        return true;
    }

    private bool ApplyOffset(WorkingPatch patch)
    {
        if (lmText is null)
        {
            LinesToIds();
        }

        if (patch.Patch.Range1.Length > lines.Count)
        {
            return false;
        }

        var loc = patch.Patch.Range2.Start + searchOffset;
        if (loc < 0)
        {
            loc = 0;
        }
        else if (loc >= lines.Count)
        {
            loc = lines.Count - 1;
        }

        var forward = lmText!.IndexOf(patch.LmContext!, loc, StringComparison.Ordinal);
        var reverse = lmText.LastIndexOf(patch.LmContext!, Math.Min(loc + patch.LmContext!.Length, lines.Count - 1), StringComparison.Ordinal);

        if (!CanApplySafelyAt(forward, patch.Patch))
        {
            forward = -1;
        }

        if (!CanApplySafelyAt(reverse, patch.Patch))
        {
            reverse = -1;
        }

        if (forward < 0 && reverse < 0)
        {
            return false;
        }

        var found = reverse < 0 || (forward >= 0 && forward - loc < loc - reverse) ? forward : reverse;
        patch.Succeed(Mode.Offset, ApplyExactAt(found, patch));
        patch.AddOffsetResult(found - loc, lines.Count);

        return true;
    }

    private bool ApplyFuzzy(WorkingPatch patch)
    {
        if (wmLines is null)
        {
            WordsToIds();
        }

        var loc = patch.Patch.Range2.Start + searchOffset;
        if (loc + patch.Patch.Range1.Length > wmLines!.Count)
        {
            // Initialize search at the end of the file if loc is past the
            // file length.
            loc = wmLines.Count - patch.Patch.Range1.Length;
        }

        var (match, matchQuality) = FindMatch(loc, patch.WmContext!);
        if (match is null)
        {
            return false;
        }

        var fuzzyPatch = new WorkingPatch(AdjustPatchToMatchedLines(patch.Patch, match, lines));
        {
            if (wmLines is not null)
            {
                fuzzyPatch.WordsToIds(tokenMapper);
            }

            if (lmText is not null)
            {
                fuzzyPatch.LinesToIds(tokenMapper);
            }
        }

        // If the patch needs lines trimmed, the early match entries will be
        // negative.
        var at = match.First(x => x >= 0);
        patch.Succeed(Mode.Fuzzy, ApplyExactAt(at, fuzzyPatch));
        patch.AddOffsetResult(fuzzyPatch.Patch.Range2.Start - loc, lines.Count);
        patch.AddFuzzyResult(matchQuality);
        return true;
    }

    private (int[]? match, float score) FindMatch(int loc, IReadOnlyList<string> wmContext)
    {
        // Fuzzy matching is more complex because we need to split up the
        // patched file to only search *between* previously-applied patches.
        var keepOutRanges = patches.Select(x => x.KeepOutRange2).Where(x => x is not null).Select(x => x!.Value);

        // Parts of the file to search in.
        var ranges = new LineRange().WithLength(wmLines!.Count).Except(keepOutRanges).ToArray();

        return FuzzyMatch(wmContext, wmLines, loc, FuzzyOptions, ranges);
    }

    private static (int[]? match, float score) FuzzyMatch(IReadOnlyList<string> wmPattern, IReadOnlyList<string> wmText, int loc, FuzzyMatchOptions? options = null, LineRange[]? ranges = null)
    {
        ranges  ??= [new LineRange(0, wmText.Count)];
        options ??= new FuzzyMatchOptions();

        // We're creating twice as many MatchMatrix objects as we need,
        // incurring some wasted allocation and setup time, but it reads easier
        // than trying to precompute all the edge cases.
        var fwdMatchers = ranges.Select(x => new MatchMatrix(wmPattern,           wmText, options.MaxMatchOffset, x)).SkipWhile(x => loc > x.WorkingRange.Last).ToArray();
        var revMatches  = ranges.Reverse().Select(x => new MatchMatrix(wmPattern, wmText, options.MaxMatchOffset, x)).SkipWhile(x => loc < x.WorkingRange.First).ToArray();

        var warnDist       = OffsetWarnDistance(wmPattern.Count, wmText.Count);
        var penaltyPerLine = options.EnableDistancePenalty ? 1f / (10 * warnDist) : 0;

        var fwd = new MatchRunner(loc, 1,  fwdMatchers, penaltyPerLine);
        var rev = new MatchRunner(loc, -1, revMatches,  penaltyPerLine);

        var bestScore = options.MinMatchScore;
        var bestMatch = default(int[]);
        while (fwd.Step(ref bestScore, ref bestMatch) | rev.Step(ref bestScore, ref bestMatch)) { }

        return (bestMatch, bestScore);
    }

    private static ReadOnlyPatch AdjustPatchToMatchedLines(ReadOnlyPatch patch, int[] match, IReadOnlyList<string> lines)
    {
        var fuzzyPatch = patch.CreateMutable();
        var diffs      = fuzzyPatch.Diffs;

        // Keep operations but replace lines with lines in source text.
        // Unmatched patch lines (-1) are deleted, unmatched target lines
        // (increasing offset) are added to the patch.
        for (int i = 0, j = 0, pLoc = -1; i < patch.Range1.Length; i++)
        {
            var mLoc = match[i];

            // Insert extra target lines into the patch.
            if (mLoc >= 0 && pLoc >= 0 && mLoc - pLoc > 1)
            {
                // Delete an unmatched target line if the surrounding diffs are
                // also DELETE, otherwise ut it as context.
                var op = diffs[j - 1].Operation == Operation.DELETE && diffs[j].Operation == Operation.DELETE ? Operation.DELETE : Operation.EQUALS;

                for (var l = pLoc + 1; l < mLoc; l++)
                {
                    diffs.Insert(j++, new DiffLine(op, lines[l]));
                }
            }

            pLoc = mLoc;

            // Keep INSERT lines the same.
            while (diffs[j].Operation == Operation.INSERT)
            {
                j++;
            }

            // Unmatched context line.
            if (mLoc < 0)
            {
                diffs.RemoveAt(j);
            }
            else
            {
                // Update context to match target file (might be the same,
                // doesn't matter).
                diffs[j] = new DiffLine(diffs[j].Operation, lines[mLoc]);
                j++;
            }
        }

        return fuzzyPatch.AsReadOnly();
    }

    private static int OffsetWarnDistance(int patchLength, int fileLength)
    {
        return Math.Max(patchLength * 10, fileLength / 10);
    }
}