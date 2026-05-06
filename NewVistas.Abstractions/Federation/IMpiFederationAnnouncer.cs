// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Per-deployment policy for announcing MPI events (patient registered,
/// patient merged) to peer clusters in a multi-facility federation. Single-
/// cluster deployments register <see cref="NoOpMpiFederationAnnouncer"/> as
/// the default and incur zero overhead. Federated deployments register an
/// outbox-backed implementation that emits announcements to peer clusters
/// where the inbound applier updates each peer's local
/// <see cref="GrainInterfaces.IMpiSearchGrain"/> and
/// <see cref="GrainInterfaces.IMpiCorrelationGrain"/>.
///
/// <para>
/// Same architectural pattern as <see cref="Eligibility.IRegistrationEligibilityPolicy"/>,
/// <see cref="Reporting.IGpraSubmissionFormatter"/>, and
/// <see cref="Reporting.INdwExportFormatter"/>: pluggable, default no-op,
/// register a concrete implementation per deployment.
/// </para>
///
/// <para>
/// <b>Why announce instead of just letting the federation outbox flow MPI
/// changes alongside clinical events?</b> MPI is identity infrastructure,
/// not clinical data — its propagation rules differ (e.g., a merged-into
/// alias should reach every peer immediately rather than ride the per-
/// patient clinical event stream). Keeping a separate announcement seam
/// lets each deployment choose the right transport without entangling
/// clinical event sourcing with identity-management semantics.
/// </para>
/// </summary>
public interface IMpiFederationAnnouncer
{
    /// <summary>
    /// Announce that a new patient has been registered locally. Peer
    /// clusters that receive the announcement add the patient to their
    /// local <see cref="GrainInterfaces.IMpiSearchGrain"/> so a clinician
    /// at any facility can find the patient by name/SSN/DOB.
    /// </summary>
    /// <param name="searchEntry">The MPI search entry as it should appear in peer indexes.</param>
    /// <param name="originatingFacilityId">The facility id where the patient was registered.</param>
    Task AnnouncePatientRegisteredAsync(MpiSearchEntry searchEntry, string originatingFacilityId);

    /// <summary>
    /// Announce that two patient records have been merged locally (the
    /// source ICN's data has been moved into the target ICN, and the source
    /// ICN is now an alias). Peer clusters that receive the announcement
    /// update their local
    /// <see cref="GrainInterfaces.IMpiCorrelationGrain"/> for the source
    /// ICN to set <see cref="MpiCorrelationState.MergedIntoIcn"/>, and
    /// stamp the same alias on their MPI search entry.
    /// </summary>
    Task AnnouncePatientMergedAsync(string sourceIcn, string targetIcn, string originatingFacilityId);
}
