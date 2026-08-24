// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Services;

/// <summary>
/// Makes the coding-assistant seam robust to a fallible or unavailable model: tries the
/// primary (live) assistant and, on failure, falls back to the offline lexicon. Suggestion
/// quality degrades but the feature never breaks. The fallback result is tagged so the
/// provenance shows the live call was bypassed.
/// </summary>
public sealed class ResilientClinicalCodingAssistant : IClinicalCodingAssistant
{
    private readonly IClinicalCodingAssistant _primary;
    private readonly IClinicalCodingAssistant _fallback;

    public ResilientClinicalCodingAssistant(
        IClinicalCodingAssistant primary, IClinicalCodingAssistant fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public bool IsLiveModel => _primary.IsLiveModel;

    public string ProviderName => _primary.ProviderName;

    public async Task<CodingClaimsResult> SuggestClaimsAsync(
        string noteText, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.SuggestClaimsAsync(noteText, cancellationToken);
        }
        catch (Exception)
        {
            CodingClaimsResult fallback = await _fallback.SuggestClaimsAsync(noteText, cancellationToken);
            fallback.ProviderName = $"{_fallback.ProviderName} (fallback from {_primary.ProviderName})";
            return fallback;
        }
    }
}
