// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Outcome of an <see cref="IFederationTransport.SendAsync"/> call. The drainer
/// uses this to decide whether to mark rows shipped or schedule a retry.
/// </summary>
public sealed record TransportResult(bool Success, string? Error)
{
    public static TransportResult Ok() => new(true, null);
    public static TransportResult Fail(string error) => new(false, error);
}
