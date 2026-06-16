// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using Orleans;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Mpi;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Default <see cref="IMpiInboundHandler"/>. Routes MPI envelopes to local
/// <see cref="IMpiSearchGrain"/> and <see cref="IMpiCorrelationGrain"/>
/// based on the payload type. Idempotent: re-applying the same envelope
/// (e.g., from a retry) is safe — search-index AddOrUpdate and
/// correlation MarkAsMerged are both idempotent under same-target.
/// </summary>
public sealed class DefaultMpiInboundHandler : IMpiInboundHandler
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DefaultMpiInboundHandler> _logger;

    public DefaultMpiInboundHandler(IGrainFactory grainFactory, ILogger<DefaultMpiInboundHandler> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task ApplyAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        switch (envelope.Payload)
        {
            case MpiPatientRegisteredV1 reg:
                await ApplyRegisteredAsync(reg);
                break;
            case MpiPatientMergedV1 mrg:
                await ApplyMergedAsync(mrg);
                break;
            default:
                _logger.LogWarning(
                    "MPI inbound handler received envelope {EventId} with unrecognised payload type '{Type}'; skipping.",
                    envelope.EventId, envelope.Payload?.GetType().Name ?? "<null>");
                break;
        }
    }

    private async Task ApplyRegisteredAsync(MpiPatientRegisteredV1 reg)
    {
        // Add or update the local MPI search index entry so a clinician at
        // this cluster can find the patient by name/SSN/DOB. Don't overwrite
        // an existing MergedIntoIcn alias — if the local cluster has the same
        // ICN already marked merged, leave it that way (a peer announcing
        // registration of an already-merged ICN is ordering-anomalous; the
        // merge should win).
        IMpiSearchGrain search = _grainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");
        MpiSearchResult? existing = await search.LookupByIcnAsync(reg.PatientId);
        string? mergedIntoPreserved = existing?.MergedIntoIcn;

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = reg.PatientId,
            PatientName = reg.PatientName,
            Ssn = reg.Ssn,
            DateOfBirth = reg.DateOfBirth,
            Sex = reg.Sex,
            FacilityCount = 1,
            IsDeceased = false,
            MergedIntoIcn = mergedIntoPreserved,
        });

        _logger.LogInformation(
            "MPI inbound: indexed peer-registered patient ICN {Icn} from facility {Facility}.",
            reg.PatientId, reg.OriginatingFacilityId);
    }

    private async Task ApplyMergedAsync(MpiPatientMergedV1 mrg)
    {
        // Mark the source ICN as merged into the target on the local correlation
        // grain. MarkAsMergedAsync is idempotent for the same target, refuses to
        // re-route to a different target, and rejects self-merge — no extra
        // guarding needed here.
        IMpiCorrelationGrain sourceMpi = _grainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{mrg.SourceIcn}");
        try
        {
            await sourceMpi.MarkAsMergedAsync(mrg.TargetIcn);
        }
        catch (InvalidOperationException ex)
        {
            // A peer announced a merge to a different target than the local
            // record holds. Log and continue — do not propagate; the inbound
            // applier counts caller-thrown exceptions as failures.
            _logger.LogWarning(ex,
                "MPI inbound: refused to re-route merged ICN {SourceIcn} (peer says -> {NewTarget}); local alias preserved.",
                mrg.SourceIcn, mrg.TargetIcn);
            return;
        }

        // Refresh the source's MPI search index entry to surface the alias.
        // Read existing demographics so the entry stays useful for searches
        // that hit the source ICN before the alias is followed.
        IMpiSearchGrain search = _grainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");
        MpiCorrelationState sourceCorr = await sourceMpi.GetCorrelationAsync();
        MpiSearchResult? existing = await search.LookupByIcnAsync(mrg.SourceIcn);

        await search.AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = mrg.SourceIcn,
            // Prefer the existing local entry's demographics if we have them;
            // otherwise fall back to whatever the correlation grain knows.
            PatientName = existing?.PatientName ?? sourceCorr.PatientName,
            Ssn = existing?.Ssn ?? sourceCorr.Ssn,
            DateOfBirth = existing?.DateOfBirth ?? sourceCorr.DateOfBirth,
            Sex = existing?.Sex ?? sourceCorr.Sex,
            FacilityCount = sourceCorr.LocalCorrelations.Count,
            IsDeceased = existing?.IsDeceased ?? sourceCorr.IsDeceased,
            MergedIntoIcn = mrg.TargetIcn,
        });

        _logger.LogInformation(
            "MPI inbound: applied peer merge {SourceIcn} -> {TargetIcn} from facility {Facility}.",
            mrg.SourceIcn, mrg.TargetIcn, mrg.OriginatingFacilityId);
    }
}
