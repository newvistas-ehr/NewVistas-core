// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Medicine (Procedures) — Files #691-699 ─────────────────────────────────

    private IMedProcedureGrain MedProc(string procedureId)
        => GrainFactory.GetGrain<IMedProcedureGrain>(procedureId);

    private IMedProcedureIndexGrain MedProcIndex()
        => GrainFactory.GetGrain<IMedProcedureIndexGrain>($"MED-PROC-IDX:{PatientId}");

    private static GrainStates.MedProcedureIndexEntry BuildMedProcIndexEntry(GrainStates.MedProcedureState s) =>
        new()
        {
            ProcedureId          = s.ProcedureId,
            Category             = s.Category,
            ProcedureCode        = s.ProcedureCode,
            ProcedureDescription = s.ProcedureDescription,
            Status               = s.Status,
            OrderedDate          = s.OrderedDate,
            PerformedDate        = s.PerformedDate,
            ProviderName         = s.ProviderName,
            LocationName         = s.LocationName,
            Impression           = s.Impression?.Length > 120 ? s.Impression[..120] + "…" : s.Impression
        };

    public async Task<string> OrderMedProcedureAsync(
        GrainStates.MedProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication)
    {
        string procedureId = $"MED-PROC:{Guid.NewGuid()}";
        IMedProcedureGrain proc = MedProc(procedureId);
        await proc.OrderProcedureAsync(
            PatientId, category, procedureCode, procedureDescription,
            orderedDate, providerId, providerName, locationId, locationName, indication);
        GrainStates.MedProcedureState state = await proc.GetProcedureAsync();
        await MedProcIndex().UpsertProcedureAsync(BuildMedProcIndexEntry(state));
        return procedureId;
    }

    public async Task ScheduleMedProcedureAsync(string procedureId, DateTime scheduledDate)
    {
        await MedProc(procedureId).ScheduleProcedureAsync(scheduledDate);
        GrainStates.MedProcedureState state = await MedProc(procedureId).GetProcedureAsync();
        await MedProcIndex().UpsertProcedureAsync(BuildMedProcIndexEntry(state));
    }

    public async Task CompleteMedProcedureAsync(
        string procedureId,
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes)
    {
        await MedProc(procedureId).CompleteProcedureAsync(performedDate, findings, impression, notes);
        GrainStates.MedProcedureState state = await MedProc(procedureId).GetProcedureAsync();
        await MedProcIndex().UpsertProcedureAsync(BuildMedProcIndexEntry(state));
    }

    public async Task CancelMedProcedureAsync(string procedureId, string? reason)
    {
        await MedProc(procedureId).CancelProcedureAsync(reason);
        GrainStates.MedProcedureState state = await MedProc(procedureId).GetProcedureAsync();
        await MedProcIndex().UpsertProcedureAsync(BuildMedProcIndexEntry(state));
    }

    public async Task RecordMedEcgResultsAsync(
        string procedureId,
        int? rate,
        GrainStates.CardiacRhythm? rhythm,
        int? prIntervalMs,
        int? qrsDurationMs,
        int? qtcMs,
        int? axisDegrees,
        string? interpretation,
        bool? isNormal)
    {
        await MedProc(procedureId).RecordEcgResultsAsync(
            rate, rhythm, prIntervalMs, qrsDurationMs, qtcMs, axisDegrees, interpretation, isNormal);
    }

    public async Task RecordMedEchoResultsAsync(
        string procedureId,
        decimal? lvEjectionFraction,
        string? lvDiastolicFunction,
        string? valvularFindings)
    {
        await MedProc(procedureId).RecordEchoResultsAsync(lvEjectionFraction, lvDiastolicFunction, valvularFindings);
    }

    public async Task RecordMedStressTestResultsAsync(
        string procedureId,
        decimal? peakMets,
        decimal? targetHeartRatePct,
        bool? inducibleIschemia)
    {
        await MedProc(procedureId).RecordStressTestResultsAsync(peakMets, targetHeartRatePct, inducibleIschemia);
    }

    public async Task RecordMedPftResultsAsync(
        string procedureId,
        decimal? fev1,
        decimal? fev1PctPredicted,
        decimal? fvc,
        decimal? fvcPctPredicted,
        decimal? fev1FvcRatio,
        decimal? dlco,
        decimal? dlcoPctPredicted,
        decimal? tlc,
        decimal? rv,
        bool? obstructive,
        bool? restrictive,
        bool? bronchodilatorResponse)
    {
        await MedProc(procedureId).RecordPftResultsAsync(
            fev1, fev1PctPredicted, fvc, fvcPctPredicted, fev1FvcRatio,
            dlco, dlcoPctPredicted, tlc, rv, obstructive, restrictive, bronchodilatorResponse);
    }

    public async Task RecordMedAbgResultsAsync(
        string procedureId,
        decimal? ph,
        decimal? pao2,
        decimal? paco2,
        decimal? hco3,
        decimal? sao2)
    {
        await MedProc(procedureId).RecordAbgResultsAsync(ph, pao2, paco2, hco3, sao2);
    }

    public async Task RecordMedEndoscopyResultsAsync(
        string procedureId,
        GrainStates.EndoscopyType endoscopyType,
        GrainStates.BowelPrepQuality? bowelPrepQuality,
        bool? cecumReached,
        int? scopeAdvancedCm,
        bool? biopsyTaken,
        List<string>? biopsySites,
        int? polypCount,
        List<string>? polypDescriptions,
        List<string>? endoscopicInterventions)
    {
        await MedProc(procedureId).RecordEndoscopyResultsAsync(
            endoscopyType, bowelPrepQuality, cecumReached, scopeAdvancedCm,
            biopsyTaken, biopsySites, polypCount, polypDescriptions, endoscopicInterventions);
        GrainStates.MedProcedureState state = await MedProc(procedureId).GetProcedureAsync();
        await MedProcIndex().UpsertProcedureAsync(BuildMedProcIndexEntry(state));
    }

    public Task<GrainStates.MedProcedureState> GetMedProcedureAsync(string procedureId)
        => MedProc(procedureId).GetProcedureAsync();

    public Task<List<GrainStates.MedProcedureIndexEntry>> GetMedProceduresAsync()
        => MedProcIndex().GetAllProceduresAsync();

    public Task<List<GrainStates.MedProcedureIndexEntry>> GetMedProceduresByCategoryAsync(GrainStates.MedProcedureCategory category)
        => MedProcIndex().GetProceduresByCategoryAsync(category);

    public Task<List<GrainStates.MedProcedureIndexEntry>> GetCompletedMedProceduresAsync()
        => MedProcIndex().GetCompletedProceduresAsync();

    // ─── Clinical Procedures — File #702 ────────────────────────────────────────

    private IClinicProcedureGrain CpProc(string procedureId)
        => GrainFactory.GetGrain<IClinicProcedureGrain>(procedureId);

    private IClinicProcedureIndexGrain CpProcIndex()
        => GrainFactory.GetGrain<IClinicProcedureIndexGrain>($"CP-PROC-IDX:{PatientId}");

    private static GrainStates.ClinicProcedureIndexEntry BuildCpIndexEntry(GrainStates.ClinicProcedureState s) =>
        new()
        {
            ProcedureId          = s.ProcedureId,
            Category             = s.Category,
            ProcedureCode        = s.ProcedureCode,
            ProcedureDescription = s.ProcedureDescription,
            Status               = s.Status,
            OrderedDate          = s.OrderedDate,
            PerformedDate        = s.PerformedDate,
            ProviderName         = s.ProviderName,
            LocationName         = s.LocationName,
            Impression           = s.Impression?.Length > 120 ? s.Impression[..120] + "…" : s.Impression
        };

    public async Task<string> OrderClinicProcedureAsync(
        GrainStates.ClinicProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication)
    {
        string procedureId = $"CP-PROC:{Guid.NewGuid()}";
        IClinicProcedureGrain proc = CpProc(procedureId);
        await proc.OrderProcedureAsync(
            PatientId, category, procedureCode, procedureDescription,
            orderedDate, providerId, providerName, locationId, locationName, indication);
        GrainStates.ClinicProcedureState state = await proc.GetProcedureAsync();
        await CpProcIndex().UpsertProcedureAsync(BuildCpIndexEntry(state));
        return procedureId;
    }

    public async Task ScheduleClinicProcedureAsync(string procedureId, DateTime scheduledDate)
    {
        await CpProc(procedureId).ScheduleProcedureAsync(scheduledDate);
        GrainStates.ClinicProcedureState state = await CpProc(procedureId).GetProcedureAsync();
        await CpProcIndex().UpsertProcedureAsync(BuildCpIndexEntry(state));
    }

    public async Task CompleteClinicProcedureAsync(
        string procedureId,
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes)
    {
        await CpProc(procedureId).CompleteProcedureAsync(performedDate, findings, impression, notes);
        GrainStates.ClinicProcedureState state = await CpProc(procedureId).GetProcedureAsync();
        await CpProcIndex().UpsertProcedureAsync(BuildCpIndexEntry(state));
    }

    public async Task CancelClinicProcedureAsync(string procedureId, string? reason)
    {
        await CpProc(procedureId).CancelProcedureAsync(reason);
        GrainStates.ClinicProcedureState state = await CpProc(procedureId).GetProcedureAsync();
        await CpProcIndex().UpsertProcedureAsync(BuildCpIndexEntry(state));
    }

    public async Task RecordClinicEegResultsAsync(
        string procedureId,
        int? durationMinutes,
        string? background,
        GrainStates.EegAlertType? alertType,
        bool? seizureActivity,
        string? focalRegion,
        List<string>? activations)
    {
        await CpProc(procedureId).RecordEegResultsAsync(
            durationMinutes, background, alertType, seizureActivity, focalRegion, activations);
    }

    public async Task RecordClinicEmgResultsAsync(
        string procedureId,
        List<string>? musclesStudied,
        GrainStates.EmgFindingType? findingType,
        string? spontaneousActivity,
        string? mupDescription)
    {
        await CpProc(procedureId).RecordEmgResultsAsync(musclesStudied, findingType, spontaneousActivity, mupDescription);
    }

    public async Task RecordClinicNcsResultsAsync(
        string procedureId,
        List<string>? nervesStudied,
        decimal? meanMotorVelocity,
        decimal? meanSensoryVelocity,
        bool? fWavesObtained,
        GrainStates.EmgFindingType? findingType)
    {
        await CpProc(procedureId).RecordNcsResultsAsync(
            nervesStudied, meanMotorVelocity, meanSensoryVelocity, fWavesObtained, findingType);
    }

    public async Task RecordClinicSleepStudyResultsAsync(
        string procedureId,
        GrainStates.SleepStudyType studyType,
        GrainStates.SleepApneaType? apneaType,
        decimal? apneaHypopneaIndex,
        decimal? cpapPressureCmH2O,
        decimal? sleepEfficiencyPct,
        int? totalSleepTimeMin,
        decimal? sleepLatencyMin,
        decimal? remLatencyMin)
    {
        await CpProc(procedureId).RecordSleepStudyResultsAsync(
            studyType, apneaType, apneaHypopneaIndex, cpapPressureCmH2O,
            sleepEfficiencyPct, totalSleepTimeMin, sleepLatencyMin, remLatencyMin);
    }

    public async Task RecordClinicAudiometryResultsAsync(
        string procedureId,
        GrainStates.HearingLossType? hearingLossType,
        decimal? rightEarPta,
        decimal? leftEarPta,
        decimal? speechDiscriminationRight,
        decimal? speechDiscriminationLeft,
        string? tympanometryRight,
        string? tympanometryLeft)
    {
        await CpProc(procedureId).RecordAudiometryResultsAsync(
            hearingLossType, rightEarPta, leftEarPta,
            speechDiscriminationRight, speechDiscriminationLeft,
            tympanometryRight, tympanometryLeft);
    }

    public Task<GrainStates.ClinicProcedureState> GetClinicProcedureAsync(string procedureId)
        => CpProc(procedureId).GetProcedureAsync();

    public Task<List<GrainStates.ClinicProcedureIndexEntry>> GetClinicProceduresAsync()
        => CpProcIndex().GetAllProceduresAsync();

    public Task<List<GrainStates.ClinicProcedureIndexEntry>> GetClinicProceduresByCategoryAsync(GrainStates.ClinicProcedureCategory category)
        => CpProcIndex().GetProceduresByCategoryAsync(category);

    public Task<List<GrainStates.ClinicProcedureIndexEntry>> GetCompletedClinicProceduresAsync()
        => CpProcIndex().GetCompletedProceduresAsync();

    // ─── Radiation Therapy — File #135 ──────────────────────────────────────────

    private IRadiationTherapyCourseGrain RtCourse(string courseId)
        => GrainFactory.GetGrain<IRadiationTherapyCourseGrain>(courseId);

    private IRadiationTherapyCourseIndexGrain RtCourseIndex()
        => GrainFactory.GetGrain<IRadiationTherapyCourseIndexGrain>($"RT-COURSE-IDX:{PatientId}");

    private IRadiationTherapyTreatmentGrain RtTx(string treatmentId)
        => GrainFactory.GetGrain<IRadiationTherapyTreatmentGrain>(treatmentId);

    private IRadiationTherapyTreatmentIndexGrain RtTxIndex(string courseId)
        => GrainFactory.GetGrain<IRadiationTherapyTreatmentIndexGrain>($"RT-TX-IDX:{courseId}");

    private static GrainStates.RtCourseIndexEntry BuildRtCourseIndexEntry(GrainStates.RtCourseState s) =>
        new()
        {
            CourseId                 = s.CourseId,
            CourseName               = s.CourseName,
            Status                   = s.Status,
            Intent                   = s.Intent,
            Modality                 = s.Modality,
            TreatmentSite            = s.TreatmentSite,
            DiagnosisCode            = s.DiagnosisCode,
            PrescribedDoseCgy        = s.PrescribedDoseCgy,
            FractionsPlanned         = s.FractionsPlanned,
            TotalDeliveredDoseCgy    = s.TotalDeliveredDoseCgy,
            FractionsCompleted       = s.FractionsCompleted,
            TreatmentStartDate       = s.TreatmentStartDate,
            TreatmentCompletionDate  = s.TreatmentCompletionDate,
            OncologistName           = s.OncologistName
        };

    public async Task<string> CreateRtCourseAsync(
        string courseName,
        string diagnosisCode,
        string diagnosisText,
        string treatmentSite,
        GrainStates.RtLaterality laterality,
        GrainStates.RtIntent intent,
        GrainStates.RtModality modality,
        int prescribedDoseCgy,
        int fractionsPlanned,
        int dosePerFractionCgy,
        string? beamEnergy,
        string? oncologistId,
        string? oncologistName,
        string? physicistId,
        string? physicistName,
        string? dosimetristId,
        string? dosimetristName,
        string? treatmentMachineId,
        string? treatmentMachineName,
        string? planningNotes)
    {
        string courseId = $"RT-COURSE:{Guid.NewGuid()}";
        IRadiationTherapyCourseGrain course = RtCourse(courseId);
        await course.CreateCourseAsync(
            PatientId, courseName, diagnosisCode, diagnosisText,
            treatmentSite, laterality, intent, modality,
            prescribedDoseCgy, fractionsPlanned, dosePerFractionCgy, beamEnergy,
            oncologistId, oncologistName, physicistId, physicistName,
            dosimetristId, dosimetristName, treatmentMachineId, treatmentMachineName,
            planningNotes);
        GrainStates.RtCourseState state = await course.GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
        return courseId;
    }

    public async Task RecordRtSimulationAsync(string courseId, DateTime simulationDate, string? planningNotes)
    {
        await RtCourse(courseId).RecordSimulationAsync(simulationDate, planningNotes);
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task StartRtCourseAsync(string courseId, DateTime treatmentStartDate)
    {
        await RtCourse(courseId).StartCourseAsync(treatmentStartDate);
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task CompleteRtCourseAsync(string courseId, DateTime completionDate, string? notes)
    {
        await RtCourse(courseId).CompleteCourseAsync(completionDate, notes);
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task DiscontinueRtCourseAsync(string courseId, DateTime discontinuationDate, string reason, string? notes)
    {
        await RtCourse(courseId).DiscontinueCourseAsync(discontinuationDate, reason, notes);
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task PlaceRtCourseOnHoldAsync(string courseId, string? reason)
    {
        await RtCourse(courseId).PlaceCourseOnHoldAsync(reason);
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task ResumeRtCourseAsync(string courseId)
    {
        await RtCourse(courseId).ResumeCourseAsync();
        GrainStates.RtCourseState state = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(state));
    }

    public async Task SetRtBoostAsync(string courseId, string boostSite, int boostDoseCgy, int boostFractionsPlanned)
    {
        await RtCourse(courseId).SetBoostAsync(boostSite, boostDoseCgy, boostFractionsPlanned);
    }

    public async Task SetRtBrachytherapyAsync(string courseId, GrainStates.BrachytherapyDoseRate doseRate, string? isotope)
    {
        await RtCourse(courseId).SetBrachytherapyAsync(doseRate, isotope);
    }

    public async Task<string> RecordRtFractionAsync(
        string courseId,
        int fractionNumber,
        DateTime treatmentDate,
        int doseDeliveredCgy,
        int? treatmentDurationMin,
        string? machineId,
        string? machineName,
        string? technicianId,
        string? technicianName,
        bool setupVerified,
        string? setupMethod,
        decimal? setupDeviationMm,
        bool interrupted,
        string? interruptionReason,
        string? notes)
    {
        string treatmentId = $"RT-TX:{Guid.NewGuid()}";
        await RtTx(treatmentId).RecordDeliveryAsync(
            courseId, PatientId, fractionNumber, treatmentDate, doseDeliveredCgy,
            treatmentDurationMin, machineId, machineName, technicianId, technicianName,
            setupVerified, setupMethod, setupDeviationMm, interrupted, interruptionReason, notes);

        // Update per-course fraction index
        GrainStates.RtTreatmentState txState = await RtTx(treatmentId).GetTreatmentAsync();
        await RtTxIndex(courseId).UpsertTreatmentAsync(new GrainStates.RtTreatmentIndexEntry
        {
            TreatmentId    = txState.TreatmentId,
            FractionNumber = txState.FractionNumber,
            TreatmentDate  = txState.TreatmentDate,
            Status         = txState.Status,
            DoseDeliveredCgy = txState.DoseDeliveredCgy,
            MachineName    = txState.MachineName,
            TechnicianName = txState.TechnicianName,
            SetupVerified  = txState.SetupVerified,
            Notes          = txState.Notes
        });

        // Update course cumulative dose and sync index
        await RtCourse(courseId).RecordFractionDeliveredAsync(doseDeliveredCgy);
        GrainStates.RtCourseState courseState = await RtCourse(courseId).GetCourseAsync();
        await RtCourseIndex().UpsertCourseAsync(BuildRtCourseIndexEntry(courseState));

        return treatmentId;
    }

    public async Task<string> RecordRtSkippedFractionAsync(
        string courseId,
        int fractionNumber,
        DateTime scheduledDate,
        GrainStates.RtFractionStatus status,
        string? skipReason)
    {
        string treatmentId = $"RT-TX:{Guid.NewGuid()}";
        await RtTx(treatmentId).RecordSkipAsync(courseId, PatientId, fractionNumber, scheduledDate, status, skipReason);

        GrainStates.RtTreatmentState txState = await RtTx(treatmentId).GetTreatmentAsync();
        await RtTxIndex(courseId).UpsertTreatmentAsync(new GrainStates.RtTreatmentIndexEntry
        {
            TreatmentId    = txState.TreatmentId,
            FractionNumber = txState.FractionNumber,
            TreatmentDate  = txState.TreatmentDate,
            Status         = txState.Status,
            DoseDeliveredCgy = 0,
            MachineName    = null,
            TechnicianName = null,
            SetupVerified  = false,
            Notes          = skipReason
        });

        return treatmentId;
    }

    public Task<GrainStates.RtCourseState> GetRtCourseAsync(string courseId)
        => RtCourse(courseId).GetCourseAsync();

    public Task<List<GrainStates.RtCourseIndexEntry>> GetRtCoursesAsync()
        => RtCourseIndex().GetAllCoursesAsync();

    public Task<List<GrainStates.RtCourseIndexEntry>> GetActiveRtCoursesAsync()
        => RtCourseIndex().GetActiveCoursesAsync();

    public Task<List<GrainStates.RtTreatmentIndexEntry>> GetRtCourseTreatmentsAsync(string courseId)
        => RtTxIndex(courseId).GetAllTreatmentsAsync();

    public Task<List<GrainStates.RtTreatmentIndexEntry>> GetRtDeliveredFractionsAsync(string courseId)
        => RtTxIndex(courseId).GetDeliveredTreatmentsAsync();

    // ─── IV Pharmacy helpers ───────────────────────────────────────────────────

    private IIVAdmixOrderGrain IVOrder(string orderId)
        => GrainFactory.GetGrain<IIVAdmixOrderGrain>(orderId);

    private IIVAdmixOrderIndexGrain IVOrderIndex()
        => GrainFactory.GetGrain<IIVAdmixOrderIndexGrain>($"IVAD-ORDER-IDX:{PatientId}");

    private static GrainStates.IVAdmixOrderIndexEntry BuildIVAdmixIndexEntry(GrainStates.IVAdmixOrderState s) =>
        new()
        {
            OrderId        = s.OrderId,
            Status         = s.Status,
            Priority       = s.Priority,
            BaseSolution   = s.BaseSolution,
            TotalVolumeMl  = s.TotalVolumeMl,
            Route          = s.Route,
            InfusionRateStr = s.InfusionRateStr,
            Frequency      = s.Frequency,
            StartDateTime  = s.StartDateTime,
            StopDateTime   = s.StopDateTime,
            LotNumber      = s.LotNumber,
            ExpirationDate = s.ExpirationDate,
            ProviderName   = s.ProviderName,
            CreatedDate    = s.CreatedDate,
            AdditiveCount  = s.Additives.Count,
        };

    // ─── IV Pharmacy workflow methods ─────────────────────────────────────────

    public async Task<string> CreateIVAdmixOrderAsync(
        string baseSolution,
        int baseSolutionVolumeMl,
        GrainStates.IVAdmixRoute route,
        GrainStates.IVAdmixFrequency frequency,
        GrainStates.IVContainerType containerType,
        int containerCount,
        GrainStates.IVAdmixPriority priority,
        string? linkedInpatientOrderId,
        string? infusionRateStr,
        decimal? infusionRateMlHr,
        decimal? infusionDurationHours,
        string? routeDescription,
        string? frequencyDescription,
        DateTime? startDateTime,
        DateTime? stopDateTime,
        string? providerId,
        string? providerName,
        string? notes)
    {
        string orderId = $"IVAD-ORDER:{Guid.NewGuid()}";
        await IVOrder(orderId).CreateOrderAsync(
            PatientId, baseSolution, baseSolutionVolumeMl,
            route, frequency, containerType, containerCount, priority,
            linkedInpatientOrderId, infusionRateStr, infusionRateMlHr,
            infusionDurationHours, routeDescription, frequencyDescription,
            startDateTime, stopDateTime, providerId, providerName, notes);

        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
        return orderId;
    }

    public async Task AddIVAdmixAdditiveAsync(string orderId, GrainStates.IVAdmixAdditive additive)
    {
        await IVOrder(orderId).AddAdditiveAsync(additive);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task RemoveIVAdmixAdditiveAsync(string orderId, string drugName)
    {
        await IVOrder(orderId).RemoveAdditiveAsync(drugName);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task VerifyIVAdmixOrderAsync(string orderId, string pharmacistId, string pharmacistName, DateTime verifiedDate)
    {
        await IVOrder(orderId).VerifyOrderAsync(pharmacistId, pharmacistName, verifiedDate);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task StartIVAdmixCompoundingAsync(string orderId, string compoundedById, string compoundedByName, DateTime startDate)
    {
        await IVOrder(orderId).StartCompoundingAsync(compoundedById, compoundedByName, startDate);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task CompleteIVAdmixCompoundingAsync(string orderId, DateTime completedDate, string? lotNumber, DateTime? expirationDate)
    {
        await IVOrder(orderId).CompleteCompoundingAsync(completedDate, lotNumber, expirationDate);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task PrintIVAdmixLabelAsync(string orderId, string printedBy, DateTime printedDate)
    {
        await IVOrder(orderId).PrintLabelAsync(printedBy, printedDate);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task DispenseIVAdmixOrderAsync(string orderId, DateTime dispensingDateTime)
    {
        await IVOrder(orderId).DispenseOrderAsync(dispensingDateTime);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task RecordIVAdmixAdministrationAsync(string orderId, DateTime administrationDateTime)
    {
        await IVOrder(orderId).RecordAdministrationAsync(administrationDateTime);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task DiscontinueIVAdmixOrderAsync(string orderId, string reason)
    {
        await IVOrder(orderId).DiscontinueOrderAsync(reason);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public async Task CancelIVAdmixOrderAsync(string orderId, string reason)
    {
        await IVOrder(orderId).CancelOrderAsync(reason);
        GrainStates.IVAdmixOrderState state = await IVOrder(orderId).GetOrderAsync();
        await IVOrderIndex().UpsertOrderAsync(BuildIVAdmixIndexEntry(state));
    }

    public Task<GrainStates.IVAdmixOrderState> GetIVAdmixOrderAsync(string orderId)
        => IVOrder(orderId).GetOrderAsync();

    public Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetIVAdmixOrdersAsync()
        => IVOrderIndex().GetAllOrdersAsync();

    public Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetPendingIVAdmixOrdersAsync()
        => IVOrderIndex().GetPendingOrdersAsync();

    public Task<List<GrainStates.IVAdmixOrderIndexEntry>> GetActiveIVAdmixOrdersAsync()
        => IVOrderIndex().GetActiveOrdersAsync();

    // ──────────────────────────────────────────────────────────────────────────
    // Compensation & Pension — VistA File #396 (DVBAB5.m, DVBABEXT.m)
    // ──────────────────────────────────────────────────────────────────────────

    private ICPExamGrain CpExam(string examId)
        => GrainFactory.GetGrain<ICPExamGrain>(examId);

    private ICPExamIndexGrain CpExamIndex()
        => GrainFactory.GetGrain<ICPExamIndexGrain>($"CP-EXAM-IDX:{PatientId}");

    private IDBQGrain Dbq(string dbqId)
        => GrainFactory.GetGrain<IDBQGrain>(dbqId);

    private IDBQIndexGrain DbqIndex()
        => GrainFactory.GetGrain<IDBQIndexGrain>($"CP-DBQ-IDX:{PatientId}");

    private static GrainStates.CPExamIndexEntry BuildCpExamIndexEntry(GrainStates.CPExamState s) => new()
    {
        ExamId = s.ExamId,
        ExamType = s.ExamType,
        Status = s.Status,
        ScheduledDate = s.ScheduledDate,
        CompletedDate = s.CompletedDate,
        ExaminerName = s.ExaminerName,
        ClaimNumber = s.ClaimNumber,
        DisabilityCount = s.DisabilityClaimedCodes.Count,
        DbqCount = s.DbqIds.Count
    };

    private static GrainStates.DBQIndexEntry BuildDbqIndexEntry(GrainStates.DBQState s) => new()
    {
        DbqId = s.DbqId,
        ExamId = s.ExamId,
        DbqType = s.DbqType,
        DbqTitle = s.DbqTitle,
        ConditionClaimed = s.ConditionClaimed,
        Status = s.Status,
        ProposedRating = s.ProposedRating,
        ServiceConnectionType = s.ServiceConnectionType,
        CompletedDate = s.CompletedDate
    };

    public async Task<string> ScheduleCPExamAsync(
        string patientName,
        GrainStates.CPExamType examType,
        DateTime scheduledDate,
        string examinerName,
        string examinerTitle,
        GrainStates.CPExaminerType examinerType,
        string examLocation,
        string examFacility,
        string claimNumber,
        string benefitType,
        List<string> disabilityClaimedCodes,
        string createdBy)
    {
        string examId = $"CP-EXAM:{Guid.NewGuid()}";
        await CpExam(examId).ScheduleExamAsync(
            PatientId, patientName, examType, scheduledDate,
            examinerName, examinerTitle, examinerType,
            examLocation, examFacility, claimNumber, benefitType,
            disabilityClaimedCodes, createdBy);
        GrainStates.CPExamState state = await CpExam(examId).GetExamAsync();
        await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(state));
        return examId;
    }

    public async Task CompleteCPExamAsync(string examId, List<string> diagnoses, bool nexus, string nexusRationale)
    {
        await CpExam(examId).CompleteExamAsync(diagnoses, nexus, nexusRationale);
        GrainStates.CPExamState state = await CpExam(examId).GetExamAsync();
        await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(state));
    }

    public async Task CancelCPExamAsync(string examId, string cancellationReason)
    {
        await CpExam(examId).CancelExamAsync(cancellationReason);
        GrainStates.CPExamState state = await CpExam(examId).GetExamAsync();
        await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(state));
    }

    public async Task RescheduleCPExamAsync(string examId, DateTime newScheduledDate, string reason)
    {
        await CpExam(examId).RescheduleExamAsync(newScheduledDate, reason);
        GrainStates.CPExamState state = await CpExam(examId).GetExamAsync();
        await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(state));
    }

    public async Task<string> CreateDBQAsync(
        string examId,
        string patientName,
        GrainStates.DBQType dbqType,
        string dbqFormNumber,
        string dbqTitle,
        string claimNumber,
        string conditionClaimed,
        string diagnosisCode,
        string diagnosisDescription)
    {
        string dbqId = $"CP-DBQ:{Guid.NewGuid()}";
        await Dbq(dbqId).CreateDBQAsync(
            examId, PatientId, patientName, dbqType,
            dbqFormNumber, dbqTitle, claimNumber,
            conditionClaimed, diagnosisCode, diagnosisDescription);
        await CpExam(examId).AddDbqToExamAsync(dbqId);
        GrainStates.DBQState dbqState = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(dbqState));
        GrainStates.CPExamState examState = await CpExam(examId).GetExamAsync();
        await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(examState));
        return dbqId;
    }

    public async Task UpdateDBQSectionsAsync(
        string dbqId,
        string historySection,
        string symptomsSection,
        string functionalImpactSection,
        string rangeOfMotionSection,
        string mentalStatusSection,
        string diagnosticTestsSection)
    {
        await Dbq(dbqId).UpdateSectionsAsync(
            historySection, symptomsSection, functionalImpactSection,
            rangeOfMotionSection, mentalStatusSection, diagnosticTestsSection);
        GrainStates.DBQState state = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(state));
    }

    public async Task RecordDBQOpinionAsync(
        string dbqId,
        bool nexusOpinion,
        string nexusStatement,
        string opinionsSection,
        GrainStates.ServiceConnectionType serviceConnectionType,
        bool residualsPermanent,
        bool expectedImprovement)
    {
        await Dbq(dbqId).RecordOpinionAsync(
            nexusOpinion, nexusStatement, opinionsSection,
            serviceConnectionType, residualsPermanent, expectedImprovement);
        GrainStates.DBQState state = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(state));
    }

    public async Task SetDBQRatingAsync(string dbqId, int proposedRating)
    {
        await Dbq(dbqId).SetProposedRatingAsync(proposedRating);
        GrainStates.DBQState state = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(state));
    }

    public async Task CompleteDBQAsync(string dbqId)
    {
        await Dbq(dbqId).CompleteDBQAsync();
        GrainStates.DBQState state = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(state));
    }

    public async Task SignDBQAsync(string dbqId, string signedBy)
    {
        await Dbq(dbqId).SignDBQAsync(signedBy, DateTime.UtcNow);
        GrainStates.DBQState dbqState = await Dbq(dbqId).GetDBQAsync();
        await DbqIndex().UpsertDBQAsync(BuildDbqIndexEntry(dbqState));
        // Update the exam index to reflect the new DBQ count
        string examId = dbqState.ExamId;
        if (!string.IsNullOrEmpty(examId))
        {
            GrainStates.CPExamState examState = await CpExam(examId).GetExamAsync();
            await CpExamIndex().UpsertExamAsync(BuildCpExamIndexEntry(examState));
        }
    }

    public Task<GrainStates.CPExamState> GetCPExamAsync(string examId)
        => CpExam(examId).GetExamAsync();

    public Task<List<GrainStates.CPExamIndexEntry>> GetCPExamsAsync()
        => CpExamIndex().GetAllExamsAsync();

    public Task<List<GrainStates.CPExamIndexEntry>> GetScheduledCPExamsAsync()
        => CpExamIndex().GetScheduledExamsAsync();

    public Task<List<GrainStates.CPExamIndexEntry>> GetCompletedCPExamsAsync()
        => CpExamIndex().GetCompletedExamsAsync();

    public Task<GrainStates.DBQState> GetDBQAsync(string dbqId)
        => Dbq(dbqId).GetDBQAsync();

    public Task<List<GrainStates.DBQIndexEntry>> GetDBQsAsync()
        => DbqIndex().GetAllDBQsAsync();

    public Task<List<GrainStates.DBQIndexEntry>> GetDBQsForExamAsync(string examId)
        => DbqIndex().GetDBQsForExamAsync(examId);
}
