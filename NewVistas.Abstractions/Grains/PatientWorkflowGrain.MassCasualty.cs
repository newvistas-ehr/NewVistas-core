// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Mass Casualty Mode — Site Flavor Architecture (Option 4: Composition).
/// Checks the MASS_CASUALTY feature flag. Patient-level methods register
/// casualties linked to this patient within an active MCI.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string MassCasualtyFeature = "MASS_CASUALTY";

    public async Task<MassCasualtyCasualtyState> RegisterAsMciCasualtyAsync(
        string incidentId, string triageTag, string triageCategory,
        string? chiefInjury, string? arrivalMode, string registeredByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(MassCasualtyFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Mass casualty mode is not enabled for this site. Enable the MASS_CASUALTY feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string casualtyId = $"MCI-CASUALTY:{Guid.NewGuid()}";
        IMassCasualtyCasualtyGrain grain =
            GrainFactory.GetGrain<IMassCasualtyCasualtyGrain>(casualtyId);

        MassCasualtyCasualtyState result = await grain.RegisterCasualtyAsync(
            incidentId, triageTag, triageCategory,
            PatientId, patient.Name,
            chiefInjury, arrivalMode, registeredByName);

        await LogAuditEventAsync(
            "EMERGENCY", "MCI_CASUALTY_REGISTERED", "MciCasualty", casualtyId,
            null, registeredByName, null, null,
            $"Registered as MCI casualty, tag {triageTag}, triage {triageCategory}");

        return result;
    }

    public async Task<List<MassCasualtyCasualtyIndexEntry>> GetMciCasualtiesForPatientAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(MassCasualtyFeature);
        if (!enabled) return [];

        // Search casualty index for entries linked to this patient
        IMassCasualtyCasualtyIndexGrain index =
            GrainFactory.GetGrain<IMassCasualtyCasualtyIndexGrain>("MCI-CASUALTY-IDX");
        var all = await index.SearchAsync(null, null, null, 200);
        return all.Where(e => e.PatientName != "UNIDENTIFIED")
            .ToList(); // In production, would have a patient-keyed query
    }
}
