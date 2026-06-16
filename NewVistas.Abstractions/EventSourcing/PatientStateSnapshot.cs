// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Adt;
using NewVistas.Abstractions.Events.Clinical.Allergies;
using NewVistas.Abstractions.Events.Clinical.Consults;
using NewVistas.Abstractions.Events.Clinical.Labs;
using NewVistas.Abstractions.Events.Clinical.MentalHealth;
using NewVistas.Abstractions.Events.Clinical.Notes;
using NewVistas.Abstractions.Events.Clinical.Orders;
using NewVistas.Abstractions.Events.Clinical.Prescriptions;
using NewVistas.Abstractions.Events.Clinical.Scheduling;
using NewVistas.Abstractions.Events.Clinical.Vitals;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.EventSourcing;

/// <summary>
/// Projection state held by the per-patient clinical event stream grain
/// (a <see cref="Orleans.EventSourcing.JournaledGrain{TGrainState, TEventBase}"/>).
///
/// Built up by replaying every confirmed envelope in the patient's chain through
/// <see cref="Apply"/>. Exposes the projection slices used by forensic replay
/// (e.g., problems list as-of T) without going back to the per-domain grains.
///
/// As new clinical domains adopt event sourcing, additional slices and Apply
/// branches are added below — never remove or reorder existing <c>[Id]</c> fields.
/// </summary>
[GenerateSerializer]
public class PatientStateSnapshot
{
    /// <summary>Patient ICN/ID — set when the first envelope is applied.</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Hash of the most recently applied envelope. Genesis if no events yet.</summary>
    [Id(1)]
    public string LastEventHash { get; set; } = HashChain.GenesisHash;

    /// <summary>
    /// Bounded list of recently applied event IDs used for idempotency dedupe
    /// inside <see cref="Apply"/>. Capped to the most recent 1000 IDs to avoid
    /// unbounded growth — older duplicates are extremely unlikely once a chain
    /// has thousands of intervening events.
    /// </summary>
    [Id(2)]
    public List<string> RecentEventIds { get; set; } = new();

    // ── Projection slices (one per domain) ────────────────────────────────

    /// <summary>Active + inactive problems projected from the patient's chain.</summary>
    [Id(3)]
    public List<ProblemEntry> Problems { get; set; } = new();

    /// <summary>Orders projected from the patient's chain — by order id, current state.</summary>
    [Id(4)]
    public List<OrderState> Orders { get; set; } = new();

    /// <summary>Prescriptions projected from the patient's chain — by Rx id, current state.</summary>
    [Id(5)]
    public List<PharmacyState> Prescriptions { get; set; } = new();

    /// <summary>TIU notes projected from the patient's chain — by document id, current state.</summary>
    [Id(6)]
    public List<TiuDocumentState> Notes { get; set; } = new();

    /// <summary>Allergies projected from the patient's chain — by allergy id.</summary>
    [Id(7)]
    public List<AllergyEntry> Allergies { get; set; } = new();

    /// <summary>Mental-health instrument administrations projected from the patient's chain.</summary>
    [Id(8)]
    public List<MentalHealthState> MentalHealthInstruments { get; set; } = new();

    /// <summary>Lab tests projected from the patient's chain — by lab test id, current state.</summary>
    [Id(9)]
    public List<LabTestState> LabTests { get; set; } = new();

    /// <summary>Vital measurements projected from the patient's chain — by vital id.</summary>
    [Id(10)]
    public List<VitalState> Vitals { get; set; } = new();

    /// <summary>Consults projected from the patient's chain — by consult id.</summary>
    [Id(11)]
    public List<ConsultState> Consults { get; set; } = new();

    /// <summary>Appointments projected from the patient's chain — by appointment id.</summary>
    [Id(12)]
    public List<AppointmentState> Appointments { get; set; } = new();

    /// <summary>ADT movements projected from the patient's chain — by movement id.</summary>
    [Id(13)]
    public List<AdtState> AdtMovements { get; set; } = new();

    // ── Behavior ──────────────────────────────────────────────────────────

    private const int RecentEventIdCap = 1000;

    /// <summary>True if an envelope with this <paramref name="eventId"/> has already been applied.</summary>
    public bool HasEventId(string eventId) => RecentEventIds.Contains(eventId);

