// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Computes canonical pair keys for drug-drug interaction lookups.
///
/// A pair key is order-independent: the two ingredient IENs are sorted
/// lexicographically so that (A,B) and (B,A) always produce the same key.
/// This mirrors VistA's sorted cross-reference approach on the drug
/// interaction files (#56-1 through #56-6).
///
/// All three layers — dataset grain, cache service, and StatelessWorker checker —
/// must use this helper so that keys are identical everywhere.
/// </summary>
public static class DrugInteractionKeyHelper
{
    /// <summary>
    /// Produces the canonical key for an interaction pair.
    /// Both orderings of the two ingredient IENs produce the same result.
    /// Format: "{smaller}:{larger}" where ordering is lexicographic (Ordinal).
    /// </summary>
    public static string MakePairKey(string ingredientIen1, string ingredientIen2)
    {
        int cmp = string.Compare(ingredientIen1, ingredientIen2, StringComparison.Ordinal);
        return cmp <= 0
            ? $"{ingredientIen1}:{ingredientIen2}"
            : $"{ingredientIen2}:{ingredientIen1}";
    }
}
