// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;

namespace NewVistas.Abstractions.Security;

/// <summary>
/// The one definition of how an electronic signature code is hashed.
///
/// Shared deliberately. The hash was previously computed privately inside
/// <c>AuthController</c>, so any other caller wanting to verify a code had to re-implement it —
/// and two implementations that drift produce a signature that silently never matches. An
/// electronic signature is a legal attestation; it must not depend on two copies of a hash
/// agreeing by luck.
/// </summary>
public static class ElectronicSignature
{
    /// <summary>SHA-256 of the code, Base64-encoded. Only the hash is ever stored.</summary>
    public static string Hash(string signatureCode)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(signatureCode)));
}
