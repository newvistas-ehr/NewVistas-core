// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using NewVistas.WebServer.Infrastructure.Federation;
using Orleans.Serialization;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Receive surface for clinical event envelopes shipped from another NewVistas
/// cluster. Pairs with the outbound <c>SqlOutboxClinicalEventReplicationSink</c>
/// + <c>OutboxDrainerService</c> on the sender side; closes the federation
/// loop.
///
/// <para>
/// <b>Authentication:</b> guarded by the <c>FederationPeer</c> authorization
/// policy, which is configuration-driven:
/// <list type="bullet">
///   <item><description>
///     When <c>Federation:Inbound:TrustedCaPath</c> is unset, the policy
///     accepts all callers (allow-all) and a startup warning is logged.
///   </description></item>
///   <item><description>
///     When set, the policy requires a valid client certificate chained to
///     the configured CA, with the cert's CN listed in
///     <c>Federation:Inbound:AllowedClusterIds</c>. The action additionally
///     verifies the authenticated cluster id matches the request body's
///     <c>FromClusterId</c> — a peer with a valid <c>KIBALE-UGANDA</c> cert
///     cannot post events claiming to be <c>VAMC-BOSTON</c>.
///   </description></item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/federation")]
[Produces("application/json")]
[Authorize(Policy = FederationAuthExtensions.PolicyName,
           AuthenticationSchemes = CertificateAuthenticationDefaults.AuthenticationScheme)]
public sealed class FederationController : ControllerBase
{
    private readonly IFederationInboundApplier _applier;
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly InboundAuthOptions _authOptions;
    private readonly ILogger<FederationController> _logger;

    public FederationController(
        IFederationInboundApplier applier,
        Serializer<EventEnvelope> serializer,
        IOptions<InboundAuthOptions> authOptions,
        ILogger<FederationController> logger)
    {
        _applier = applier;
        _serializer = serializer;
        _authOptions = authOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Accept a batch of clinical event envelopes from a remote cluster,
    /// deserialize each blob, and apply them through the local stream grain.
    /// </summary>
    [HttpPost("inbound")]
    [ProducesResponseType(typeof(InboundApplyResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InboundApplyResult>> Inbound(
        [FromBody] InboundFederationBatch batch,
        CancellationToken cancellationToken)
    {
        if (batch is null)
            return BadRequest("Body required.");
        if (string.IsNullOrWhiteSpace(batch.FromClusterId))
            return BadRequest("FromClusterId is required.");

        // Cluster-id match check: only meaningful when an authenticated
        // principal is present (mTLS configured). In allow-all mode, the
        // policy accepts unauthenticated callers and we have no cluster id
        // to compare against — skip the check then.
        if (_authOptions.RequireMatchingClusterId
            && User.Identity?.IsAuthenticated == true
            && !string.IsNullOrEmpty(User.Identity.Name)
            && !string.Equals(User.Identity.Name, batch.FromClusterId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Federation inbound rejected: authenticated cluster '{Authenticated}' does not match body's FromClusterId '{Claimed}'.",
                User.Identity.Name, batch.FromClusterId);
            return Forbid();
        }

        // Per-blob deserialization: an unparseable blob counts as an error
        // for that envelope but doesn't fail the batch. Mirrors the per-
        // envelope failure tolerance the applier itself enforces.
        var envelopes = new List<EventEnvelope>(batch.EnvelopeBlobs.Count);
        int deserializeErrors = 0;
        foreach (byte[] blob in batch.EnvelopeBlobs)
        {
            try
            {
                envelopes.Add(_serializer.Deserialize(blob));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialize an inbound envelope blob from cluster {FromCluster}; skipping.",
                    batch.FromClusterId);
                deserializeErrors++;
            }
        }

        InboundApplyResult result =
            await _applier.ApplyBatchAsync(envelopes, batch.FromClusterId, cancellationToken);

        // Roll the deserialization errors into the result so the sender sees a
        // single Total/Applied/Errors triple covering the whole batch.
        return Ok(new InboundApplyResult(
            Total: result.Total + deserializeErrors,
            Applied: result.Applied,
            Errors: result.Errors + deserializeErrors));
    }
}
