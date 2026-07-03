// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// The Person identity anchor (ADR-002). Additive overlay: linking a role sets the back-pointer on
/// that role's own record AND records the reverse reference here, so the two never drift. The hot
/// clinical path never routes through here — Person is consulted only for cross-role operations.
/// </summary>
public class PersonGrain : Grain, IPersonGrain
{
    private readonly IPersistentState<PersonState> _state;

    public PersonGrain(
        [PersistentState("personState", "personStore")] IPersistentState<PersonState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PersonId))
        {
            _state.State.PersonId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private IPatientGrain Patient(string patientId) => GrainFactory.GetGrain<IPatientGrain>(patientId);
    private INewPersonGrain Staff(string userId) => GrainFactory.GetGrain<INewPersonGrain>($"USER:{userId}");
    private IPersonIndexGrain Index() => GrainFactory.GetGrain<IPersonIndexGrain>("PERSON-INDEX:DEFAULT");
    private IPatientAccessControlGrain Pac(string patientId) => GrainFactory.GetGrain<IPatientAccessControlGrain>($"PAC:{patientId}");

    public async Task RegisterIdentityAsync(string name, DateTime? dateOfBirth, string sex, string ssnLast4)
    {
        _state.State.Name = name;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.Sex = sex;
        _state.State.SsnLast4 = ssnLast4;
        await SaveAndIndexAsync();
    }

    public async Task LinkPatientAsync(string patientId, string facilityId, bool primary, PersonLinkConfidence confidence, string linkedBy)
    {
        _state.State.PatientRoles.RemoveAll(r => string.Equals(r.PatientId, patientId, StringComparison.OrdinalIgnoreCase));
        _state.State.PatientRoles.Add(new PersonPatientRole
        {
            PatientId = patientId,
            FacilityId = facilityId,
            Primary = primary,
            Confidence = confidence,
            LinkedBy = linkedBy,
            LinkedDate = DateTime.UtcNow
        });
        await SaveAndIndexAsync();
        await Patient(patientId).SetPersonIdAsync(_state.State.PersonId);
    }

    public async Task LinkStaffAsync(string userId, PersonLinkConfidence confidence, string linkedBy)
    {
        _state.State.StaffRoles.RemoveAll(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        _state.State.StaffRoles.Add(new PersonStaffRole
        {
            UserId = userId,
            Confidence = confidence,
            LinkedBy = linkedBy,
            LinkedDate = DateTime.UtcNow
        });
        await SaveAndIndexAsync();
        await Staff(userId).SetPersonIdAsync(_state.State.PersonId);
    }

    public async Task AddRelativeAppearanceAsync(string onPatientId, string relationship, PersonRelativeSource source, string sourceEntryId, string linkedBy)
    {
        _state.State.RelativeAppearances.RemoveAll(a =>
            string.Equals(a.OnPatientId, onPatientId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.SourceEntryId, sourceEntryId, StringComparison.OrdinalIgnoreCase));
        _state.State.RelativeAppearances.Add(new PersonRelativeAppearance
        {
            OnPatientId = onPatientId,
            Relationship = relationship,
            Source = source,
            SourceEntryId = sourceEntryId,
            LinkedBy = linkedBy,
            LinkedDate = DateTime.UtcNow
        });
        await SaveAndIndexAsync();
    }

    public async Task UnlinkPatientAsync(string patientId)
    {
        int removed = _state.State.PatientRoles.RemoveAll(r => string.Equals(r.PatientId, patientId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return;
        await SaveAndIndexAsync();
        await Patient(patientId).SetPersonIdAsync(null);
    }

    public async Task UnlinkStaffAsync(string userId)
    {
        int removed = _state.State.StaffRoles.RemoveAll(r => string.Equals(r.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return;
        await SaveAndIndexAsync();
        await Staff(userId).SetPersonIdAsync(null);
    }

    public async Task RemoveRelativeAppearanceAsync(string onPatientId, string sourceEntryId)
    {
        int removed = _state.State.RelativeAppearances.RemoveAll(a =>
            string.Equals(a.OnPatientId, onPatientId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.SourceEntryId, sourceEntryId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return;
        await SaveAndIndexAsync();
    }

    public Task<PersonState> GetAsync() => Task.FromResult(_state.State);

    // Recompute the employee-patient flag, persist, and refresh the directory index in one place.
    private async Task SaveAndIndexAsync()
    {
        _state.State.IsEmployeePatient = _state.State.PatientRoles.Count > 0 && _state.State.StaffRoles.Count > 0;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await Index().UpsertAsync(new PersonIndexEntry
        {
            PersonId = _state.State.PersonId,
            Name = _state.State.Name,
            DateOfBirth = _state.State.DateOfBirth,
            Sex = _state.State.Sex,
            PatientRoleCount = _state.State.PatientRoles.Count,
            StaffRoleCount = _state.State.StaffRoles.Count,
            IsEmployeePatient = _state.State.IsEmployeePatient
        });

        // ADR-002 Phase 4: propagate the employee-patient sensitivity to each linked chart, so a chart
        // whose owner is also on staff is auto-flagged sensitive (boundary only — never gates the team).
        foreach (PersonPatientRole role in _state.State.PatientRoles)
            await Pac(role.PatientId).SetEmployeePatientAsync(_state.State.IsEmployeePatient);
    }
}
