// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
