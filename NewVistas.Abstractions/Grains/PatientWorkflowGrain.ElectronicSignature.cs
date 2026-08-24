// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.Security;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Electronic-signature verification for the signing workflows. The check lives HERE, on the
/// grain, so every client inherits it: before this, verification existed only in the Blazor
/// UI — the REST endpoint signed on an empty body, the terminal accepted any keystroke, and
/// both WPF apps signed with hardcoded placeholder strings. A legal attestation gate that any
/// client can walk around is not a gate.
///
/// The code is verified against the hash stored for the CALLER in RequestContext — never
/// against a client-chosen signer id — and the raw code is never persisted anywhere.
/// </summary>
public partial class PatientWorkflowGrain
{
    /// <summary>
    /// Verifies the caller's electronic-signature code fail-closed and returns the caller's
    /// user id. Throws <see cref="UnauthorizedAccessException"/> when there is no
    /// authenticated caller, no code, or the code does not match the caller's stored hash.
    /// </summary>
    private async Task<string> VerifyElectronicSignatureCodeAsync(string signatureCode)
    {
        string? userId = RequestContext.Get(RequestContextKeys.UserId) as string;
        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException(
                "Signing requires an authenticated user; no caller identity is present.");
        if (string.IsNullOrWhiteSpace(signatureCode))
            throw new UnauthorizedAccessException(
                "Signing requires the electronic signature code.");

        bool valid = await GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}")
            .VerifyElectronicSignatureAsync(ElectronicSignature.Hash(signatureCode));
        if (!valid)
            throw new UnauthorizedAccessException("Electronic signature verification failed.");

        return userId;
    }

    // ── System signing (XUPROG-gated at the interface) ──────────────────────
    // For seeding and programmatic migration only: signs without a per-user code, which is
    // exactly why the interface entries require the programmer key. Humans sign with a code.

    public Task SignNoteAsSystemAsync(string documentId) => SignNoteCoreAsync(documentId);

    public Task CosignNoteAsSystemAsync(string documentId) => CosignNoteCoreAsync(documentId);

    public Task SignOrderAsSystemAsync(string orderId, string attestation)
        => SignOrderCoreAsync(orderId, attestation);
}