    /// <summary>
    /// Apply an envelope to this snapshot. Idempotent on duplicate <c>EventId</c>.
    /// Updates <see cref="LastEventHash"/> from the envelope and dispatches the
    /// payload to the per-domain mutator.
    /// </summary>
    public void Apply(EventEnvelope envelope)
    {
        if (envelope.Payload is null) return;
        if (HasEventId(envelope.EventId)) return;

        if (string.IsNullOrEmpty(PatientId))
            PatientId = envelope.PatientId;

        switch (envelope.Payload)
        {
            case ProblemAddedV1 e:
                ApplyProblemAdded(e);
                break;
            case ProblemInactivatedV1 e:
                ApplyProblemInactivated(e);
                break;
            case OrderPlacedV1 e:
                ApplyOrderPlaced(e);
                break;
            case OrderSignedV1 e:
                ApplyOrderSigned(e);
                break;
            case OrderDiscontinuedV1 e:
                ApplyOrderDiscontinued(e);
                break;
            case OrderHeldV1 e:
                ApplyOrderHeld(e);
                break;
            case OrderReleasedV1 e:
                ApplyOrderReleased(e);
                break;
            case PrescriptionCreatedV1 e:
                ApplyPrescriptionCreated(e);
                break;
            case PrescriptionFilledV1 e:
                ApplyPrescriptionFilled(e);
                break;
            case PrescriptionRefilledV1 e:
                ApplyPrescriptionRefilled(e);
                break;
            case PrescriptionVerifiedV1 e:
                ApplyPrescriptionVerified(e);
                break;
            case PrescriptionDiscontinuedV1 e:
                ApplyPrescriptionDiscontinued(e);
                break;
            case NoteCreatedV1 e:
                ApplyNoteCreated(e);
                break;
            case NoteSignedV1 e:
                ApplyNoteSigned(e);
                break;
            case NoteCosignedV1 e:
                ApplyNoteCosigned(e);
                break;
            case AllergyRecordedV1 e:
                ApplyAllergyRecorded(e);
                break;
            case MentalHealthRecordedV1 e:
                ApplyMentalHealthRecorded(e);
                break;
            case MentalHealthRiskAssessedV1 e:
                ApplyMentalHealthRiskAssessed(e);
                break;
            case MentalHealthScoredV1 e:
                ApplyMentalHealthScored(e);
                break;
            case LabOrderedV1 e:
                ApplyLabOrdered(e);
                break;
            case SpecimenCollectedV1 e:
                ApplySpecimenCollected(e);
                break;
            case LabResultRecordedV1 e:
                ApplyLabResultRecorded(e);
                break;
            case LabResultVerifiedV1 e:
                ApplyLabResultVerified(e);
                break;
            case VitalRecordedV1 e:
                ApplyVitalRecorded(e);
                break;
            case ConsultRequestedV1 e:
                ApplyConsultRequested(e);
                break;
            case ConsultCompletedV1 e:
                ApplyConsultCompleted(e);
                break;
            case AppointmentScheduledV1 e:
                ApplyAppointmentScheduled(e);
                break;
            case AppointmentCheckedInV1 e:
                ApplyAppointmentCheckedIn(e);
                break;
            case AppointmentCheckedOutV1 e:
                ApplyAppointmentCheckedOut(e);
                break;
            case AppointmentCancelledV1 e:
                ApplyAppointmentCancelled(e);
                break;
            case AdmissionRecordedV1 e:
                ApplyAdmissionRecorded(e);
                break;
            case TransferRecordedV1 e:
                ApplyTransferRecorded(e);
                break;
            case DischargeRecordedV1 e:
                ApplyDischargeRecorded(e);
                break;
            // Add new event types here as additional clinical domains adopt event sourcing.
        }

        LastEventHash = envelope.EventHash;
        RecentEventIds.Add(envelope.EventId);
        if (RecentEventIds.Count > RecentEventIdCap)
            RecentEventIds.RemoveRange(0, RecentEventIds.Count - RecentEventIdCap);
    }

    private void ApplyProblemAdded(ProblemAddedV1 e)
    {
        if (Problems.Any(p => p.ProblemId == e.Snapshot.ProblemId))
            return;
        // Defensive clone — the projection must not share a reference with the
        // event's historical Snapshot. Subsequent in-place mutation by another
        // event (e.g. ProblemInactivatedV1) must affect only the projection.
        Problems.Add(e.Snapshot.Clone());
    }

    private void ApplyProblemInactivated(ProblemInactivatedV1 e)
    {
        int idx = Problems.FindIndex(p => p.ProblemId == e.ProblemId);
        if (idx < 0) return;
        ProblemEntry p = Problems[idx];
        p.Status = "INACTIVE";
        p.DateResolved = e.DateResolved;
        p.LastModifiedDate = e.OccurredUtc;
        Problems[idx] = p;
    }

