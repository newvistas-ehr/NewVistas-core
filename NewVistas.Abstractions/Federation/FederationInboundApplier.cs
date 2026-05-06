// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Mpi;
using NewVistas.Abstractions.GrainInterfaces;
using Orleans;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Default <see cref="IFederationInboundApplier"/> implementation. Stamps
/// missing source attribution from <c>fromClusterId</c>, validates basic
/// shape (non-empty patient and source cluster), and dispatches based on
/// the envelope's <see cref="EventEnvelope.Domain"/>:
///
///   - <c>"MPI"</c> → <see cref="IMpiInboundHandler"/> for cross-cluster
///     identity sync (patient registered, patient merged).
///   - everything else → <c>IPatientClinicalEventStreamGrain.AppendAsync</c>
///     for clinical event sourcing.
///
/// Per-envelope failures (validation, grain exception, etc.) are caught,
/// logged, and counted. The batch always completes — one bad envelope must
/// never abort delivery of the rest.
/// </summary>
public sealed class FederationInboundApplier : IFederationInboundApplier
{
    private readonly IGrainFactory _grainFactory;
    private readonly IMpiInboundHandler _mpiHandler;
    private readonly ILogger<FederationInboundApplier> _logger;

    public FederationInboundApplier(
        IGrainFactory grainFactory,
        IMpiInboundHandler mpiHandler,
        ILogger<FederationInboundApplier> logger)
    {
        _grainFactory = grainFactory;
        _mpiHandler = mpiHandler;
        _logger = logger;
    }

    public async Task<InboundApplyResult> ApplyBatchAsync(
        IReadOnlyList<EventEnvelope> envelopes,
        string fromClusterId,
        CancellationToken cancellationToken)
    {
        if (envelopes.Count == 0) return InboundApplyResult.Empty;

        if (string.IsNullOrWhiteSpace(fromClusterId))
        {
            // Caller's contract violation — the transport must authenticate
            // the sender before invoking the applier. Reject the whole batch
            // rather than swallow a missing identity.
            throw new ArgumentException(
                "fromClusterId cannot be empty; the calling transport must authenticate the sender.",
                nameof(fromClusterId));
        }

        int applied = 0;
        int errors = 0;

        foreach (EventEnvelope envelope in envelopes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                EventEnvelope stamped = StampSourceCluster(envelope, fromClusterId);

                if (string.IsNullOrEmpty(stamped.PatientId))
                {
                    _logger.LogWarning(
                        "Inbound envelope {EventId} from cluster {FromCluster} has empty PatientId; skipping.",
                        stamped.EventId, fromClusterId);
                    errors++;
                    continue;
                }
                if (string.IsNullOrEmpty(stamped.SourceClusterId))
                {
                    // Should be impossible after the stamp step, but defend
                    // against a fromClusterId that slipped through validation.
                    _logger.LogWarning(
                        "Inbound envelope {EventId} from cluster {FromCluster} has empty SourceClusterId after stamp; skipping.",
                        stamped.EventId, fromClusterId);
                    errors++;
                    continue;
                }

                // Dispatch on Domain. MPI envelopes go to the MPI inbound
                // handler; everything else goes to the per-patient clinical
                // event stream as before.
                if (string.Equals(stamped.Domain, MpiPatientRegisteredV1.MpiDomain, StringComparison.Ordinal))
                {
                    await _mpiHandler.ApplyAsync(stamped, cancellationToken);
                }
                else
                {
                    IPatientClinicalEventStreamGrain stream =
                        _grainFactory.GetGrain<IPatientClinicalEventStreamGrain>(stamped.PatientId);
                    await stream.AppendAsync(stamped);
                }
                applied++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Inbound applier failed on envelope {EventId} from cluster {FromCluster}; counted as error.",
                    envelope.EventId, fromClusterId);
                errors++;
            }
        }

        _logger.LogInformation(
            "Inbound applier processed batch from cluster {FromCluster}: total={Total} applied={Applied} errors={Errors}",
            fromClusterId, envelopes.Count, applied, errors);

        return new InboundApplyResult(envelopes.Count, applied, errors);
    }

    /// <summary>
    /// If the envelope has no <see cref="EventEnvelope.SourceClusterId"/>,
    /// stamp it with the authenticated sender; otherwise preserve the
    /// existing value (hub-and-spoke forwarding case).
    /// </summary>
    private static EventEnvelope StampSourceCluster(EventEnvelope envelope, string fromClusterId)
    {
        if (!string.IsNullOrEmpty(envelope.SourceClusterId)) return envelope;
        return envelope with { SourceClusterId = fromClusterId };
    }
}
