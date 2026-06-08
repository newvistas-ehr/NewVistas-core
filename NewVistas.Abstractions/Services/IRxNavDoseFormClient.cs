// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Optional client for refreshing dose-form metadata from the NLM RxNav API
/// (https://rxnav.nlm.nih.gov/). This is a SEAM only: the default registration
/// is <see cref="NullRxNavDoseFormClient"/>, which performs no network access.
///
/// The embedded dose-form/route tables work fully offline and are the source of
/// truth; a live RxNav client (config-gated, using IHttpClientFactory) is a
/// future enhancement. Even when enabled, a refresh updates only the DF→DFG and
/// VistA-form→DF bridges — the curated DFG→route mapping is never overwritten.
/// </summary>
public interface IRxNavDoseFormClient
{
    /// <summary>
    /// Whether a live RxNav integration is configured. False for the Null default.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Fetches the current dose-form (TTY=DF) and dose-form-group (TTY=DFG)
    /// vocabulary plus their relationships from RxNav. Returns null when the
    /// feature is disabled (the offline embedded seed remains authoritative).
    /// </summary>
    Task<RxNavDoseFormSnapshot?> FetchDoseFormMetadataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A refreshable snapshot of RxNorm dose-form metadata returned by a live
/// RxNav client. Carries only the bridges that RxNav can supply
/// (DF→DFG membership and DF identities) — never VistA route mappings.
/// </summary>
public sealed class RxNavDoseFormSnapshot
{
    /// <summary>Dose forms with their dose-form-group membership.</summary>
    public List<DoseFormEntry> DoseForms { get; init; } = new();

    /// <summary>A marker for the RxNorm release this snapshot came from.</summary>
    public string SourceVersion { get; init; } = string.Empty;
}

/// <summary>
/// No-op default implementation. Reports the feature as disabled and never
/// touches the network, so the system runs fully offline out of the box.
/// </summary>
public sealed class NullRxNavDoseFormClient : IRxNavDoseFormClient
{
    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<RxNavDoseFormSnapshot?> FetchDoseFormMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<RxNavDoseFormSnapshot?>(null);
}