    private void ApplyOrderPlaced(OrderPlacedV1 e)
    {
        if (Orders.Any(o => o.OrderId == e.OrderId)) return;
        // Defensive clone — projection must not share a reference with the
        // event's historical Snapshot.
        Orders.Add(e.Snapshot.Clone());
    }

    private void ApplyOrderSigned(OrderSignedV1 e)
    {
        int idx = Orders.FindIndex(o => o.OrderId == e.OrderId);
        if (idx < 0) return;
        OrderState o = Orders[idx];
        o.ElectronicSignature = e.ElectronicSignature;
        o.SignatureDateTime = e.SignatureDateTime;
        o.SignatureStatus = "E-SIGNED";
        o.LastModifiedDate = e.OccurredUtc;
        Orders[idx] = o;
    }

    private void ApplyOrderDiscontinued(OrderDiscontinuedV1 e)
    {
        int idx = Orders.FindIndex(o => o.OrderId == e.OrderId);
        if (idx < 0) return;
        OrderState o = Orders[idx];
        o.Status = "Discontinued";
        o.DiscontinuedDateTime = e.DiscontinuedDateTime;
        o.DiscontinuedReason = e.Reason;
        o.DiscontinuedByProviderId = e.DiscontinuedByProviderId;
        o.LastModifiedDate = e.OccurredUtc;
        Orders[idx] = o;
    }

    private void ApplyOrderHeld(OrderHeldV1 e)
    {
        int idx = Orders.FindIndex(o => o.OrderId == e.OrderId);
        if (idx < 0) return;
        OrderState o = Orders[idx];
        o.Status = "Hold";
        o.LastModifiedDate = e.OccurredUtc;
        Orders[idx] = o;
    }

    private void ApplyOrderReleased(OrderReleasedV1 e)
    {
        int idx = Orders.FindIndex(o => o.OrderId == e.OrderId);
        if (idx < 0) return;
        OrderState o = Orders[idx];
        o.Status = "Active";
        o.ReleaseDateTime = e.ReleaseDateTime;
        o.LastModifiedDate = e.OccurredUtc;
        Orders[idx] = o;
    }

    private void ApplyPrescriptionCreated(PrescriptionCreatedV1 e)
    {
        if (Prescriptions.Any(p => p.PrescriptionId == e.PrescriptionId)) return;
        Prescriptions.Add(e.Snapshot.Clone());
    }

    private void ApplyPrescriptionFilled(PrescriptionFilledV1 e)
    {
        int idx = Prescriptions.FindIndex(p => p.PrescriptionId == e.PrescriptionId);
        if (idx < 0) return;
        PharmacyState rx = Prescriptions[idx];
        rx.FillDate = e.FillDate;
        rx.LastDispenseDate = e.FillDate;
        rx.RxNumber = e.RxNumber ?? rx.RxNumber;
        if (rx.DaysSupply.HasValue)
            rx.ExpirationDate = e.FillDate.AddDays(rx.DaysSupply.Value);
        rx.RefillHistory.Add(new RefillRecord
        {
            FillNumber = 0,
            FillDate = e.FillDate,
            Quantity = e.Quantity,
            DaysSupply = e.DaysSupply,
            RxNumber = e.RxNumber
        });
        rx.LastModifiedDate = e.OccurredUtc;
        Prescriptions[idx] = rx;
    }

    private void ApplyPrescriptionRefilled(PrescriptionRefilledV1 e)
    {
        int idx = Prescriptions.FindIndex(p => p.PrescriptionId == e.PrescriptionId);
        if (idx < 0) return;
        PharmacyState rx = Prescriptions[idx];
        rx.LastDispenseDate = e.FillDate;
        rx.RefillsRemaining = e.RefillsRemainingAfter;
        if (rx.DaysSupply.HasValue)
            rx.ExpirationDate = e.FillDate.AddDays(rx.DaysSupply.Value);
        rx.RefillHistory.Add(new RefillRecord
        {
            FillNumber = e.FillNumber,
            FillDate = e.FillDate,
            Quantity = e.Quantity,
            DaysSupply = e.DaysSupply,
            RxNumber = e.RxNumber
        });
        rx.LastModifiedDate = e.OccurredUtc;
        Prescriptions[idx] = rx;
    }

    private void ApplyPrescriptionVerified(PrescriptionVerifiedV1 e)
    {
        int idx = Prescriptions.FindIndex(p => p.PrescriptionId == e.PrescriptionId);
        if (idx < 0) return;
        PharmacyState rx = Prescriptions[idx];
        rx.IsVerified = true;
        rx.VerifiedBy = e.PharmacistId;
        rx.VerifiedDate = e.VerifiedDate;
        rx.LastModifiedDate = e.OccurredUtc;
        Prescriptions[idx] = rx;
    }

