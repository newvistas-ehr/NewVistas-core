// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Allergies;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Grain implementation based on VistA PATIENT file (#2)
/// </summary>
public class PatientGrain : Grain, IPatientGrain
{
    private readonly IPersistentState<PatientState> _state;

    public PatientGrain(
        [PersistentState("patientState", "patientStore")] IPersistentState<PatientState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        // Idempotent on EventId, so a re-delivery of an already-confirmed envelope
        // is harmless.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public Task<PatientState> GetPatientAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task<PatientState> UpdateDemographicsAsync(
        string name,
        string sex,
        DateTime? dateOfBirth,
        string? socialSecurityNumber)
    {
        _state.State.Name = name;
        _state.State.Sex = sex;
        _state.State.DateOfBirth = dateOfBirth;
        _state.State.SocialSecurityNumber = socialSecurityNumber ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return _state.State;
    }

    public async Task UpdateAddressAsync(
        string? streetAddress1,
        string? streetAddress2,
        string? streetAddress3,
        string? city,
        string? state,
        string? zipCode)
    {
        _state.State.StreetAddress1 = streetAddress1;
        _state.State.StreetAddress2 = streetAddress2;
        _state.State.StreetAddress3 = streetAddress3;
        _state.State.City = city;
        _state.State.State = state;
        _state.State.ZipCode = zipCode;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateContactInfoAsync(
        string? phoneResidence,
        string? phoneWork,
        string? email)
    {
        _state.State.PhoneNumberResidence = phoneResidence;
        _state.State.PhoneNumberWork = phoneWork;
        _state.State.Email = email;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateEmergencyContactAsync(
        string? name,
        string? relationship,
        string? phone)
    {
        _state.State.EmergencyContactName = name;
        _state.State.EmergencyContactRelationship = relationship;
        _state.State.EmergencyContactPhone = phone;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateVeteranInfoAsync(
        string veteran,
        int? serviceConnectedPercentage,
        string? eligibilityCode,
        string? primaryEligibilityCode)
    {
        _state.State.Veteran = veteran;
        _state.State.ServiceConnectedPercentage = serviceConnectedPercentage;
        _state.State.EligibilityCode = eligibilityCode;
        _state.State.PrimaryEligibilityCode = primaryEligibilityCode;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateMilitaryServiceAsync(
        DateTime? serviceEntryDate,
        DateTime? serviceSeparationDate,
        string? serviceBranch,
        string? dischargeType,
        string? prisonerOfWar)
    {
        _state.State.ServiceEntryDate = serviceEntryDate;
        _state.State.ServiceSeparationDate = serviceSeparationDate;
        _state.State.ServiceBranch = serviceBranch;
        _state.State.ServiceDischargeType = dischargeType;
        _state.State.PrisonerOfWar = prisonerOfWar;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddAppointmentAsync(DateTime appointmentDateTime)
    {
        if (!_state.State.Appointments.Contains(appointmentDateTime))
        {
            _state.State.Appointments.Add(appointmentDateTime);
            _state.State.Appointments.Sort();
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveAppointmentAsync(DateTime appointmentDateTime)
    {
        if (_state.State.Appointments.Remove(appointmentDateTime))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<DateTime>> GetAppointmentsAsync()
    {
        return Task.FromResult(_state.State.Appointments);
    }

    public async Task UpdateMaritalStatusAsync(string? maritalStatus)
    {
        _state.State.MaritalStatus = maritalStatus;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateReligiousPreferenceAsync(string? religiousPreference)
    {
        _state.State.ReligiousPreference = religiousPreference;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddRaceAsync(string race)
    {
        if (!_state.State.Race.Contains(race))
        {
            _state.State.Race.Add(race);
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task AddEthnicityAsync(string ethnicity)
    {
        if (!_state.State.Ethnicity.Contains(ethnicity))
        {
            _state.State.Ethnicity.Add(ethnicity);
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateCurrentAdmissionAsync(
        string? admissionId,
        string? roomBed,
        string? currentMovement)
    {
        _state.State.CurrentAdmission = admissionId;
        _state.State.RoomBed = roomBed;
        _state.State.CurrentMovement = currentMovement;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RecordDateOfDeathAsync(DateTime dateOfDeath)
    {
        _state.State.DateOfDeath = dateOfDeath;
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateBirthPlaceAsync(string? city, string? state)
    {
        _state.State.BirthCity = city;
        _state.State.BirthState = state;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<string> GetNameAsync()
    {
        return Task.FromResult(_state.State.Name);
    }

    public Task<bool> IsVeteranAsync()
    {
        return Task.FromResult(_state.State.Veteran?.ToUpper() == "Y");
    }

    public Task<int?> GetAgeAsync()
    {
        if (_state.State.DateOfBirth.HasValue)
        {
            var today = DateTime.Today;
            var age = today.Year - _state.State.DateOfBirth.Value.Year;

            if (_state.State.DateOfBirth.Value.Date > today.AddYears(-age))
            {
                age--;
            }

            return Task.FromResult<int?>(age);
        }

        return Task.FromResult<int?>(null);
    }

    public async Task AddAppointmentIdAsync(string appointmentId)
    {
        if (!_state.State.AppointmentIds.Contains(appointmentId))
        {
            _state.State.AppointmentIds.Add(appointmentId);
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveAppointmentIdAsync(string appointmentId)
    {
        if (_state.State.AppointmentIds.Remove(appointmentId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetAppointmentIdsAsync()
    {
        return Task.FromResult(_state.State.AppointmentIds);
    }

    public async Task AddLabTestIdAsync(string labTestId)
    {
        if (!_state.State.LabTestIds.Contains(labTestId))
        {
            _state.State.LabTestIds.Add(labTestId);
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveLabTestIdAsync(string labTestId)
    {
        if (_state.State.LabTestIds.Remove(labTestId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetLabTestIdsAsync()
    {
        return Task.FromResult(_state.State.LabTestIds);
    }

    public async Task AddOrderIdAsync(string orderId)
    {
        if (!_state.State.OrderIds.Contains(orderId))
        {
            _state.State.OrderIds.Add(orderId);
            _state.State.LastModifiedDate = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveOrderIdAsync(string orderId)
    {
        if (_state.State.OrderIds.Remove(orderId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetOrderIdsAsync()
    {
        return Task.FromResult(_state.State.OrderIds);
    }

    public async Task AddAllergyAsync(AllergyEntry entry)
    {
        if (_state.State.Allergies.Any(a => a.AllergyId == entry.AllergyId))
            return;

        // Defensive copy for the event payload — must not share a reference
        // with the live state, or future mutations to the live allergy would
        // retroactively rewrite the historical event.
        var evt = new AllergyRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = this.GetPrimaryKeyString(),
            OccurredUtc = DateTime.UtcNow,
            UserId = RequestContext.Get(RequestContextKeys.UserId) as string,
            UserName = RequestContext.Get(RequestContextKeys.UserName) as string,
            AllergyId = entry.AllergyId,
            Snapshot = entry.Clone()
        };

        _state.State.Allergies.Add(entry);
        _state.State.LastModifiedDate = evt.OccurredUtc;
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task RemoveAllergyAsync(string allergyId)
    {
        int removed = _state.State.Allergies.RemoveAll(a => a.AllergyId == allergyId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<AllergyEntry>> GetAllergiesAsync()
    {
        return Task.FromResult(_state.State.Allergies);
    }

    public Task<AllergyEntry?> GetAllergyAsync(string allergyId)
    {
        AllergyEntry? entry = _state.State.Allergies.FirstOrDefault(a => a.AllergyId == allergyId);
        return Task.FromResult(entry);
    }

    public async Task UpdateAllergyAsync(AllergyEntry updated)
    {
        int idx = _state.State.Allergies.FindIndex(a => a.AllergyId == updated.AllergyId);
        if (idx >= 0)
        {
            _state.State.Allergies[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- Embedded Problem List (event-sourced) ---
    public async Task AddProblemAsync(ProblemEntry entry)
    {
        if (_state.State.Problems.Any(p => p.ProblemId == entry.ProblemId))
            return;

        // Defensive copy for the event payload — must not share a reference
        // with the live state, or future mutations to the live problem (e.g.
        // status change on inactivation) would retroactively rewrite the
        // historical event and break the hash chain.
        var evt = new ProblemAddedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = this.GetPrimaryKeyString(),
            OccurredUtc = DateTime.UtcNow,
            UserId = RequestContext.Get(RequestContextKeys.UserId) as string,
            UserName = RequestContext.Get(RequestContextKeys.UserName) as string,
            ProblemId = entry.ProblemId,
            Snapshot = entry.Clone()
        };

        ApplyProblemAdded(evt, entry);
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));
        await _state.WriteStateAsync();

        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task RemoveProblemAsync(string problemId)
    {
        if (_state.State.Problems.RemoveAll(p => p.ProblemId == problemId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ProblemEntry>> GetProblemsAsync() => Task.FromResult(_state.State.Problems);

    public Task<ProblemEntry?> GetProblemAsync(string problemId)
        => Task.FromResult(_state.State.Problems.FirstOrDefault(p => p.ProblemId == problemId));

    public async Task UpdateProblemAsync(ProblemEntry updated)
    {
        int idx = _state.State.Problems.FindIndex(p => p.ProblemId == updated.ProblemId);
        if (idx >= 0)
        {
            _state.State.Problems[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task InactivateProblemAsync(string problemId, DateTime dateResolved)
    {
        int idx = _state.State.Problems.FindIndex(p => p.ProblemId == problemId);
        if (idx < 0) return;
        if (_state.State.Problems[idx].Status == "INACTIVE") return;

        var evt = new ProblemInactivatedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = this.GetPrimaryKeyString(),
            OccurredUtc = DateTime.UtcNow,
            UserId = RequestContext.Get(RequestContextKeys.UserId) as string,
            UserName = RequestContext.Get(RequestContextKeys.UserName) as string,
            ProblemId = problemId,
            DateResolved = dateResolved
        };

        ApplyProblemInactivated(evt);
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));
        await _state.WriteStateAsync();

        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private void ApplyProblemAdded(ProblemAddedV1 e, ProblemEntry liveEntry)
    {
        // Live state holds the caller's entry — they may mutate it (the
        // event's Snapshot is a separate clone and stays immutable).
        _state.State.Problems.Add(liveEntry);
        _state.State.LastModifiedDate = e.OccurredUtc;
    }

    private void ApplyProblemInactivated(ProblemInactivatedV1 e)
    {
        int idx = _state.State.Problems.FindIndex(p => p.ProblemId == e.ProblemId);
        if (idx < 0) return;
        ProblemEntry p = _state.State.Problems[idx];
        p.Status = "INACTIVE";
        p.DateResolved = e.DateResolved;
        p.LastModifiedDate = e.OccurredUtc;
        _state.State.Problems[idx] = p;
        _state.State.LastModifiedDate = e.OccurredUtc;
    }

    // --- Pharmacy ---
    public async Task AddPharmacyIdAsync(string pharmacyId)
    {
        if (!_state.State.PharmacyIds.Contains(pharmacyId))
        {
            _state.State.PharmacyIds.Add(pharmacyId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemovePharmacyIdAsync(string pharmacyId)
    {
        if (_state.State.PharmacyIds.Remove(pharmacyId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetPharmacyIdsAsync() => Task.FromResult(_state.State.PharmacyIds);

    // --- BCMA ---
    public async Task AddBcmaIdAsync(string bcmaId)
    {
        if (!_state.State.BcmaIds.Contains(bcmaId))
        {
            _state.State.BcmaIds.Add(bcmaId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveBcmaIdAsync(string bcmaId)
    {
        if (_state.State.BcmaIds.Remove(bcmaId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetBcmaIdsAsync() => Task.FromResult(_state.State.BcmaIds);

    // --- Radiology ---
    public async Task AddRadiologyIdAsync(string radiologyId)
    {
        if (!_state.State.RadiologyIds.Contains(radiologyId))
        {
            _state.State.RadiologyIds.Add(radiologyId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveRadiologyIdAsync(string radiologyId)
    {
        if (_state.State.RadiologyIds.Remove(radiologyId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetRadiologyIdsAsync() => Task.FromResult(_state.State.RadiologyIds);

    // --- Orders (Recent Cache) ---
    public async Task AddRecentOrderAsync(OrderSummary summary, int maxCount)
    {
        // Insert at the front (most recent first), then trim to maxCount
        _state.State.RecentOrders.Insert(0, summary);
        if (_state.State.RecentOrders.Count > maxCount)
            _state.State.RecentOrders.RemoveRange(maxCount, _state.State.RecentOrders.Count - maxCount);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<OrderSummary>> GetRecentOrdersAsync()
        => Task.FromResult(_state.State.RecentOrders);

    public async Task SetRecentOrdersAsync(List<OrderSummary> orders)
    {
        _state.State.RecentOrders = orders;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // --- Vitals (Recent Cache) ---
    public async Task AddRecentVitalAsync(VitalSummary summary, int maxCount)
    {
        // Insert at the front (most recent first), then trim to maxCount
        _state.State.RecentVitals.Insert(0, summary);
        if (_state.State.RecentVitals.Count > maxCount)
            _state.State.RecentVitals.RemoveRange(maxCount, _state.State.RecentVitals.Count - maxCount);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<VitalSummary>> GetRecentVitalsAsync()
        => Task.FromResult(_state.State.RecentVitals);

    public async Task SetRecentVitalsAsync(List<VitalSummary> vitals)
    {
        _state.State.RecentVitals = vitals;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // --- Vitals Legacy ---
    public async Task AddVitalIdAsync(string vitalId)
    {
        if (!_state.State.VitalIds.Contains(vitalId))
        {
            _state.State.VitalIds.Add(vitalId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetVitalIdsAsync() => Task.FromResult(_state.State.VitalIds);

    // --- TIU Documents ---
    public async Task AddTiuDocumentIdAsync(string documentId)
    {
        if (!_state.State.TiuDocumentIds.Contains(documentId))
        {
            _state.State.TiuDocumentIds.Add(documentId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTiuDocumentIdAsync(string documentId)
    {
        if (_state.State.TiuDocumentIds.Remove(documentId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetTiuDocumentIdsAsync() => Task.FromResult(_state.State.TiuDocumentIds);

    // --- Notes (Recent Cache) ---
    public async Task AddRecentNoteAsync(TiuNoteSummary summary, int maxCount)
    {
        // Insert at the front (most recent first), then trim to maxCount
        _state.State.RecentNotes.Insert(0, summary);
        if (_state.State.RecentNotes.Count > maxCount)
            _state.State.RecentNotes.RemoveRange(maxCount, _state.State.RecentNotes.Count - maxCount);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<TiuNoteSummary>> GetRecentNotesAsync()
        => Task.FromResult(_state.State.RecentNotes);

    public async Task SetRecentNotesAsync(List<TiuNoteSummary> notes)
    {
        _state.State.RecentNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // --- Consults ---
    public async Task AddConsultIdAsync(string consultId)
    {
        if (!_state.State.ConsultIds.Contains(consultId))
        {
            _state.State.ConsultIds.Add(consultId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveConsultIdAsync(string consultId)
    {
        if (_state.State.ConsultIds.Remove(consultId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetConsultIdsAsync() => Task.FromResult(_state.State.ConsultIds);

    // --- Surgery ---
    public async Task AddSurgeryIdAsync(string surgeryId)
    {
        if (!_state.State.SurgeryIds.Contains(surgeryId))
        {
            _state.State.SurgeryIds.Add(surgeryId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveSurgeryIdAsync(string surgeryId)
    {
        if (_state.State.SurgeryIds.Remove(surgeryId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetSurgeryIdsAsync() => Task.FromResult(_state.State.SurgeryIds);

    // --- Clinical Reminders ---
    public async Task AddClinicalReminderIdAsync(string reminderId)
    {
        if (!_state.State.ClinicalReminderIds.Contains(reminderId))
        {
            _state.State.ClinicalReminderIds.Add(reminderId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveClinicalReminderIdAsync(string reminderId)
    {
        if (_state.State.ClinicalReminderIds.Remove(reminderId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetClinicalReminderIdsAsync() => Task.FromResult(_state.State.ClinicalReminderIds);

    // --- Embedded Immunizations ---
    public async Task AddImmunizationAsync(ImmunizationEntry entry)
    {
        if (!_state.State.Immunizations.Any(i => i.ImmunizationId == entry.ImmunizationId))
        {
            _state.State.Immunizations.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveImmunizationAsync(string immunizationId)
    {
        if (_state.State.Immunizations.RemoveAll(i => i.ImmunizationId == immunizationId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ImmunizationEntry>> GetImmunizationsAsync() => Task.FromResult(_state.State.Immunizations);

    public Task<ImmunizationEntry?> GetImmunizationAsync(string immunizationId)
        => Task.FromResult(_state.State.Immunizations.FirstOrDefault(i => i.ImmunizationId == immunizationId));

    public async Task UpdateImmunizationAsync(ImmunizationEntry updated)
    {
        int idx = _state.State.Immunizations.FindIndex(i => i.ImmunizationId == updated.ImmunizationId);
        if (idx >= 0)
        {
            _state.State.Immunizations[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- Health Factors ---
    public async Task AddHealthFactorIdAsync(string healthFactorId)
    {
        if (!_state.State.HealthFactorIds.Contains(healthFactorId))
        {
            _state.State.HealthFactorIds.Add(healthFactorId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveHealthFactorIdAsync(string healthFactorId)
    {
        if (_state.State.HealthFactorIds.Remove(healthFactorId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetHealthFactorIdsAsync() => Task.FromResult(_state.State.HealthFactorIds);

    // --- Mental Health ---
    public async Task AddMentalHealthIdAsync(string instrumentId)
    {
        if (!_state.State.MentalHealthIds.Contains(instrumentId))
        {
            _state.State.MentalHealthIds.Add(instrumentId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveMentalHealthIdAsync(string instrumentId)
    {
        if (_state.State.MentalHealthIds.Remove(instrumentId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetMentalHealthIdsAsync() => Task.FromResult(_state.State.MentalHealthIds);

    // --- Embedded Diet Orders ---
    public async Task AddDietOrderAsync(DieteticsEntry entry)
    {
        if (!_state.State.DietOrders.Any(d => d.DieteticsId == entry.DieteticsId))
        {
            _state.State.DietOrders.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveDietOrderAsync(string dieteticsId)
    {
        if (_state.State.DietOrders.RemoveAll(d => d.DieteticsId == dieteticsId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<DieteticsEntry>> GetDietOrdersAsync() => Task.FromResult(_state.State.DietOrders);

    public Task<DieteticsEntry?> GetDietOrderAsync(string dieteticsId)
        => Task.FromResult(_state.State.DietOrders.FirstOrDefault(d => d.DieteticsId == dieteticsId));

    public async Task UpdateDietOrderAsync(DieteticsEntry updated)
    {
        int idx = _state.State.DietOrders.FindIndex(d => d.DieteticsId == updated.DieteticsId);
        if (idx >= 0)
        {
            _state.State.DietOrders[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- Embedded Prosthetics ---
    public async Task AddProstheticsItemAsync(ProstheticsEntry entry)
    {
        if (!_state.State.ProstheticsItems.Any(p => p.ProstheticsId == entry.ProstheticsId))
        {
            _state.State.ProstheticsItems.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveProstheticsItemAsync(string prostheticsId)
    {
        if (_state.State.ProstheticsItems.RemoveAll(p => p.ProstheticsId == prostheticsId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ProstheticsEntry>> GetProstheticsItemsAsync() => Task.FromResult(_state.State.ProstheticsItems);

    public Task<ProstheticsEntry?> GetProstheticsItemAsync(string prostheticsId)
        => Task.FromResult(_state.State.ProstheticsItems.FirstOrDefault(p => p.ProstheticsId == prostheticsId));

    public async Task UpdateProstheticsItemAsync(ProstheticsEntry updated)
    {
        int idx = _state.State.ProstheticsItems.FindIndex(p => p.ProstheticsId == updated.ProstheticsId);
        if (idx >= 0)
        {
            _state.State.ProstheticsItems[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- Imaging ---
    public async Task AddImagingIdAsync(string imagingId)
    {
        if (!_state.State.ImagingIds.Contains(imagingId))
        {
            _state.State.ImagingIds.Add(imagingId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveImagingIdAsync(string imagingId)
    {
        if (_state.State.ImagingIds.Remove(imagingId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetImagingIdsAsync() => Task.FromResult(_state.State.ImagingIds);

    // --- ADT Episodes ---
    public async Task AddAdtIdAsync(string adtId)
    {
        if (!_state.State.AdtIds.Contains(adtId))
        {
            _state.State.AdtIds.Add(adtId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveAdtIdAsync(string adtId)
    {
        if (_state.State.AdtIds.Remove(adtId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetAdtIdsAsync() => Task.FromResult(_state.State.AdtIds);

    // --- Embedded Means Tests ---
    public async Task AddMeansTestAsync(MeansTestEntry entry)
    {
        if (!_state.State.MeansTests.Any(m => m.MeansTestId == entry.MeansTestId))
        {
            _state.State.MeansTests.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveMeansTestAsync(string meansTestId)
    {
        if (_state.State.MeansTests.RemoveAll(m => m.MeansTestId == meansTestId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<MeansTestEntry>> GetMeansTestsAsync() => Task.FromResult(_state.State.MeansTests);

    public Task<MeansTestEntry?> GetMeansTestAsync(string meansTestId)
        => Task.FromResult(_state.State.MeansTests.FirstOrDefault(m => m.MeansTestId == meansTestId));

    public async Task UpdateMeansTestAsync(MeansTestEntry updated)
    {
        int idx = _state.State.MeansTests.FindIndex(m => m.MeansTestId == updated.MeansTestId);
        if (idx >= 0)
        {
            _state.State.MeansTests[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- Embedded Service Connected Conditions ---
    public async Task AddScConditionAsync(ScConditionEntry entry)
    {
        if (!_state.State.ScConditions.Any(s => s.ConditionId == entry.ConditionId))
        {
            _state.State.ScConditions.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveScConditionAsync(string conditionId)
    {
        if (_state.State.ScConditions.RemoveAll(s => s.ConditionId == conditionId) > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ScConditionEntry>> GetScConditionsAsync() => Task.FromResult(_state.State.ScConditions);

    public Task<ScConditionEntry?> GetScConditionAsync(string conditionId)
        => Task.FromResult(_state.State.ScConditions.FirstOrDefault(s => s.ConditionId == conditionId));

    public async Task UpdateScConditionAsync(ScConditionEntry updated)
    {
        int idx = _state.State.ScConditions.FindIndex(s => s.ConditionId == updated.ConditionId);
        if (idx >= 0)
        {
            _state.State.ScConditions[idx] = updated;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    // --- VistA Identity ---

    public async Task<PatientState> SetDfnAsync(string dfn)
    {
        _state.State.Dfn = dfn;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return _state.State;
    }

    public async Task<PatientState> SetIcnAsync(string icn)
    {
        _state.State.Icn = icn;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return _state.State;
    }

    // --- Sensitivity Flags ---

    public async Task UpdateSensitivityFlagsAsync(bool isSensitive, string? sensitivityLevel)
    {
        _state.State.IsSensitiveRecord = isSensitive;
        _state.State.SensitivityLevel = sensitivityLevel;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // --- Merge ---

    public async Task MarkAsMergedAsync(string survivingPatientId, string mergedByUserId)
    {
        _state.State.MergedIntoPatientId = survivingPatientId;
        _state.State.MergeDate = DateTime.UtcNow;
        _state.State.MergedByUserId = mergedByUserId;
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // --- Capped Domain ID Lists (recent window + full history in IPatientHistoryIndexGrain) ---

    /// <summary>
    /// Maps a PatientHistoryDomains constant to its ID list in state.
    /// Allergies are deliberately absent — never capped, never migrated.
    /// </summary>
    private List<string> GetDomainList(string domain) => domain switch
    {
        PatientHistoryDomains.Lab => _state.State.LabTestIds,
        PatientHistoryDomains.Consult => _state.State.ConsultIds,
        PatientHistoryDomains.Surgery => _state.State.SurgeryIds,
        PatientHistoryDomains.Radiology => _state.State.RadiologyIds,
        PatientHistoryDomains.Bcma => _state.State.BcmaIds,
        PatientHistoryDomains.Imaging => _state.State.ImagingIds,
        PatientHistoryDomains.Adt => _state.State.AdtIds,
        PatientHistoryDomains.HealthFactor => _state.State.HealthFactorIds,
        PatientHistoryDomains.MentalHealth => _state.State.MentalHealthIds,
        PatientHistoryDomains.Reminder => _state.State.ClinicalReminderIds,
        PatientHistoryDomains.Pharmacy => _state.State.PharmacyIds,
        PatientHistoryDomains.Tiu => _state.State.TiuDocumentIds,
        PatientHistoryDomains.Order => _state.State.OrderIds,
        PatientHistoryDomains.Appointment => _state.State.AppointmentIds,
        _ => throw new ArgumentException($"Unknown history domain '{domain}'.", nameof(domain))
    };

    public Task<List<string>> GetDomainIdsAsync(string domain)
        => Task.FromResult(new List<string>(GetDomainList(domain)));

    public Task<bool> IsDomainMigratedAsync(string domain)
        => Task.FromResult(_state.State.HistoryMigratedDomains.Contains(domain));

    public async Task AddDomainIdCappedAsync(string domain, string id, int maxCount)
    {
        List<string> list = GetDomainList(domain);
        if (list.Contains(id))
            return;

        list.Add(id);

        // Trim only after migration: until the full list has been flushed to
        // the history index, trimming would lose the only copy of those IDs.
        if (_state.State.HistoryMigratedDomains.Contains(domain) && list.Count > maxCount)
            list.RemoveRange(0, list.Count - maxCount);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkDomainMigratedAndTrimAsync(string domain, int maxCount)
    {
        List<string> list = GetDomainList(domain);

        bool changed = _state.State.HistoryMigratedDomains.Add(domain);

        // Lists append chronologically, so trimming from the front keeps the
        // most recent maxCount entries.
        if (list.Count > maxCount)
        {
            list.RemoveRange(0, list.Count - maxCount);
            changed = true;
        }

        if (changed)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}
