using System;
using System.Collections.Generic;

using JetBrains.Annotations;

namespace Reaganism.FBI.Diffing;

/// <summary>
///     A file differ capable of producing 
/// </summary>
[PublicAPI]
public interface IDiffer : IDisposable
{
    /// <summary>
    ///     The token mapper used to map tokens to their unique IDs.
    /// </summary>
    [PublicAPI]
    TokenMapper TokenMapper { [PublicAPI] get; }

    /// <summary>
    ///     Matches the original lines and the modified lines.
    /// </summary>
    /// <param name="originalLines">The original file lines.</param>
    /// <param name="modifiedLines">The modified file lines.</param>
    /// <returns>
    ///     TODO
    /// </returns>
    [PublicAPI]
    int[] Match(IReadOnlyCollection<string> originalLines, IReadOnlyCollection<string> modifiedLines);
}