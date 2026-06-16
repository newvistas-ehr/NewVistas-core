// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;

namespace NewVistas.Abstractions.Security;

/// <summary>
/// Tamper-evident SHA-256 hash chain helper used by both the audit-event chain
/// and the clinical-event chain.
///
/// §170.315(d)(2) tamper-resistance: each event stores a SHA-256 hash of its
/// canonical content concatenated with the previous event's hash, forming an
/// append-only hash chain. Any retroactive modification breaks the chain.
///
/// Genesis hash (Base64 of SHA-256("GENESIS")) is shared by both chains so that
/// a brand-new patient's first event in either chain anchors to the same root.
/// </summary>
public static class HashChain
{
    /// <summary>
    /// Hash used as the PreviousEventHash for the first event in a chain.
    /// SHA-256 of "GENESIS" encoded as Base64.
    /// </summary>
    public const string GenesisHash = "uLHcHSVOgHQgXEomvUlhJcQv5JOYaGTyYGaCHdPjlRo=";

    /// <summary>
    /// Compute SHA-256(canonicalContent + previousHash) and return Base64.
    /// Callers build the canonical content by joining their immutable fields with '|'.
    /// </summary>
    public static string Compute(string canonicalContent, string previousHash)
    {
        string combined = canonicalContent + "|" + previousHash;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }
}
