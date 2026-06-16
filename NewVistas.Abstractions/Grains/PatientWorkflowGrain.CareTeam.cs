// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Care Team (PCMM File #404.43) ─────────────────────────────────────

    private ICareTeamGrain GetCareTeamGrain()
        => GrainFactory.GetGrain<ICareTeamGrain>($"CARE-TEAM:{PatientId}");

    private IProviderPatientIndexGrain GetProviderPatientIndex(string providerId)
        => GrainFactory.GetGrain<IProviderPatientIndexGrain>($"PROV-PAT-IDX:{providerId}");

    private IProviderScheduleIndexGrain GetProviderScheduleIndex(string providerId)
        => GrainFactory.GetGrain<IProviderScheduleIndexGrain>($"PROV-SCHED:{providerId}");

    public async Task AddCareTeamMemberAsync(string providerId, string providerName, string role,
        string? specialty, string assignmentSource, DateTime? expirationDate)
    {
        // Add to care team grain
        await GetCareTeamGrain().AddMemberAsync(providerId, providerName, role,
            specialty, assignmentSource, expirationDate);

        // Sync provider's patient index with denormalized demographics
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        string? ssnLast4 = patient.SocialSecurityNumber?.Length >= 4
            ? patient.SocialSecurityNumber[^4..] : null;

        await GetProviderPatientIndex(providerId).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = PatientId,
            PatientName = patient.Name,
            DateOfBirth = patient.DateOfBirth,
            SsnLast4 = ssnLast4,
            Relationship = role,
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Grant access to sensitive records
        await GetPatientAccessControlGrain().AddAuthorizedProviderAsync(providerId);
    }

    public async Task RemoveCareTeamMemberAsync(string providerId)
    {
        // Deactivate in care team (does not delete)
        await GetCareTeamGrain().RemoveMemberAsync(providerId);

        // Deactivate in provider's patient index
        await GetProviderPatientIndex(providerId).DeactivatePatientAsync(PatientId);

        // NOTE: Do NOT remove from AuthorizedProviderIds — provider may still need
        // access for continuity of care, legal proceedings, or quality review.
    }

    public async Task SetPcpAsync(string providerId, string providerName, string? specialty)
    {
        await GetCareTeamGrain().SetPcpAsync(providerId, providerName, specialty);

        // Sync provider's patient index
        PatientState patient = await GetPatientGrain().GetPatientAsync();
        string? ssnLast4 = patient.SocialSecurityNumber?.Length >= 4
            ? patient.SocialSecurityNumber[^4..] : null;

        await GetProviderPatientIndex(providerId).AddOrUpdatePatientAsync(new ProviderPatientEntry
        {
            PatientId = PatientId,
            PatientName = patient.Name,
            DateOfBirth = patient.DateOfBirth,
            SsnLast4 = ssnLast4,
            Relationship = "PCP",
            IsActive = true,
            AssignmentDate = DateTime.UtcNow
        });

        // Grant access to sensitive records
        await GetPatientAccessControlGrain().AddAuthorizedProviderAsync(providerId);
    }

    public Task<CareTeamMember?> GetPcpAsync()
        => GetCareTeamGrain().GetPcpAsync();

    public Task<List<CareTeamMember>> GetCareTeamAsync()
        => GetCareTeamGrain().GetMembersAsync();

    public Task<List<CareTeamMember>> GetActiveCareTeamAsync()
        => GetCareTeamGrain().GetActiveMembersAsync();

    public Task<bool> IsOnCareTeamAsync(string providerId)
        => GetCareTeamGrain().HasActiveMemberAsync(providerId);

    public Task<List<CareTeamMember>> GetCareTeamForMessagingAsync()
        => GetCareTeamGrain().GetActiveMembersAsync();
}