    private void ApplyPrescriptionDiscontinued(PrescriptionDiscontinuedV1 e)
    {
        int idx = Prescriptions.FindIndex(p => p.PrescriptionId == e.PrescriptionId);
        if (idx < 0) return;
        PharmacyState rx = Prescriptions[idx];
        rx.Status = "DISCONTINUED";
        rx.DiscontinueReason = e.Reason;
        rx.LastModifiedDate = e.OccurredUtc;
        Prescriptions[idx] = rx;
    }

    private void ApplyNoteCreated(NoteCreatedV1 e)
    {
        if (Notes.Any(n => n.DocumentId == e.DocumentId)) return;
        Notes.Add(e.Snapshot.Clone());
    }

    private void ApplyNoteSigned(NoteSignedV1 e)
    {
        int idx = Notes.FindIndex(n => n.DocumentId == e.DocumentId);
        if (idx < 0) return;
        TiuDocumentState n = Notes[idx];
        n.SignedDateTime = e.SignedDateTime;
        n.Status = e.ResultingStatus;
        n.LastModifiedDate = e.OccurredUtc;
        Notes[idx] = n;
    }

    private void ApplyNoteCosigned(NoteCosignedV1 e)
    {
        int idx = Notes.FindIndex(n => n.DocumentId == e.DocumentId);
        if (idx < 0) return;
        TiuDocumentState n = Notes[idx];
        n.CosignedDateTime = e.CosignedDateTime;
        n.Status = "COMPLETED";
        n.LastModifiedDate = e.OccurredUtc;
        Notes[idx] = n;
    }

    private void ApplyAllergyRecorded(AllergyRecordedV1 e)
    {
        if (Allergies.Any(a => a.AllergyId == e.Snapshot.AllergyId)) return;
        Allergies.Add(e.Snapshot.Clone());
    }

    private void ApplyMentalHealthRecorded(MentalHealthRecordedV1 e)
    {
        if (MentalHealthInstruments.Any(m => m.InstrumentId == e.InstrumentId)) return;
        MentalHealthInstruments.Add(e.Snapshot.Clone());
    }

    private void ApplyMentalHealthRiskAssessed(MentalHealthRiskAssessedV1 e)
    {
        int idx = MentalHealthInstruments.FindIndex(m => m.InstrumentId == e.InstrumentId);
        if (idx < 0) return;
        MentalHealthState m = MentalHealthInstruments[idx];
        m.RiskLevel = e.RiskLevel;
        m.RiskAssessmentNotes = e.RiskNotes;
        m.LastModifiedDate = e.OccurredUtc;
        MentalHealthInstruments[idx] = m;
    }

    private void ApplyMentalHealthScored(MentalHealthScoredV1 e)
    {
        int idx = MentalHealthInstruments.FindIndex(m => m.InstrumentId == e.InstrumentId);
        if (idx < 0) return;
        MentalHealthState m = MentalHealthInstruments[idx];
        m.TotalScore = e.TotalScore;
        m.ScoreInterpretation = e.ScoreInterpretation;
        m.IsPositiveScreen = e.IsPositiveScreen;
        m.ScoringMethodUsed = e.ScoringMethod;
        m.LastModifiedDate = e.OccurredUtc;
        MentalHealthInstruments[idx] = m;
    }

    private void ApplyLabOrdered(LabOrderedV1 e)
    {
        if (LabTests.Any(t => t.LabTestId == e.LabTestId)) return;
        LabTests.Add(e.Snapshot.Clone());
    }

    private void ApplySpecimenCollected(SpecimenCollectedV1 e)
    {
        int idx = LabTests.FindIndex(t => t.LabTestId == e.LabTestId);
        if (idx < 0) return;
        LabTestState t = LabTests[idx];
        t.CollectionDateTime = e.CollectionDateTime;
        t.CollectionSample = e.CollectionSample;
        t.PerformingLab = e.PerformingLab;
        t.Status = "Collected";
        t.LastModifiedDate = e.OccurredUtc;
        LabTests[idx] = t;
    }

    private void ApplyLabResultRecorded(LabResultRecordedV1 e)
    {
        int idx = LabTests.FindIndex(t => t.LabTestId == e.LabTestId);
        if (idx < 0) return;
        LabTestState t = LabTests[idx];
        t.ResultDateTime = e.ResultDateTime;
        t.ResultValue = e.ResultValue;
        t.ResultUnit = e.ResultUnit;
        t.ReferenceRangeLow = e.ReferenceRangeLow;
        t.ReferenceRangeHigh = e.ReferenceRangeHigh;
        t.AbnormalFlag = e.AbnormalFlag;
        t.Status = "Pending";
        t.LastModifiedDate = e.OccurredUtc;
        LabTests[idx] = t;
    }

