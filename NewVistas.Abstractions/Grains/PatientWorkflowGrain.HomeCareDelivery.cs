// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Home-Based Care — the delivery-model dimension (who delivers): hospital-provided vs. an external
/// home-health agency we coordinate with, plus the Hospital-at-Home acute-substitution handoff. This is
/// orthogonal to the program/payer axis (HBPC vs Medicare skilled vs Hospital-at-Home). Writes are gated
/// by the HBHC MANAGER key at the interface; each write refreshes the census so the caseload delivery
/// column/filter stay current.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IHomeHealthAgencyDirectoryGrain HomeAgencyDirectory() =>
        GrainFactory.GetGrain<IHomeHealthAgencyDirectoryGrain>("HHA-DIRECTORY");

    public async Task SetHomeCareDeliveryModelAsync(string episodeId, HomeCareDeliveryModel deliveryModel)
    {
        // The episode grain enforces the HospitalAtHome → HospitalProvided invariant.
        await HomeEpisode(episodeId).SetDeliveryModelAsync(deliveryModel);
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task LinkHomeCareAgencyAsync(
        string episodeId, string agencyId, string coordinatorProviderId, string coordinatorName, string? externalReferralId)
    {
        HomeHealthAgencyEntry? agency = await HomeAgencyDirectory().GetAsync(agencyId);
        if (agency is null)
            throw new InvalidOperationException($"Home-health agency '{agencyId}' not found in the directory.");

        await HomeEpisode(episodeId).SetAgencyCoordinationAsync(new HomeCareAgencyCoordination
        {
            AgencyId = agency.AgencyId,
            AgencyName = agency.Name,
            AgencyNpi = agency.Npi,
            AgencyCcn = agency.Ccn,
            ExternalReferralId = externalReferralId,
            CoordinatorProviderId = coordinatorProviderId,
            CoordinatorName = coordinatorName
        });
        await RefreshHomeCensusAsync(episodeId);
    }

    public async Task<string> AddAgencyCareMilestoneAsync(
        string episodeId, AgencyMilestoneType type, DateTime date, string note, string recordedById, string recordedByName)
    {
        var milestone = new AgencyCareMilestone
        {
            MilestoneId = Guid.NewGuid().ToString(),
            Type = type,
            Date = date,
            Note = note ?? string.Empty,
            RecordedById = recordedById,
            RecordedByName = recordedByName
        };
        await HomeEpisode(episodeId).AddAgencyMilestoneAsync(milestone);
        return milestone.MilestoneId;
    }

    public async Task SetHospitalAtHomeContextAsync(
        string episodeId, string sourceAdmissionId, string sourceFacilityId, string sourceFacilityName,
        string? sourceUnitId, string? sourceBedId, DateTime? substitutionStartDate, string clinicalRationale)
    {
        await HomeEpisode(episodeId).SetHospitalAtHomeContextAsync(new HospitalAtHomeContext
        {
            SourceAdmissionId = sourceAdmissionId,
            SourceFacilityId = sourceFacilityId,
            SourceFacilityName = sourceFacilityName,
            SourceUnitId = sourceUnitId,
            SourceBedId = sourceBedId,
            SubstitutionStartDate = substitutionStartDate,
            ClinicalRationale = clinicalRationale ?? string.Empty
        });
    }
}
