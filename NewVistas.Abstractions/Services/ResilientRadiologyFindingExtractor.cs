// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Services;

/// <summary>
/// Makes the extractor seam robust to a fallible or unavailable model: tries a primary
/// (live) extractor and, on failure, falls back to the offline heuristic. Extraction
/// degrades but never breaks. The fallback result is tagged so the provenance shows the
/// live call was bypassed.
/// </summary>
public sealed class ResilientRadiologyFindingExtractor : IRadiologyFindingExtractor
{
    private readonly IRadiologyFindingExtractor _primary;
    private readonly IRadiologyFindingExtractor _fallback;

    public ResilientRadiologyFindingExtractor(
        IRadiologyFindingExtractor primary, IRadiologyFindingExtractor fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public bool IsLiveModel => _primary.IsLiveModel;

    public string ProviderName => _primary.ProviderName;

    public async Task<RadiologyExtractionResult> ExtractAsync(
        string reportText, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.ExtractAsync(reportText, cancellationToken);
        }
        catch (Exception)
        {
            RadiologyExtractionResult fallback = await _fallback.ExtractAsync(reportText, cancellationToken);
            fallback.ProviderName = $"{_fallback.ProviderName} (fallback from {_primary.ProviderName})";
            return fallback;
        }
    }
}