    private void ApplyLabResultVerified(LabResultVerifiedV1 e)
    {
        int idx = LabTests.FindIndex(t => t.LabTestId == e.LabTestId);
        if (idx < 0) return;
        LabTestState t = LabTests[idx];
        t.VerifyingProviderId = e.VerifyingProviderId;
        t.VerifyingProviderName = e.VerifyingProviderName;
        t.VerifiedDateTime = e.VerifiedDateTime;
        t.Status = "Completed";
        t.LastModifiedDate = e.OccurredUtc;
        LabTests[idx] = t;
    }

    private void ApplyVitalRecorded(VitalRecordedV1 e)
    {
        if (Vitals.Any(v => v.VitalId == e.VitalId)) return;
        Vitals.Add(e.Snapshot.Clone());
    }

    private void ApplyConsultRequested(ConsultRequestedV1 e)
    {
        if (Consults.Any(c => c.ConsultId == e.ConsultId)) return;
        Consults.Add(e.Snapshot.Clone());
    }

    private void ApplyConsultCompleted(ConsultCompletedV1 e)
    {
        int idx = Consults.FindIndex(c => c.ConsultId == e.ConsultId);
        if (idx < 0) return;
        ConsultState c = Consults[idx];
        c.Status = "COMPLETE";
        c.CompletedDateTime = e.CompletedDateTime;
        c.ResultDocumentId = e.ResultDocumentId;
        c.LastModifiedDate = e.OccurredUtc;
        Consults[idx] = c;
    }

    private void ApplyAppointmentScheduled(AppointmentScheduledV1 e)
    {
        if (Appointments.Any(a => a.AppointmentId == e.AppointmentId)) return;
        Appointments.Add(e.Snapshot.Clone());
    }

    private void ApplyAppointmentCheckedIn(AppointmentCheckedInV1 e)
    {
        int idx = Appointments.FindIndex(a => a.AppointmentId == e.AppointmentId);
        if (idx < 0) return;
        AppointmentState a = Appointments[idx];
        a.CheckInDateTime = e.CheckInDateTime;
        a.Status = "Checked In";
        a.LastModifiedDate = e.OccurredUtc;
        Appointments[idx] = a;
    }

    private void ApplyAppointmentCheckedOut(AppointmentCheckedOutV1 e)
    {
        int idx = Appointments.FindIndex(a => a.AppointmentId == e.AppointmentId);
        if (idx < 0) return;
        AppointmentState a = Appointments[idx];
        a.CheckOutDateTime = e.CheckOutDateTime;
        a.Status = "Checked Out";
        a.LastModifiedDate = e.OccurredUtc;
        Appointments[idx] = a;
    }

    private void ApplyAppointmentCancelled(AppointmentCancelledV1 e)
    {
        int idx = Appointments.FindIndex(a => a.AppointmentId == e.AppointmentId);
        if (idx < 0) return;
        AppointmentState a = Appointments[idx];
        a.Status = "Cancelled";
        a.CancellationReason = e.CancellationReason;
        a.CancellationDateTime = e.CancellationDateTime;
        a.LastModifiedDate = e.OccurredUtc;
        Appointments[idx] = a;
    }

    private void ApplyAdmissionRecorded(AdmissionRecordedV1 e)
    {
        if (AdtMovements.Any(m => m.MovementId == e.MovementId)) return;
        AdtMovements.Add(e.Snapshot.Clone());
    }

    private void ApplyTransferRecorded(TransferRecordedV1 e)
    {
        if (AdtMovements.Any(m => m.MovementId == e.MovementId)) return;
        AdtMovements.Add(e.Snapshot.Clone());
    }

    private void ApplyDischargeRecorded(DischargeRecordedV1 e)
    {
        int idx = AdtMovements.FindIndex(m => m.MovementId == e.MovementId);
        if (idx < 0) return;
        AdtState m = AdtMovements[idx];
        m.TransactionType = "DISCHARGE";
        m.DischargeDateTime = e.DischargeDateTime;
        m.DischargeDiagnosis = e.DischargeDiagnosis;
        m.Disposition = e.Disposition;
        m.LengthOfStay = e.LengthOfStay;
        m.LastModifiedDate = e.OccurredUtc;
        AdtMovements[idx] = m;
    }
}
