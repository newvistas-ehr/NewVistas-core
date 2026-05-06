// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// One entry per issued bootstrap token. Mirrors the per-token grain's
/// state so the dashboard can list tokens without enumerating every
/// possible token-string grain.
///
/// Stores the full token string for matching during the
/// <c>MarkConsumed</c> call; the dashboard renders only the first few
/// chars + ellipsis to avoid surfacing the credential.
/// </summary>
[GenerateSerializer]
public sealed record ProvisioningTokenSummary
{
    [Id(0)] public string Token { get; init; } = string.Empty;
    [Id(1)] public string ClusterId { get; init; } = string.Empty;
    [Id(2)] public DateTime IssuedUtc { get; init; }
    [Id(3)] public DateTime ExpiresUtc { get; init; }
    [Id(4)] public DateTime? ConsumedUtc { get; init; }
    [Id(5)] public string? ConsumedByThumbprint { get; init; }
}

/// <summary>
/// State for the singleton <see cref="GrainInterfaces.IProvisioningTokenIndexGrain"/>.
/// Append-only list of every bootstrap token ever issued, with the
/// consumed-or-not status updated when the spoke posts its CSR.
/// </summary>
[GenerateSerializer]
public class ProvisioningTokenIndexState
{
    [Id(0)] public List<ProvisioningTokenSummary> Tokens { get; set; } = new();
}
