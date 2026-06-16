// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Auto Refill — Site Flavor Architecture (Option 4: Composition).
/// Checks the AUTO_REFILL feature flag before delegating to the optional
/// IAutoRefillGrain. Automated prescription refill scheduling that VistA lacks.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string AutoRefillFeature = "AUTO_REFILL";

    public async Task<AutoRefillState> EnrollAutoRefillAsync(
        string prescriptionId, string drugName, string drugClass,
        int daysSupply, int refillsRemaining, DateTime lastFillDate,
        string pharmacyId, string pharmacyName,
        string enrolledByProviderId, string enrolledByProviderName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled)
            throw new InvalidOperationException(
                "Auto refill is not enabled for this site. Enable the AUTO_REFILL feature in Site Parameters.");

        PatientState patient = await GetPatientGrain().GetPatientAsync();

        string enrollmentId = $"RX-AUTOREFILL:{Guid.NewGuid()}";
        IAutoRefillGrain grain = GrainFactory.GetGrain<IAutoRefillGrain>(enrollmentId);

        AutoRefillState result = await grain.EnrollAsync(
            PatientId, patient.Name,
            prescriptionId, drugName, drugClass,
            daysSupply, refillsRemaining, lastFillDate,
            pharmacyId, pharmacyName,
            enrolledByProviderId, enrolledByProviderName);

        await LogAuditEventAsync(
            "PHARMACY", "ENROLL_AUTO_REFILL", "AutoRefill", enrollmentId,
            enrolledByProviderId, enrolledByProviderName, null, null,
            $"Enrolled {drugName} in auto-refill ({daysSupply}-day supply, {refillsRemaining} refills)");

        return result;
    }

    public async Task<List<AutoRefillIndexEntry>> GetAutoRefillEnrollmentsAsync()
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled) return [];

        IAutoRefillIndexGrain index =
            GrainFactory.GetGrain<IAutoRefillIndexGrain>("RX-AUTOREFILL-IDX");
        return await index.GetByPatientAsync(PatientId);
    }

    public async Task<AutoRefillState> GetAutoRefillEnrollmentAsync(string enrollmentId)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled)
            throw new InvalidOperationException("Auto refill is not enabled for this site.");

        IAutoRefillGrain grain = GrainFactory.GetGrain<IAutoRefillGrain>(enrollmentId);
        return await grain.GetEnrollmentAsync();
    }

    public async Task SuspendAutoRefillAsync(string enrollmentId, string reason, string suspendedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled)
            throw new InvalidOperationException("Auto refill is not enabled for this site.");

        IAutoRefillGrain grain = GrainFactory.GetGrain<IAutoRefillGrain>(enrollmentId);
        await grain.SuspendAsync(reason, suspendedByName);
    }

    public async Task ResumeAutoRefillAsync(string enrollmentId, string resumedByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled)
            throw new InvalidOperationException("Auto refill is not enabled for this site.");

        IAutoRefillGrain grain = GrainFactory.GetGrain<IAutoRefillGrain>(enrollmentId);
        await grain.ResumeAsync(resumedByName);
    }

    public async Task DisenrollAutoRefillAsync(string enrollmentId, string reason, string disenrolledByName)
    {
        bool enabled = await GetSiteParams().IsFeatureEnabledAsync(AutoRefillFeature);
        if (!enabled)
            throw new InvalidOperationException("Auto refill is not enabled for this site.");

        IAutoRefillGrain grain = GrainFactory.GetGrain<IAutoRefillGrain>(enrollmentId);
        await grain.DisenrollAsync(reason, disenrolledByName);
    }
}
