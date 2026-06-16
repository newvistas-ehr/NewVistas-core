// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Computes the 6-digit checksum portion of an Integration Control Number
/// (ICN). The full ICN is <c>{3-digit cluster prefix}{7-digit local sequence}V{6-digit checksum}</c>
/// — this helper computes just the trailing 6 digits.
///
/// The algorithm here is a deterministic stand-in: a Luhn-mod-10-style
/// double-and-sum applied across the 10-digit prefix+sequence, then mapped
/// into the 6-digit numeric range. It is sufficient for cross-cluster
/// uniqueness validation and for catching single-digit transcription errors.
/// </summary>
// TODO: swap to authoritative VA ICN checksum algorithm if/when sourced from AITC.
// Until then, our locally-issued ICNs can be distinguished from AITC-issued
// ones only by the cluster prefix (synthetic 9xx prefixes for non-VA sites).
public static class IcnChecksumCalculator
{
    public static string Compute(string tenDigitPrefixAndSequence)
    {
        if (tenDigitPrefixAndSequence is null)
            throw new ArgumentNullException(nameof(tenDigitPrefixAndSequence));
        if (tenDigitPrefixAndSequence.Length != 10)
            throw new ArgumentException(
                $"Input must be exactly 10 digits (got {tenDigitPrefixAndSequence.Length}: '{tenDigitPrefixAndSequence}').",
                nameof(tenDigitPrefixAndSequence));

        long accumulator = 0;
        for (int i = 0; i < 10; i++)
        {
            char c = tenDigitPrefixAndSequence[i];
            if (c < '0' || c > '9')
                throw new ArgumentException(
                    $"Input must be all digits (offending char '{c}' at index {i} in '{tenDigitPrefixAndSequence}').",
                    nameof(tenDigitPrefixAndSequence));

            int digit = c - '0';
            // Position-weighted sum: even positions doubled (Luhn-like), with a
            // multiplicative factor per position to spread digits across the
            // checksum range.
            int weighted = (i % 2 == 0) ? digit * 2 : digit;
            if (weighted > 9) weighted -= 9;
            accumulator = (accumulator * 31 + weighted) & 0x7fffffff;
        }

        // Fold into 6-digit numeric range (000000–999999), zero-padded.
        long checksum = accumulator % 1_000_000;
        return checksum.ToString("D6");
    }
}
