// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton patient registration coordinator. See
/// <see cref="IPatientRegistrationGrain"/> for the contract and ADR-001 for
/// the rationale.
///
/// Stateless: this grain only orchestrates other grains. Each registration
/// call:
///   1. Determines the ICN (from the local issuer or an externally-supplied value)
///   2. Routes to the per-patient <see cref="IPatientWorkflowGrain"/> keyed by ICN
///   3. Sets demographics, then DFN, then ICN+MPI correlation+search index
///   4. Adds a local-facility correlation row to the MPI grain
///   5. Hands off to the configured <see cref="IRegistrationEligibilityPolicy"/>
///      so per-organization eligibility rules can run (or not) without this
///      grain knowing the difference between VA, IHS, and other deployments.
/// </summary>
public class PatientRegistrationGrain : Grain, IPatientRegistrationGrain
{
    private readonly IClusterIdentity _clusterIdentity;
    private readonly IRegistrationEligibilityPolicy _eligibilityPolicy;
    private readonly IMpiFederationAnnouncer _mpiAnnouncer;

    public PatientRegistrationGrain(
        IClusterIdentity clusterIdentity,
        IRegistrationEligibilityPolicy eligibilityPolicy,
        IMpiFederationAnnouncer mpiAnnouncer)
    {
        _clusterIdentity = clusterIdentity;
        _eligibilityPolicy = eligibilityPolicy;
        _mpiAnnouncer = mpiAnnouncer;
    }

    public async Task<string> RegisterPatientAsync(RegistrationRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.PatientName))
            throw new ArgumentException("PatientName is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.FacilityDfn))
            throw new ArgumentException("FacilityDfn is required.", nameof(request));

        string icn = !string.IsNullOrWhiteSpace(request.ExternallySuppliedIcn)
            ? request.ExternallySuppliedIcn!
            : await GrainFactory.GetGrain<IIcnIssuerGrain>("ICN-ISSUER").IssueNextAsync();

        IPatientWorkflowGrain workflow = GrainFactory.GetGrain<IPatientWorkflowGrain>(icn);

        // Demographics first — SetMpiCorrelationAsync reads PatientState.Name
        // from the patient grain to populate the correlation record.
        await workflow.UpdateDemographicsAsync(
            request.PatientName, request.Sex ?? string.Empty, request.DateOfBirth, request.Ssn);

        // DFN preserved as legacy / forensic state per ADR-001.
        await workflow.SetDfnAsync(request.FacilityDfn);

        // SetMpiCorrelationAsync sets the ICN on the patient grain, creates
        // the MpiCorrelationGrain, and adds an entry to the MpiSearchGrain.
        await workflow.SetMpiCorrelationAsync(icn, request.Ssn, request.DateOfBirth, request.Sex);

        // Add the local-facility correlation row so the MPI knows this
        // patient has been seen at this cluster's facility.
        IMpiCorrelationGrain mpi = GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");
        await mpi.AddLocalCorrelationAsync(
            facilityId: _clusterIdentity.LocalClusterId,
            facilityName: _clusterIdentity.LocalClusterId,
            localDfn: request.FacilityDfn,
            correlationDate: DateTime.UtcNow);

        // Run the configured eligibility policy. NoOp by default; VA-aligned
        // profiles register VaRegistrationEligibilityPolicy which applies
        // §17.36 priority-group rules on the spot.
        await _eligibilityPolicy.DetermineAndApplyAsync(icn, request, workflow);

        // Announce the new patient to peer clusters in a federated deployment
        // so their MPI search indexes pick up the patient. NoOp by default;
        // outbox-backed in federated profiles. Errors here are intentionally
        // swallowed — the local registration has already completed; a peer-
        // announcement failure is operational, not clinical.
        try
        {
            var entry = new MpiSearchEntry
            {
                Icn = icn,
                PatientName = request.PatientName,
                Ssn = request.Ssn,
                DateOfBirth = request.DateOfBirth,
                Sex = request.Sex,
                FacilityCount = 1,
                IsDeceased = false,
            };
            await _mpiAnnouncer.AnnouncePatientRegisteredAsync(entry, _clusterIdentity.LocalClusterId);
        }
        catch
        {
            // Announce-failure does not fail the registration. The local
            // patient grain is already created and consistent.
        }

        return icn;
    }
}
