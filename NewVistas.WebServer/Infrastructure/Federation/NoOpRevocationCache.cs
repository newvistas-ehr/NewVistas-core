// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// <see cref="IRevocationCache"/> for non-hub deployments. Always reports
/// "not revoked" — those deployments don't host the registry and have no
/// list to consult. Keeps <c>FederationPeerHandler</c> branch-free: it
/// always has the dependency and always asks the question, the answer
/// just happens to be no.
/// </summary>
public sealed class NoOpRevocationCache : IRevocationCache
{
    public bool IsRevoked(string thumbprint) => false;
    public Task RefreshAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
