// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Blood Bank helpers + methods ────────────────────────────────────────────

    private IBloodBankPatientGrain BBPatient()
        => GrainFactory.GetGrain<IBloodBankPatientGrain>($"BB-PATIENT:{PatientId}");

    private ICrossmatchIndexGrain CrossmatchIndex()
        => GrainFactory.GetGrain<ICrossmatchIndexGrain>($"BB-XM-IDX:{PatientId}");

    private ITransfusionIndexGrain TransfusionIndex()
        => GrainFactory.GetGrain<ITransfusionIndexGrain>($"BB-TX-IDX:{PatientId}");

    public async Task<GrainStates.BloodBankPatientState> GetBloodBankPatientAsync()
    {
        await BBPatient().InitializeAsync(PatientId);
        return await BBPatient().GetAsync();
    }

    public async Task UpdateBloodTypeAsync(
        GrainStates.AboBloodType aboType,
        GrainStates.RhBloodType rhType,
        GrainStates.AntibodyScreenResult antibodyScreenResult,
        DateTime? antibodyScreenDate,
        string? directAntibodyTest,
        string? specialRequirements,
        string? notes)
    {
        await BBPatient().InitializeAsync(PatientId);
        await BBPatient().UpdateBloodTypeAsync(
            aboType, rhType, antibodyScreenResult, antibodyScreenDate,
            directAntibodyTest, specialRequirements, notes);
    }

    public async Task<string> RequestCrossmatchAsync(
        string unitId,
        GrainStates.CrossmatchUrgency urgency,
        string requestedByUserId,
        string requestedByUserName,
        string? notes)
    {
        // Fetch patient blood type for informational fields in the crossmatch record
        await BBPatient().InitializeAsync(PatientId);
        GrainStates.BloodBankPatientState bbPatient = await BBPatient().GetAsync();
        string patientAbo = bbPatient.AboType.ToString();
        string patientRh = bbPatient.RhType.ToString();

        // Fetch unit info for informational fields
        IBloodUnitGrain unit = GrainFactory.GetGrain<IBloodUnitGrain>($"BB-UNIT:{unitId}");
        GrainStates.BloodUnitState unitState = await unit.GetUnitAsync();
        string unitAbo = unitState.AboType.ToString();
        string unitRh = unitState.RhType.ToString();

        // Create crossmatch record
        string crossmatchId = $"BB-XM:{Guid.NewGuid()}";
        ICrossmatchGrain xm = GrainFactory.GetGrain<ICrossmatchGrain>(crossmatchId);
        await xm.CreateAsync(PatientId, unitId, urgency,
            requestedByUserId, requestedByUserName,
            patientAbo, patientRh, unitAbo, unitRh, notes);

        // Reserve the blood unit
        await unit.ReserveAsync(PatientId, crossmatchId);

        // Update the blood unit index
        IBloodUnitIndexGrain unitIndex = GrainFactory.GetGrain<IBloodUnitIndexGrain>("BB-UNIT-IDX");
        await unitIndex.AddOrUpdateAsync(new GrainStates.BloodUnitIndexEntry
        {
            UnitId             = unitId,
            ProductType        = unitState.ProductType,
            AboType            = unitState.AboType,
            RhType             = unitState.RhType,
            Status             = GrainStates.BloodUnitStatus.Reserved,
            ExpirationDate     = unitState.ExpirationDate,
            IsIrradiated       = unitState.IsIrradiated,
            IsLeukoreduced     = unitState.IsLeukoreduced,
            IsAntigenNegative  = unitState.IsAntigenNegative,
            AntigenNegativeFor = unitState.AntigenNegativeFor,
            ReservedForPatientId = PatientId
        });

        // Add crossmatch to patient index
        await CrossmatchIndex().AddOrUpdateAsync(new GrainStates.CrossmatchIndexEntry
        {
            CrossmatchId  = crossmatchId,
            UnitId        = unitId,
            ProductType   = unitState.ProductType.ToString(),
            Result        = GrainStates.CrossmatchResult.Pending,
            Urgency       = urgency,
            RequestedDate = DateTime.UtcNow,
            IsIssued      = false
        });

        return crossmatchId;
    }

    public Task<List<GrainStates.CrossmatchIndexEntry>> GetCrossmatchesAsync()
        => CrossmatchIndex().GetAllAsync();

    public async Task RecordCrossmatchResultAsync(
        string crossmatchId,
        GrainStates.CrossmatchResult result,
        GrainStates.CrossmatchMethod method,
        string technicianId,
        string technicianName,
        string? antibodyIdentification)
    {
        ICrossmatchGrain xm = GrainFactory.GetGrain<ICrossmatchGrain>(crossmatchId);
        await xm.RecordResultAsync(result, method, technicianId, technicianName, antibodyIdentification);

        // Update the patient index entry
        GrainStates.CrossmatchState xmState = await xm.GetCrossmatchAsync();
        await CrossmatchIndex().AddOrUpdateAsync(new GrainStates.CrossmatchIndexEntry
        {
            CrossmatchId  = crossmatchId,
            UnitId        = xmState.UnitId,
            ProductType   = string.Empty,
            Result        = result,
            Urgency       = xmState.Urgency,
            RequestedDate = xmState.RequestedDate,
            IsIssued      = false
        });
    }

    public async Task<string> StartTransfusionAsync(
        string crossmatchId,
        string unitId,
        string administeredByUserId,
        string administeredByUserName,
        string orderedByUserId,
        string orderedByUserName,
        string? infusionSite,
        string? preTransfusionVitals)
    {
        // Fetch unit info for type labels
        IBloodUnitGrain unit = GrainFactory.GetGrain<IBloodUnitGrain>($"BB-UNIT:{unitId}");
        GrainStates.BloodUnitState unitState = await unit.GetUnitAsync();

        // Create the transfusion record
        string transfusionId = $"BB-TX:{Guid.NewGuid()}";
        ITransfusionGrain tx = GrainFactory.GetGrain<ITransfusionGrain>(transfusionId);
        await tx.StartAsync(
            PatientId, unitId, crossmatchId,
            unitState.ProductType.ToString(),
            unitState.AboType.ToString(),
            unitState.RhType.ToString(),
            administeredByUserId, administeredByUserName,
            orderedByUserId, orderedByUserName,
            infusionSite, preTransfusionVitals);

        // Issue the crossmatch and mark the unit
        ICrossmatchGrain xm = GrainFactory.GetGrain<ICrossmatchGrain>(crossmatchId);
        await xm.IssueUnitAsync(administeredByUserId, administeredByUserName, transfusionId);
        await unit.MarkTransfusedAsync(PatientId, transfusionId, DateTime.UtcNow);

        // Update inventory index
        IBloodUnitIndexGrain unitIndex = GrainFactory.GetGrain<IBloodUnitIndexGrain>("BB-UNIT-IDX");
        await unitIndex.AddOrUpdateAsync(new GrainStates.BloodUnitIndexEntry
        {
            UnitId             = unitId,
            ProductType        = unitState.ProductType,
            AboType            = unitState.AboType,
            RhType             = unitState.RhType,
            Status             = GrainStates.BloodUnitStatus.Transfused,
            ExpirationDate     = unitState.ExpirationDate,
            IsIrradiated       = unitState.IsIrradiated,
            IsLeukoreduced     = unitState.IsLeukoreduced,
            IsAntigenNegative  = unitState.IsAntigenNegative,
            AntigenNegativeFor = unitState.AntigenNegativeFor
        });

        // Update crossmatch index
        GrainStates.CrossmatchState xmState = await xm.GetCrossmatchAsync();
        await CrossmatchIndex().AddOrUpdateAsync(new GrainStates.CrossmatchIndexEntry
        {
            CrossmatchId  = crossmatchId,
            UnitId        = unitId,
            ProductType   = unitState.ProductType.ToString(),
            Result        = xmState.Result,
            Urgency       = xmState.Urgency,
            RequestedDate = xmState.RequestedDate,
            IsIssued      = true
        });

        // Add to transfusion index and bump patient count
        await TransfusionIndex().AddOrUpdateAsync(new GrainStates.TransfusionIndexEntry
        {
            TransfusionId = transfusionId,
            UnitId        = unitId,
            ProductType   = unitState.ProductType.ToString(),
            AboType       = unitState.AboType.ToString(),
            RhType        = unitState.RhType.ToString(),
            StartDateTime = DateTime.UtcNow,
            Status        = GrainStates.TransfusionStatus.InProgress,
            ReactionType  = GrainStates.TransfusionReactionType.None
        });

        await BBPatient().IncrementTransfusionCountAsync();

        return transfusionId;
    }

    public async Task CompleteTransfusionAsync(
        string transfusionId,
        DateTime endDateTime,
        decimal? volumeML,
        string? postTransfusionVitals)
    {
        ITransfusionGrain tx = GrainFactory.GetGrain<ITransfusionGrain>(transfusionId);
        await tx.CompleteAsync(endDateTime, volumeML, postTransfusionVitals);

        GrainStates.TransfusionState txState = await tx.GetTransfusionAsync();
        await TransfusionIndex().AddOrUpdateAsync(new GrainStates.TransfusionIndexEntry
        {
            TransfusionId = transfusionId,
            UnitId        = txState.UnitId,
            ProductType   = txState.ProductType,
            AboType       = txState.AboType,
            RhType        = txState.RhType,
            StartDateTime = txState.StartDateTime,
            EndDateTime   = endDateTime,
            Status        = GrainStates.TransfusionStatus.Completed,
            ReactionType  = GrainStates.TransfusionReactionType.None
        });
    }

    public async Task StopTransfusionAsync(
        string transfusionId,
        DateTime endDateTime,
        string stopReason,
        GrainStates.TransfusionReactionType reactionType,
        string? reactionNotes)
    {
        ITransfusionGrain tx = GrainFactory.GetGrain<ITransfusionGrain>(transfusionId);
        await tx.StopAsync(endDateTime, stopReason, reactionType, reactionNotes);

        GrainStates.TransfusionState txState = await tx.GetTransfusionAsync();
        GrainStates.TransfusionStatus status = reactionType != GrainStates.TransfusionReactionType.None
            ? GrainStates.TransfusionStatus.Reaction
            : GrainStates.TransfusionStatus.Stopped;

        await TransfusionIndex().AddOrUpdateAsync(new GrainStates.TransfusionIndexEntry
        {
            TransfusionId = transfusionId,
            UnitId        = txState.UnitId,
            ProductType   = txState.ProductType,
            AboType       = txState.AboType,
            RhType        = txState.RhType,
            StartDateTime = txState.StartDateTime,
            EndDateTime   = endDateTime,
            Status        = status,
            ReactionType  = reactionType
        });
    }

    public Task<List<GrainStates.TransfusionIndexEntry>> GetTransfusionHistoryAsync()
        => TransfusionIndex().GetAllAsync();

    // ─── Anatomic Pathology ───────────────────────────────────────────────────

    private IAnatomicPathologyCaseIndexGrain APCaseIndex()
        => GrainFactory.GetGrain<IAnatomicPathologyCaseIndexGrain>($"AP-CASE-IDX:{PatientId}");

    private IAnatomicPathologyCaseGrain APCase(string caseId)
        => GrainFactory.GetGrain<IAnatomicPathologyCaseGrain>(caseId);

    public async Task<string> AccessionAPCaseAsync(
        GrainStates.APCaseType caseType,
        string accessionNumber,
        string? specimenSource,
        string? specimenDescription,
        string? specimenType,
        string? clinicalHistory,
        string? clinicalDiagnosis,
        string? referringProviderId,
        string? referringProviderName,
        string? collectionLocation,
        DateTime? dateCollected,
        DateTime dateReceived)
    {
        string caseId = $"AP-CASE:{Guid.NewGuid()}";
        await APCase(caseId).AccessionCaseAsync(
            PatientId, caseType, accessionNumber,
            specimenSource, specimenDescription, specimenType,
            clinicalHistory, clinicalDiagnosis,
            referringProviderId, referringProviderName,
            collectionLocation, dateCollected, dateReceived);

        await APCaseIndex().UpsertCaseAsync(new GrainStates.APCaseIndexEntry
        {
            CaseId          = caseId,
            AccessionNumber = accessionNumber,
            CaseType        = caseType,
            Status          = GrainStates.APCaseStatus.Received,
            DateReceived    = dateReceived,
            SpecimenSource  = specimenSource
        });

        return caseId;
    }

    public async Task RecordAPGrossDescriptionAsync(
        string caseId,
        string grossDescription,
        string? pathologistId,
        string? pathologistName,
        int? specimenPartCount,
        decimal? specimenWeightGrams,
        string? frozenSectionDiagnosis)
    {
        await APCase(caseId).RecordGrossDescriptionAsync(
            grossDescription, pathologistId, pathologistName,
            specimenPartCount, specimenWeightGrams, frozenSectionDiagnosis);

        GrainStates.AnatomicPathologyState s = await APCase(caseId).GetCaseAsync();
        await APCaseIndex().UpsertCaseAsync(BuildIndexEntry(s));
    }

    public async Task RecordAPMicroscopicDescriptionAsync(string caseId, string microscopicDescription)
    {
        await APCase(caseId).RecordMicroscopicDescriptionAsync(microscopicDescription);
    }

    public async Task SignOutAPDiagnosisAsync(
        string caseId,
        string diagnosis,
        List<string> diagnosisCodes,
        string pathologistId,
        string pathologistName,
        DateTime signOutDateTime)
    {
        await APCase(caseId).SignOutDiagnosisAsync(
            diagnosis, diagnosisCodes, pathologistId, pathologistName, signOutDateTime);

        GrainStates.AnatomicPathologyState s = await APCase(caseId).GetCaseAsync();
        await APCaseIndex().UpsertCaseAsync(BuildIndexEntry(s));
    }

    public async Task IssueAPPreliminaryDiagnosisAsync(
        string caseId,
        string preliminaryDiagnosis,
        string pathologistId,
        string pathologistName)
    {
        await APCase(caseId).IssuePreliminaryDiagnosisAsync(
            preliminaryDiagnosis, pathologistId, pathologistName);

        GrainStates.AnatomicPathologyState s = await APCase(caseId).GetCaseAsync();
        await APCaseIndex().UpsertCaseAsync(BuildIndexEntry(s));
    }

    public async Task AddAPAddendumAsync(
        string caseId,
        string addendumText,
        string pathologistId,
        string pathologistName)
    {
        await APCase(caseId).AddAddendumAsync(addendumText, pathologistId, pathologistName);

        GrainStates.AnatomicPathologyState s = await APCase(caseId).GetCaseAsync();
        await APCaseIndex().UpsertCaseAsync(BuildIndexEntry(s));
    }

    public async Task AmendAPDiagnosisAsync(
        string caseId,
        string correctedDiagnosis,
        List<string> correctedCodes,
        string amendmentReason,
        string pathologistId,
        string pathologistName)
    {
        await APCase(caseId).AmendDiagnosisAsync(
            correctedDiagnosis, correctedCodes, amendmentReason, pathologistId, pathologistName);

        GrainStates.AnatomicPathologyState s = await APCase(caseId).GetCaseAsync();
        await APCaseIndex().UpsertCaseAsync(BuildIndexEntry(s));
    }

    public async Task RecordAPCytologyDetailsAsync(
        string caseId,
        string? bethesdaCategory,
        string? specimenAdequacy)
    {
        await APCase(caseId).RecordCytologyDetailsAsync(bethesdaCategory, specimenAdequacy);
    }

    public async Task RecordAPAutopsyFindingsAsync(
        string caseId,
        string? causeOfDeath,
        string? underlyingCauseOfDeath,
        GrainStates.MannerOfDeath? mannerOfDeath,
        string? toxicologyFindings,
        decimal? bodyWeightKg,
        string? neuropathologyFindings)
    {
        await APCase(caseId).RecordAutopsyFindingsAsync(
            causeOfDeath, underlyingCauseOfDeath, mannerOfDeath,
            toxicologyFindings, bodyWeightKg, neuropathologyFindings);
    }

    public Task<GrainStates.AnatomicPathologyState> GetAPCaseAsync(string caseId)
        => APCase(caseId).GetCaseAsync();

    public Task<List<GrainStates.APCaseIndexEntry>> GetAPCasesAsync()
        => APCaseIndex().GetAllCasesAsync();

    public Task<List<GrainStates.APCaseIndexEntry>> GetAPCasesByTypeAsync(GrainStates.APCaseType caseType)
        => APCaseIndex().GetCasesByTypeAsync(caseType);

    private static GrainStates.APCaseIndexEntry BuildIndexEntry(GrainStates.AnatomicPathologyState s) =>
        new()
        {
            CaseId          = s.CaseId,
            AccessionNumber = s.AccessionNumber,
            CaseType        = s.CaseType,
            Status          = s.Status,
            DateReceived    = s.DateReceived,
            DateReported    = s.DateReported,
            SpecimenSource  = s.SpecimenSource,
            PrimaryDiagnosis = s.Diagnosis is not null && s.Diagnosis.Length > 80
                ? s.Diagnosis[..80] + "…"
                : s.Diagnosis,
            PathologistName = s.PathologistName
        };

    // ─── Nursing (Files #210-212) ─────────────────────────────────────────────

    private INursingAssessmentIndexGrain AssessmentIndex()
        => GrainFactory.GetGrain<INursingAssessmentIndexGrain>($"NURS-ASSESS-IDX:{PatientId}");

    private INursingAssessmentGrain AssessmentGrain(string assessmentId)
        => GrainFactory.GetGrain<INursingAssessmentGrain>($"NURS-ASSESS:{assessmentId}");

    private INursingCarePlanGrain CarePlanGrain()
        => GrainFactory.GetGrain<INursingCarePlanGrain>($"NURS-CAREPLAN:{PatientId}");

    private INursingAcuityGrain AcuityGrain()
        => GrainFactory.GetGrain<INursingAcuityGrain>($"NURS-ACUITY:{PatientId}");

    public async Task<string> CreateNursingAssessmentAsync(
        DateTime assessmentDateTime,
        string assessmentType,
        string nurseId,
        string nurseName,
        string? locationId,
        string? locationName,
        string? levelOfConsciousness,
        List<string>? orientation,
        string? breathSounds,
        string? oxygenTherapy,
        decimal? spO2,
        string? heartRhythm,
        string? edema,
        string? skinIntegrity,
        int? bradenScore,
        int? painScore,
        string? painLocation,
        string? bowelSounds,
        string? appetiteAssessment,
        decimal? urineOutput,
        bool hasFoley,
        string? anxietyLevel,
        string? mood,
        int? morseScore,
        string? fallRiskLevel,
        List<string>? fallPrecautions,
        string? adlMobility,
        string? narrativeNotes)
    {
        string assessmentId = Guid.NewGuid().ToString("N");

        GrainStates.NursingAssessmentState initialState = new()
        {
            AssessmentId        = assessmentId,
            PatientId           = PatientId,
            AssessmentDateTime  = assessmentDateTime,
            AssessmentType      = assessmentType,
            NurseId             = nurseId,
            NurseName           = nurseName,
            LocationId          = locationId,
            LocationName        = locationName,
            Status              = GrainStates.NursingAssessmentStatus.Draft,
            LevelOfConsciousness = levelOfConsciousness,
            Orientation         = orientation ?? new(),
            BreathSounds        = breathSounds,
            OxygenTherapy       = oxygenTherapy,
            SpO2                = spO2,
            HeartRhythm         = heartRhythm,
            Edema               = edema,
            SkinIntegrity       = skinIntegrity,
            BradenScore         = bradenScore,
            PainScore           = painScore,
            PainLocation        = painLocation,
            BowelSounds         = bowelSounds,
            AppetiteAssessment  = appetiteAssessment,
            UrineOutput         = urineOutput,
            HasFoley            = hasFoley,
            AnxietyLevel        = anxietyLevel,
            Mood                = mood,
            MorseScoreTotal     = morseScore,
            FallRiskLevel       = fallRiskLevel,
            FallPrecautions     = fallPrecautions ?? new(),
            AdlMobility         = adlMobility,
            NarrativeNotes      = narrativeNotes
        };

        await AssessmentGrain(assessmentId).CreateAsync(initialState);

        await AssessmentIndex().AddEntryAsync(new GrainStates.NursingAssessmentIndexEntry
        {
            AssessmentId       = assessmentId,
            AssessmentDateTime = assessmentDateTime,
            AssessmentType     = assessmentType,
            NurseId            = nurseId,
            NurseName          = nurseName,
            Status             = GrainStates.NursingAssessmentStatus.Draft,
            LocationName       = locationName,
            PainScore          = painScore,
            MorseScore         = morseScore,
            BradenScore        = bradenScore
        });

        return assessmentId;
    }

    public Task<GrainStates.NursingAssessmentState> GetNursingAssessmentAsync(string assessmentId)
        => AssessmentGrain(assessmentId).GetAsync();

    public async Task<List<GrainStates.NursingAssessmentIndexEntry>> GetNursingAssessmentsAsync()
    {
        GrainStates.NursingAssessmentIndexState index = await AssessmentIndex().GetAsync();
        return index.Assessments;
    }

    public async Task SignNursingAssessmentAsync(string assessmentId, string nurseId, string nurseName)
    {
        await AssessmentGrain(assessmentId).SignAsync(nurseId, nurseName, DateTime.UtcNow);
        await AssessmentIndex().UpdateEntryStatusAsync(
            assessmentId, GrainStates.NursingAssessmentStatus.Signed);
    }

    public Task<GrainStates.NursingCarePlanState> GetNursingCarePlanAsync()
        => CarePlanGrain().GetAsync();

    public Task<string> AddNursingDiagnosisAsync(
        string nursingDiagnosis, string? relatedTo, string? evidencedBy,
        int? priority, string? nurseId, string? nurseName)
        => CarePlanGrain().AddDiagnosisAsync(
            nursingDiagnosis, relatedTo, evidencedBy, priority, nurseId, nurseName);

    public Task AddCarePlanGoalAsync(string problemId, string goalText, DateTime? targetDate)
        => CarePlanGrain().AddGoalAsync(problemId, goalText, targetDate);

    public Task AddCarePlanInterventionAsync(
        string problemId, string interventionText, string? frequency,
        string? nurseId, string? nurseName)
        => CarePlanGrain().AddInterventionAsync(
            problemId, interventionText, frequency, nurseId, nurseName);

    public Task RecordCarePlanOutcomeAsync(
        string problemId, GrainStates.NursingOutcomeRating rating,
        string evaluatedById, string evaluatedByName, string? notes)
        => CarePlanGrain().RecordOutcomeEvaluationAsync(
            problemId, rating, evaluatedById, evaluatedByName, notes);

    public Task UpdateCarePlanGoalStatusAsync(
        string problemId, string goalId, GrainStates.NursingGoalStatus status)
        => CarePlanGrain().UpdateGoalStatusAsync(problemId, goalId, status);

    public Task ResolveNursingDiagnosisAsync(string problemId, string? resolutionNotes)
        => CarePlanGrain().ResolveDiagnosisAsync(problemId, resolutionNotes);

    public Task RecordNursingAcuityAsync(
        GrainStates.AcuityLevel level, int? score,
        string nurseId, string nurseName, string? shift, string? notes)
        => AcuityGrain().RecordAcuityAsync(level, score, nurseId, nurseName, shift, notes);

    public Task<GrainStates.NursingAcuityState> GetNursingAcuityAsync()
        => AcuityGrain().GetAsync();

    // ─── Nursing Triage/Intake ───────────────────────────────────────────────

    private INursingTriageGrain TriageGrain(string triageId)
        => GrainFactory.GetGrain<INursingTriageGrain>($"NURS-TRIAGE:{triageId}");

    private INursingTriageIndexGrain TriageIndex()
        => GrainFactory.GetGrain<INursingTriageIndexGrain>($"NURS-TRIAGE-IDX:{PatientId}");

    public async Task<string> CreateTriageAssessmentAsync(
        DateTime triageDateTime, string nurseId, string nurseName,
        string? locationId, string? locationName,
        string chiefComplaint, string? historyOfPresentIllness,
        decimal? temperature, int? heartRate, int? respiratoryRate,
        int? systolicBP, int? diastolicBP, decimal? spO2, int? painScore,
        GrainStates.TriageLevel triageLevel, int? expectedResources,
        string? levelOfConsciousness, string? modeOfArrival,
        bool isAcuteDistress, bool arrivedByAmbulance,
        string? notes)
    {
        string triageId = Guid.NewGuid().ToString("N");

        GrainStates.NursingTriageState initial = new()
        {
            PatientId               = PatientId,
            TriageDateTime          = triageDateTime,
            NurseId                 = nurseId,
            NurseName               = nurseName,
            LocationId              = locationId,
            LocationName            = locationName,
            ChiefComplaint          = chiefComplaint,
            HistoryOfPresentIllness = historyOfPresentIllness,
            Temperature             = temperature,
            HeartRate               = heartRate,
            RespiratoryRate         = respiratoryRate,
            SystolicBP              = systolicBP,
            DiastolicBP             = diastolicBP,
            SpO2                    = spO2,
            PainScore               = painScore,
            TriageLevel             = triageLevel,
            ExpectedResources       = expectedResources,
            LevelOfConsciousness    = levelOfConsciousness,
            ModeOfArrival           = modeOfArrival,
            IsAcuteDistress         = isAcuteDistress,
            ArrivedByAmbulance      = arrivedByAmbulance,
            Notes                   = notes,
        };

        await TriageGrain(triageId).CreateAsync(initial);

        await TriageIndex().AddOrUpdateAsync(new GrainStates.NursingTriageIndexEntry
        {
            TriageId       = triageId,
            TriageDateTime = triageDateTime,
            NurseName      = nurseName,
            ChiefComplaint = chiefComplaint,
            TriageLevel    = triageLevel,
            Disposition    = GrainStates.TriageDisposition.Pending,
            Status         = GrainStates.NursingAssessmentStatus.Draft,
            PainScore      = painScore,
        });

        return triageId;
    }

    public Task<GrainStates.NursingTriageState> GetTriageAssessmentAsync(string triageId)
        => TriageGrain(triageId).GetAsync();

    public Task<List<GrainStates.NursingTriageIndexEntry>> GetTriageAssessmentsAsync()
        => TriageIndex().GetAllAsync();

    public async Task SignTriageAssessmentAsync(string triageId, string nurseId, string nurseName)
    {
        await TriageGrain(triageId).SignAsync(nurseId, nurseName);
        GrainStates.NursingTriageState state = await TriageGrain(triageId).GetAsync();
        await TriageIndex().AddOrUpdateAsync(new GrainStates.NursingTriageIndexEntry
        {
            TriageId = triageId, TriageDateTime = state.TriageDateTime,
            NurseName = state.NurseName, ChiefComplaint = state.ChiefComplaint,
            TriageLevel = state.TriageLevel, Disposition = state.Disposition,
            Status = state.Status, PainScore = state.PainScore,
        });
    }

    public async Task SetTriageDispositionAsync(string triageId, GrainStates.TriageDisposition disposition)
    {
        await TriageGrain(triageId).SetDispositionAsync(disposition);
        GrainStates.NursingTriageState state = await TriageGrain(triageId).GetAsync();
        await TriageIndex().AddOrUpdateAsync(new GrainStates.NursingTriageIndexEntry
        {
            TriageId = triageId, TriageDateTime = state.TriageDateTime,
            NurseName = state.NurseName, ChiefComplaint = state.ChiefComplaint,
            TriageLevel = state.TriageLevel, Disposition = disposition,
            Status = state.Status, PainScore = state.PainScore,
        });
    }

    // ─── Nursing Task Worklist ───────────────────────────────────────────────

    private INursingTaskWorklistGrain TaskWorklist()
        => GrainFactory.GetGrain<INursingTaskWorklistGrain>($"NURS-TASKS:{PatientId}");

    public async Task<GrainStates.NursingTaskWorklistState> RefreshNursingTaskWorklistAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<GrainStates.NursingTask> medTasks = new();
        List<GrainStates.NursingTask> vitalTasks = new();
        List<GrainStates.NursingTask> interventionTasks = new();
        List<GrainStates.NursingTask> otherTasks = new();

        // Pull due medications from MAR
        try
        {
            List<GrainStates.MarEntry> dueMeds = await GetDueMedicationsAsync();
            foreach (GrainStates.MarEntry med in dueMeds)
            {
                medTasks.Add(new GrainStates.NursingTask
                {
                    TaskId      = $"MED-{med.OrderId}",
                    PatientId   = PatientId,
                    Category    = GrainStates.NursingTaskCategory.Medication,
                    Priority    = med.Priority == "STAT" ? GrainStates.NursingTaskPriority.STAT
                        : med.Priority == "ASAP" ? GrainStates.NursingTaskPriority.Urgent
                        : GrainStates.NursingTaskPriority.Routine,
                    Status      = GrainStates.NursingTaskStatus.Due,
                    Description = $"Administer: {med.DrugName} {med.Dosage} {med.Route} {med.Schedule}",
                    SourceId    = med.OrderId,
                    SourceType  = "MAR",
                    DueDateTime = now,
                });
            }
        }
        catch { /* MAR may not be initialized */ }

        // Pull care plan active interventions
        try
        {
            GrainStates.NursingCarePlanState carePlan = await GetNursingCarePlanAsync();
            foreach (GrainStates.NursingCarePlanProblem problem in carePlan.Problems
                .Where(p => p.Status == GrainStates.NursingCarePlanStatus.Active))
            {
                foreach (GrainStates.NursingIntervention intervention in problem.Interventions.Where(i => i.IsActive))
                {
                    interventionTasks.Add(new GrainStates.NursingTask
                    {
                        TaskId      = $"NI-{intervention.InterventionId}",
                        PatientId   = PatientId,
                        Category    = GrainStates.NursingTaskCategory.Intervention,
                        Priority    = GrainStates.NursingTaskPriority.Routine,
                        Status      = GrainStates.NursingTaskStatus.Due,
                        Description = $"{intervention.InterventionText} ({intervention.Frequency ?? "PRN"})",
                        SourceId    = intervention.InterventionId,
                        SourceType  = "CAREPLAN",
                        DueDateTime = now,
                    });
                }
            }
        }
        catch { /* Care plan may not be initialized */ }

        // Standard vital sign task (every shift)
        vitalTasks.Add(new GrainStates.NursingTask
        {
            TaskId      = $"VS-{now:yyyyMMddHH}",
            PatientId   = PatientId,
            Category    = GrainStates.NursingTaskCategory.VitalSigns,
            Priority    = GrainStates.NursingTaskPriority.Routine,
            Status      = GrainStates.NursingTaskStatus.Due,
            Description = "Record vital signs (T, HR, RR, BP, SpO2, Pain)",
            SourceType  = "VITAL_SCHEDULE",
            DueDateTime = now,
        });

        await TaskWorklist().RefreshAsync(medTasks, vitalTasks, interventionTasks, otherTasks);
        return await TaskWorklist().GetAsync();
    }

    public Task<GrainStates.NursingTaskWorklistState> GetNursingTaskWorklistAsync()
        => TaskWorklist().GetAsync();

    public Task<List<GrainStates.NursingTask>> GetDueNursingTasksAsync()
        => TaskWorklist().GetDueTasksAsync();

    public Task CompleteNursingTaskAsync(string taskId, string nurseId, string nurseName, string? notes)
        => TaskWorklist().CompleteTaskAsync(taskId, nurseId, nurseName, notes);

    public Task DeferNursingTaskAsync(string taskId, string? reason)
        => TaskWorklist().DeferTaskAsync(taskId, reason);

    public Task AddNursingTaskAsync(
        GrainStates.NursingTaskCategory category,
        GrainStates.NursingTaskPriority priority,
        string description,
        DateTime dueDateTime,
        string? sourceId, string? sourceType)
    {
        string taskId = $"ADHOC-{Guid.NewGuid():N}";
        return TaskWorklist().AddTaskAsync(new GrainStates.NursingTask
        {
            TaskId      = taskId,
            PatientId   = PatientId,
            Category    = category,
            Priority    = priority,
            Status      = GrainStates.NursingTaskStatus.Due,
            Description = description,
            SourceId    = sourceId,
            SourceType  = sourceType ?? "ADHOC",
            DueDateTime = dueDateTime,
        });
    }

    // ─── Shift Handoff / Report ────────────────────────────────────────────

    private INursingShiftHandoffGrain HandoffGrain(string id)
        => GrainFactory.GetGrain<INursingShiftHandoffGrain>($"NURS-HANDOFF:{id}");

    private IShiftHandoffIndexGrain HandoffIndex()
        => GrainFactory.GetGrain<IShiftHandoffIndexGrain>($"NURS-HANDOFF-IDX:{PatientId}");

    public async Task<string> CreateShiftHandoffAsync(
        GrainStates.NursingShift shift, DateTime shiftDate,
        string outgoingNurseId, string outgoingNurseName,
        string? locationId, string? locationName, string? bedNumber,
        GrainStates.SbarPatientSummary sbar,
        List<string>? safetyConcerns, string? notes)
    {
        // Auto-populate clinical snapshot from current patient data
        GrainStates.HandoffClinicalSnapshot snapshot = new();
        try
        {
            List<GrainStates.VitalSummary> vitals = await GetLatestVitalsAsync();
            string vitalStr = string.Join("; ", vitals.Select(v => $"{v.VitalType}: {v.Value} {v.Units}"));
            int? painScore = vitals.FirstOrDefault(v => v.VitalType == "PAIN")?.Value is string ps && int.TryParse(ps, out int p) ? p : null;

            GrainStates.NursingCarePlanState cp = await GetNursingCarePlanAsync();
            List<string> activeDx = cp.Problems
                .Where(p => p.Status == GrainStates.NursingCarePlanStatus.Active)
                .Select(p => p.NursingDiagnosis).ToList();

            List<GrainStates.NursingTask> dueTasks = await GetDueNursingTasksAsync();
            List<string> taskDescs = dueTasks.Select(t => t.Description).Take(10).ToList();

            GrainStates.NursingAcuityState acuity = await GetNursingAcuityAsync();

            snapshot = new GrainStates.HandoffClinicalSnapshot
            {
                VitalsSummary         = vitalStr,
                PainScore             = painScore,
                AcuityLevel           = acuity.CurrentAcuityLevel.ToString(),
                ActiveNursingDiagnoses = activeDx,
                PendingTasks          = taskDescs,
            };
        }
        catch { /* Some data sources may not be initialized */ }

        string handoffId = Guid.NewGuid().ToString("N");
        await HandoffGrain(handoffId).CreateAsync(
            PatientId, shift, shiftDate, outgoingNurseId, outgoingNurseName,
            locationId, locationName, bedNumber, sbar, snapshot, safetyConcerns, notes);

        await HandoffIndex().AddOrUpdateAsync(new GrainStates.ShiftHandoffIndexEntry
        {
            HandoffId         = handoffId,
            Shift             = shift,
            ShiftDate         = shiftDate,
            OutgoingNurseName = outgoingNurseName,
            Status            = GrainStates.ShiftHandoffStatus.Draft,
        });

        return handoffId;
    }

    public Task<GrainStates.NursingShiftHandoffState> GetShiftHandoffAsync(string handoffId)
        => HandoffGrain(handoffId).GetAsync();

    public Task<List<GrainStates.ShiftHandoffIndexEntry>> GetShiftHandoffsAsync()
        => HandoffIndex().GetAllAsync();

    public async Task CompleteShiftHandoffAsync(string handoffId)
    {
        await HandoffGrain(handoffId).CompleteAsync();
        GrainStates.NursingShiftHandoffState state = await HandoffGrain(handoffId).GetAsync();
        await HandoffIndex().AddOrUpdateAsync(new GrainStates.ShiftHandoffIndexEntry
        {
            HandoffId = handoffId, Shift = state.Shift, ShiftDate = state.ShiftDate,
            OutgoingNurseName = state.OutgoingNurseName, IncomingNurseName = state.IncomingNurseName,
            Status = state.Status,
        });
    }

    public async Task AcknowledgeShiftHandoffAsync(string handoffId, string incomingNurseId, string incomingNurseName)
    {
        await HandoffGrain(handoffId).AcknowledgeAsync(incomingNurseId, incomingNurseName);
        GrainStates.NursingShiftHandoffState state = await HandoffGrain(handoffId).GetAsync();
        await HandoffIndex().AddOrUpdateAsync(new GrainStates.ShiftHandoffIndexEntry
        {
            HandoffId = handoffId, Shift = state.Shift, ShiftDate = state.ShiftDate,
            OutgoingNurseName = state.OutgoingNurseName, IncomingNurseName = incomingNurseName,
            Status = state.Status,
        });
    }

    // ─── Pain Assessment Workflow ────────────────────────────────────────────

    private IPainAssessmentGrain PainGrain(string id)
        => GrainFactory.GetGrain<IPainAssessmentGrain>($"NURS-PAIN:{id}");

    private IPainAssessmentIndexGrain PainIndex()
        => GrainFactory.GetGrain<IPainAssessmentIndexGrain>($"NURS-PAIN-IDX:{PatientId}");

    public async Task<string> RecordPainAssessmentAsync(
        GrainStates.PainAssessmentTool tool, int painScore,
        string? painLocation, string? painCharacter, string? painOnset,
        string? aggravatingFactors, string? alleviatingFactors, string? radiation,
        int? acceptablePainLevel,
        GrainStates.DvprsSupplemental? dvprsSupplemental,
        GrainStates.FlaccScore? flaccComponents,
        string? interventionProvided,
        string nurseId, string nurseName, string? notes)
    {
        string assessId = Guid.NewGuid().ToString("N");

        GrainStates.PainAssessmentState initial = new()
        {
            PatientId            = PatientId,
            AssessmentDateTime   = DateTime.UtcNow,
            NurseId              = nurseId,
            NurseName            = nurseName,
            Tool                 = tool,
            PainScore            = painScore,
            PainLocation         = painLocation,
            PainCharacter        = painCharacter,
            PainOnset            = painOnset,
            AggravatingFactors   = aggravatingFactors,
            AlleviatingFactors   = alleviatingFactors,
            Radiation            = radiation,
            AcceptablePainLevel  = acceptablePainLevel,
            DvprsSupplemental    = dvprsSupplemental,
            FlaccComponents      = flaccComponents,
            InterventionProvided = interventionProvided,
            Notes                = notes,
        };

        await PainGrain(assessId).CreateAsync(initial);

        await PainIndex().AddOrUpdateAsync(new GrainStates.PainAssessmentIndexEntry
        {
            AssessmentId       = assessId,
            AssessmentDateTime = DateTime.UtcNow,
            NurseName          = nurseName,
            Tool               = tool,
            PainScore          = painScore,
            PainLocation       = painLocation,
            IsReassessment     = false,
            Status             = GrainStates.NursingAssessmentStatus.Draft,
        });

        return assessId;
    }

    public async Task<string> RecordPainReassessmentAsync(
        string initialAssessmentId,
        GrainStates.PainAssessmentTool tool, int postInterventionScore,
        int minutesSinceIntervention,
        string? interventionProvided,
        string nurseId, string nurseName, string? notes)
    {
        // Get initial score for effectiveness comparison
        GrainStates.PainAssessmentState initialState = await PainGrain(initialAssessmentId).GetAsync();
        bool effective = postInterventionScore < initialState.PainScore;

        string reassessId = Guid.NewGuid().ToString("N");

        GrainStates.PainAssessmentState initial = new()
        {
            PatientId                = PatientId,
            AssessmentDateTime       = DateTime.UtcNow,
            NurseId                  = nurseId,
            NurseName                = nurseName,
            Tool                     = tool,
            PainScore                = postInterventionScore,
            PainLocation             = initialState.PainLocation,
            InterventionProvided     = interventionProvided ?? initialState.InterventionProvided,
            IsReassessment           = true,
            InitialAssessmentId      = initialAssessmentId,
            PostInterventionScore    = postInterventionScore,
            MinutesSinceIntervention = minutesSinceIntervention,
            InterventionEffective    = effective,
            Notes                    = notes,
        };

        await PainGrain(reassessId).CreateAsync(initial);

        // Also update the initial assessment with reassessment data
        await PainGrain(initialAssessmentId).RecordReassessmentAsync(
            postInterventionScore, minutesSinceIntervention, effective);

        await PainIndex().AddOrUpdateAsync(new GrainStates.PainAssessmentIndexEntry
        {
            AssessmentId         = reassessId,
            AssessmentDateTime   = DateTime.UtcNow,
            NurseName            = nurseName,
            Tool                 = tool,
            PainScore            = postInterventionScore,
            PainLocation         = initialState.PainLocation,
            IsReassessment       = true,
            PostInterventionScore = postInterventionScore,
            Status               = GrainStates.NursingAssessmentStatus.Draft,
        });

        return reassessId;
    }

    public Task<GrainStates.PainAssessmentState> GetPainAssessmentAsync(string assessmentId)
        => PainGrain(assessmentId).GetAsync();

    public Task<List<GrainStates.PainAssessmentIndexEntry>> GetPainAssessmentsAsync()
        => PainIndex().GetAllAsync();

    public Task<GrainStates.PainAssessmentIndexEntry?> GetLatestPainAssessmentAsync()
        => PainIndex().GetLatestAsync();

    // ─── Dental (Files #228, #228.1) — DENPAT.m, DENTX.m, DENPROC.m ─────────

    private IDentalPatientGrain DentalPatient()
        => GrainFactory.GetGrain<IDentalPatientGrain>($"DENTAL-PATIENT:{PatientId}");

    private IDentalTreatmentIndexGrain DentalTreatmentIndex()
        => GrainFactory.GetGrain<IDentalTreatmentIndexGrain>($"DENTAL-TX-IDX:{PatientId}");

    private IDentalTreatmentGrain DentalTreatment(string treatmentId)
        => GrainFactory.GetGrain<IDentalTreatmentGrain>(treatmentId);

    public async Task<GrainStates.DentalPatientState> GetDentalPatientAsync()
    {
        await DentalPatient().EnsureInitializedAsync(PatientId);
        return await DentalPatient().GetAsync();
    }

    public async Task UpdateDentalEligibilityAsync(
        GrainStates.DentalEligibilityStatus eligibilityStatus,
        string? eligibilityBasisCode,
        string? eligibilityBasisDescription)
    {
        await DentalPatient().EnsureInitializedAsync(PatientId);
        await DentalPatient().UpdateEligibilityAsync(
            eligibilityStatus, eligibilityBasisCode, eligibilityBasisDescription);
    }

    public async Task SetPrimaryDentistAsync(string dentistId, string dentistName)
    {
        await DentalPatient().EnsureInitializedAsync(PatientId);
        await DentalPatient().SetPrimaryDentistAsync(dentistId, dentistName);
    }

    public async Task UpdateDentalClinicalStatusAsync(
        GrainStates.DentalPeriodontalStatus periodontalStatus,
        string? prostheticStatus,
        int? remainingTeethCount,
        bool onFluoride,
        string? clinicalNotes)
    {
        await DentalPatient().EnsureInitializedAsync(PatientId);
        await DentalPatient().UpdateClinicalStatusAsync(
            periodontalStatus, prostheticStatus, remainingTeethCount, onFluoride, clinicalNotes);
    }

    public async Task RecordDentalVisitDatesAsync(
        DateTime? lastExamDate,
        DateTime? lastXRayDate,
        DateTime? lastCleaningDate)
    {
        await DentalPatient().EnsureInitializedAsync(PatientId);
        await DentalPatient().RecordVisitDatesAsync(lastExamDate, lastXRayDate, lastCleaningDate);
    }

    public Task<List<GrainStates.DentalTreatmentIndexEntry>> GetDentalTreatmentsAsync()
        => DentalTreatmentIndex().GetAllAsync();

    public Task<List<GrainStates.DentalTreatmentIndexEntry>> GetDentalTreatmentsByStatusAsync(
        GrainStates.DentalTreatmentStatus status)
        => DentalTreatmentIndex().GetByStatusAsync(status);

    public async Task<GrainStates.DentalTreatmentState> GetDentalTreatmentAsync(string treatmentId)
        => await DentalTreatment(treatmentId).GetAsync();

    public async Task<string> RecordDentalTreatmentAsync(
        DateTime treatmentDate,
        string procedureCode,
        string procedureDescription,
        GrainStates.DentalProcedureCategory procedureCategory,
        List<int> toothNumbers,
        List<string> surfaces,
        string providerId,
        string providerName,
        string? locationId,
        string? locationName,
        string? diagnosisCode,
        string? anesthesiaType,
        decimal? chargeAmount,
        string? notes)
    {
        string treatmentId = $"DENTAL-TX:{Guid.NewGuid()}";
        IDentalTreatmentGrain tx = DentalTreatment(treatmentId);

        await tx.CreateAsync(
            PatientId, treatmentDate, procedureCode, procedureDescription, procedureCategory,
            toothNumbers, surfaces, providerId, providerName,
            locationId, locationName, diagnosisCode, anesthesiaType, chargeAmount, notes);

        await DentalTreatmentIndex().AddEntryAsync(new GrainStates.DentalTreatmentIndexEntry
        {
            TreatmentId          = treatmentId,
            PatientId            = PatientId,
            ProcedureCode        = procedureCode,
            ProcedureDescription = procedureDescription,
            ProcedureCategory    = procedureCategory,
            ToothNumbers         = toothNumbers.Count > 0
                ? string.Join(",", toothNumbers)
                : null,
            TreatmentDate = treatmentDate,
            ProviderName  = providerName,
            Status        = GrainStates.DentalTreatmentStatus.Planned,
            ChargeAmount  = chargeAmount,
        });

        return treatmentId;
    }

    public async Task CompleteDentalTreatmentAsync(
        string treatmentId,
        DateTime completedDate,
        string completedByUserId,
        string? notes)
    {
        await DentalTreatment(treatmentId).CompleteAsync(completedDate, completedByUserId, notes);
        await DentalTreatmentIndex().UpdateEntryStatusAsync(
            treatmentId, GrainStates.DentalTreatmentStatus.Completed);
    }

    public async Task CancelDentalTreatmentAsync(
        string treatmentId,
        string reason,
        string cancelledByUserId)
    {
        await DentalTreatment(treatmentId).CancelAsync(reason, cancelledByUserId);
        await DentalTreatmentIndex().UpdateEntryStatusAsync(
            treatmentId, GrainStates.DentalTreatmentStatus.Cancelled);
    }

    public async Task ReferDentalTreatmentAsync(
        string treatmentId,
        string referralReason,
        string referredByUserId)
    {
        await DentalTreatment(treatmentId).ReferAsync(referralReason, referredByUserId);
        await DentalTreatmentIndex().UpdateEntryStatusAsync(
            treatmentId, GrainStates.DentalTreatmentStatus.Referred);
    }

    // ─── Social Work (File #707) — SWRPATCH.m, SWR*.m ────────────────────────

    private ISocialWorkAssessmentGrain SocialWorkAssessment(string assessmentId)
        => GrainFactory.GetGrain<ISocialWorkAssessmentGrain>(assessmentId);

    private ISocialWorkAssessmentIndexGrain SocialWorkAssessmentIndex()
        => GrainFactory.GetGrain<ISocialWorkAssessmentIndexGrain>($"SW-ASSESSMENT-IDX:{PatientId}");

    private ISocialWorkReferralGrain SocialWorkReferral(string referralId)
        => GrainFactory.GetGrain<ISocialWorkReferralGrain>(referralId);

    private ISocialWorkReferralIndexGrain SocialWorkReferralIndex()
        => GrainFactory.GetGrain<ISocialWorkReferralIndexGrain>($"SW-REFERRAL-IDX:{PatientId}");

    public async Task<string> CreateSocialWorkAssessmentAsync(
        GrainStates.SocialWorkAssessmentType assessmentType,
        DateTime assessmentDate,
        string? socialWorkerId,
        string? socialWorkerName,
        GrainStates.SocialWorkRiskLevel riskLevel,
        string? housingStatus,
        string? employmentStatus,
        string? socialSupport,
        string? financialStressors,
        string? substanceUseHistory,
        bool? abuseConcernsIdentified,
        bool? safetyPlanInPlace,
        DateTime? anticipatedDischargeDate,
        string? dischargeDisposition,
        string? dischargePlan,
        List<string>? dischargeBarriers,
        string? recommendations,
        string? notes,
        string? locationId,
        string? locationName)
    {
        string assessmentId = $"SW-ASSESSMENT:{Guid.NewGuid()}";
        ISocialWorkAssessmentGrain grain = SocialWorkAssessment(assessmentId);

        await grain.CreateAsync(
            PatientId, assessmentType, assessmentDate,
            socialWorkerId, socialWorkerName, riskLevel,
            housingStatus, employmentStatus, socialSupport,
            financialStressors, substanceUseHistory,
            abuseConcernsIdentified, safetyPlanInPlace,
            anticipatedDischargeDate, dischargeDisposition, dischargePlan,
            dischargeBarriers, recommendations, notes,
            locationId, locationName);

        await SocialWorkAssessmentIndex().AddEntryAsync(new GrainStates.SocialWorkAssessmentIndexEntry
        {
            AssessmentId     = assessmentId,
            PatientId        = PatientId,
            AssessmentType   = assessmentType,
            AssessmentDate   = assessmentDate,
            SocialWorkerName = socialWorkerName,
            RiskLevel        = riskLevel,
            Status           = GrainStates.SocialWorkAssessmentStatus.Draft,
            HousingStatus    = housingStatus,
        });

        return assessmentId;
    }

    public async Task CompleteSocialWorkAssessmentAsync(
        string assessmentId,
        DateTime completedDate,
        string? recommendations,
        string? notes)
    {
        await SocialWorkAssessment(assessmentId).CompleteAsync(completedDate, recommendations, notes);
        await SocialWorkAssessmentIndex().UpdateEntryStatusAsync(
            assessmentId, GrainStates.SocialWorkAssessmentStatus.Complete);
    }

    public async Task CloseSocialWorkAssessmentAsync(string assessmentId, string reason)
    {
        await SocialWorkAssessment(assessmentId).CloseAsync(reason);
        await SocialWorkAssessmentIndex().UpdateEntryStatusAsync(
            assessmentId, GrainStates.SocialWorkAssessmentStatus.Closed);
    }

    public async Task<GrainStates.SocialWorkAssessmentState> GetSocialWorkAssessmentAsync(string assessmentId)
        => await SocialWorkAssessment(assessmentId).GetAsync();

    public async Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetSocialWorkAssessmentsAsync()
        => await SocialWorkAssessmentIndex().GetAllAsync();

    public async Task<List<GrainStates.SocialWorkAssessmentIndexEntry>> GetSocialWorkAssessmentsByTypeAsync(
        GrainStates.SocialWorkAssessmentType assessmentType)
        => await SocialWorkAssessmentIndex().GetByTypeAsync(assessmentType);

    public async Task<string> CreateSocialWorkReferralAsync(
        DateTime referralDate,
        string? referralSource,
        string? referralReason,
        GrainStates.SocialWorkReferralServiceType serviceType,
        string? agencyName,
        string? agencyContact,
        string? agencyPhone,
        string? socialWorkerId,
        string? socialWorkerName,
        DateTime? followUpDate,
        string? assessmentId,
        string? locationId,
        string? locationName,
        string? comments)
    {
        string referralId = $"SW-REFERRAL:{Guid.NewGuid()}";
        ISocialWorkReferralGrain grain = SocialWorkReferral(referralId);

        await grain.CreateAsync(
            PatientId, referralDate, referralSource, referralReason,
            serviceType, agencyName, agencyContact, agencyPhone,
            socialWorkerId, socialWorkerName,
            followUpDate, assessmentId,
            locationId, locationName, comments);

        await SocialWorkReferralIndex().AddEntryAsync(new GrainStates.SocialWorkReferralIndexEntry
        {
            ReferralId       = referralId,
            PatientId        = PatientId,
            ReferralDate     = referralDate,
            ServiceType      = serviceType,
            AgencyName       = agencyName,
            Status           = GrainStates.SocialWorkReferralStatus.Pending,
            SocialWorkerName = socialWorkerName,
            FollowUpDate     = followUpDate,
        });

        return referralId;
    }

    public async Task UpdateSocialWorkReferralStatusAsync(
        string referralId,
        GrainStates.SocialWorkReferralStatus status,
        string? outcomeNotes,
        DateTime? followUpDate)
    {
        await SocialWorkReferral(referralId).UpdateStatusAsync(status, outcomeNotes, followUpDate);
        await SocialWorkReferralIndex().UpdateEntryStatusAsync(referralId, status, followUpDate);
    }

    public async Task CloseSocialWorkReferralAsync(string referralId, string? outcomeNotes)
    {
        await SocialWorkReferral(referralId).CloseAsync(outcomeNotes);
        await SocialWorkReferralIndex().UpdateEntryStatusAsync(
            referralId, GrainStates.SocialWorkReferralStatus.Closed);
    }

    public async Task<GrainStates.SocialWorkReferralState> GetSocialWorkReferralAsync(string referralId)
        => await SocialWorkReferral(referralId).GetAsync();

    public async Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetSocialWorkReferralsAsync()
        => await SocialWorkReferralIndex().GetAllAsync();

    public async Task<List<GrainStates.SocialWorkReferralIndexEntry>> GetSocialWorkReferralsByStatusAsync(
        GrainStates.SocialWorkReferralStatus status)
        => await SocialWorkReferralIndex().GetByStatusAsync(status);

    // ─── Women's Health (VistA File #790) ────────────────────────────────────

    private IWomensHealthNotificationGrain WomensHealthNotification(string notificationId)
        => GrainFactory.GetGrain<IWomensHealthNotificationGrain>(notificationId);

    private IWomensHealthIndexGrain WomensHealthIndex()
        => GrainFactory.GetGrain<IWomensHealthIndexGrain>($"WH-IDX:{PatientId}");

    public async Task<string> CreateWomensHealthNotificationAsync(
        GrainStates.WomensHealthNotificationType notificationType,
        DateTime procedureDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        GrainStates.MammographyResult? mammographyResult,
        int? biRadsScore,
        GrainStates.PapSmearResult? papSmearResult,
        string? contraceptiveMethod,
        int? gestationalAgeWeeks,
        DateTime? estimatedDueDate,
        string? pregnancyOutcome,
        bool followUpRequired,
        DateTime? nextDueDate,
        bool isRefusal,
        string? notes)
    {
        string notificationId = $"WH-NOTE:{Guid.NewGuid()}";
        IWomensHealthNotificationGrain grain = WomensHealthNotification(notificationId);

        await grain.CreateAsync(
            PatientId, notificationType, procedureDate,
            providerId, providerName, locationId, locationName,
            mammographyResult, biRadsScore, papSmearResult,
            contraceptiveMethod, gestationalAgeWeeks, estimatedDueDate, pregnancyOutcome,
            followUpRequired, nextDueDate, isRefusal, notes);

        await WomensHealthIndex().AddEntryAsync(new GrainStates.WomensHealthIndexEntry
        {
            NotificationId   = notificationId,
            PatientId        = PatientId,
            NotificationType = notificationType,
            ProcedureDate    = procedureDate,
            Status           = followUpRequired
                               ? GrainStates.WomensHealthNotificationStatus.FollowUpRequired
                               : GrainStates.WomensHealthNotificationStatus.Active,
            ProviderName     = providerName,
            FollowUpRequired = followUpRequired,
            NextDueDate      = nextDueDate,
        });

        return notificationId;
    }

    public async Task CompleteWomensHealthNotificationAsync(
        string notificationId,
        DateTime? followUpCompletedDate,
        string? notes)
    {
        await WomensHealthNotification(notificationId).CompleteAsync(followUpCompletedDate, notes);
        await WomensHealthIndex().UpdateEntryStatusAsync(
            notificationId,
            GrainStates.WomensHealthNotificationStatus.Completed,
            followUpRequired: false,
            nextDueDate: null);
    }

    public async Task SetWomensHealthFollowUpAsync(
        string notificationId,
        bool required,
        DateTime? nextDueDate)
    {
        await WomensHealthNotification(notificationId).SetFollowUpRequiredAsync(required, nextDueDate);
        await WomensHealthIndex().UpdateEntryStatusAsync(
            notificationId,
            required
                ? GrainStates.WomensHealthNotificationStatus.FollowUpRequired
                : GrainStates.WomensHealthNotificationStatus.Active,
            followUpRequired: required,
            nextDueDate: nextDueDate);
    }

    public async Task CancelWomensHealthNotificationAsync(string notificationId)
    {
        await WomensHealthNotification(notificationId).CancelAsync();
        await WomensHealthIndex().UpdateEntryStatusAsync(
            notificationId,
            GrainStates.WomensHealthNotificationStatus.Cancelled,
            followUpRequired: null,
            nextDueDate: null);
    }

    public async Task<GrainStates.WomensHealthNotificationState> GetWomensHealthNotificationAsync(
        string notificationId)
        => await WomensHealthNotification(notificationId).GetAsync();

    public async Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthNotificationsAsync()
        => await WomensHealthIndex().GetAllAsync();

    public async Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthNotificationsByTypeAsync(
        GrainStates.WomensHealthNotificationType notificationType)
        => await WomensHealthIndex().GetByTypeAsync(notificationType);

    public async Task<List<GrainStates.WomensHealthIndexEntry>> GetWomensHealthFollowUpRequiredAsync()
        => await WomensHealthIndex().GetFollowUpRequiredAsync();

    // ─── Prenatal / OB (IHS Prenatal Care Module — BJPNAPI.m, BWGRVL.m) ─────────

    private IPregnancyGrain Pregnancy(string pregnancyId)
        => GrainFactory.GetGrain<IPregnancyGrain>(pregnancyId);

    private IPregnancyIndexGrain PregnancyIndex()
        => GrainFactory.GetGrain<IPregnancyIndexGrain>($"OB-PREG-IDX:{PatientId}");

    private IPrenatalVisitGrain PrenatalVisit(string visitId)
        => GrainFactory.GetGrain<IPrenatalVisitGrain>(visitId);

    private IPrenatalVisitIndexGrain PrenatalVisitIndex(string pregnancyId)
        => GrainFactory.GetGrain<IPrenatalVisitIndexGrain>($"OB-VISIT-IDX:{pregnancyId}");

    public async Task<string> CreatePregnancyAsync(
        DateTime? lastMenstrualPeriod,
        DateTime? eddByLmp,
        DateTime? eddByUltrasound,
        DateTime definitiveEdd,
        int gravida, int para, int abortions, int living,
        GrainStates.PregnancyRiskLevel riskLevel,
        List<string>? riskFactors,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        string? notes)
    {
        string pregnancyId = $"OB-PREG:{Guid.NewGuid()}";
        IPregnancyGrain grain = Pregnancy(pregnancyId);

        await grain.CreateAsync(
            PatientId, lastMenstrualPeriod, eddByLmp, eddByUltrasound, definitiveEdd,
            gravida, para, abortions, living, riskLevel, riskFactors,
            providerId, providerName, locationId, locationName, notes);

        await PregnancyIndex().AddEntryAsync(new GrainStates.PregnancyIndexEntry
        {
            PregnancyId   = pregnancyId,
            PatientId     = PatientId,
            Status        = GrainStates.PregnancyStatus.Active,
            DefinitiveEdd = definitiveEdd,
            Gravida       = gravida,
            Para          = para,
            RiskLevel     = riskLevel,
            Outcome       = GrainStates.PregnancyOutcome.Ongoing,
            ProviderName  = providerName,
            CreatedDate   = DateTime.UtcNow,
        });

        return pregnancyId;
    }

    public async Task<GrainStates.PregnancyState> GetPregnancyAsync(string pregnancyId)
        => await Pregnancy(pregnancyId).GetAsync();

    public async Task<List<GrainStates.PregnancyIndexEntry>> GetPregnanciesAsync()
        => await PregnancyIndex().GetAllAsync();

    public async Task<GrainStates.PregnancyIndexEntry?> GetActivePregnancyAsync()
        => await PregnancyIndex().GetActiveAsync();

    public async Task UpdatePregnancyRiskAsync(string pregnancyId,
        GrainStates.PregnancyRiskLevel riskLevel, List<string> riskFactors)
    {
        await Pregnancy(pregnancyId).UpdateRiskAsync(riskLevel, riskFactors);
        GrainStates.PregnancyState state = await Pregnancy(pregnancyId).GetAsync();
        await PregnancyIndex().UpdateEntryAsync(pregnancyId, state.Status, state.Outcome, riskLevel);
    }

    public async Task AddPrenatalProblemAsync(string pregnancyId, GrainStates.PrenatalProblemEntry problem)
        => await Pregnancy(pregnancyId).AddProblemAsync(problem);

    public async Task ResolvePrenatalProblemAsync(string pregnancyId, string problemId)
        => await Pregnancy(pregnancyId).ResolveProblemAsync(problemId);

    public async Task RecordDeliveryAsync(string pregnancyId,
        GrainStates.DeliveryInfo delivery, GrainStates.PregnancyOutcome outcome)
    {
        await Pregnancy(pregnancyId).RecordDeliveryAsync(delivery, outcome);
        await PregnancyIndex().UpdateEntryAsync(
            pregnancyId, GrainStates.PregnancyStatus.Delivered, outcome,
            (await Pregnancy(pregnancyId).GetAsync()).RiskLevel);
    }

    public async Task RecordPostpartumAsync(string pregnancyId, GrainStates.PostpartumInfo postpartum)
    {
        await Pregnancy(pregnancyId).RecordPostpartumAsync(postpartum);
        GrainStates.PregnancyState state = await Pregnancy(pregnancyId).GetAsync();
        await PregnancyIndex().UpdateEntryAsync(
            pregnancyId, GrainStates.PregnancyStatus.Postpartum, state.Outcome, state.RiskLevel);
    }

    public async Task UpdatePregnancyStatusAsync(string pregnancyId, GrainStates.PregnancyStatus status)
    {
        await Pregnancy(pregnancyId).UpdateStatusAsync(status);
        GrainStates.PregnancyState state = await Pregnancy(pregnancyId).GetAsync();
        await PregnancyIndex().UpdateEntryAsync(pregnancyId, status, state.Outcome, state.RiskLevel);
    }

    public async Task UpdatePregnancyEddAsync(string pregnancyId, DateTime? eddByUltrasound, DateTime definitiveEdd)
        => await Pregnancy(pregnancyId).UpdateEddAsync(eddByUltrasound, definitiveEdd);

    public async Task<string> CreatePrenatalVisitAsync(
        string pregnancyId,
        DateTime visitDate,
        int gestationalAgeWeeks, int gestationalAgeDays,
        decimal? weight,
        int? bloodPressureSystolic, int? bloodPressureDiastolic,
        decimal? fundalHeightCm, int? fetalHeartRate,
        GrainStates.FetalPresentation fetalPresentation,
        bool? fetalMovement,
        string? urineProtein, string? urineGlucose, string? edema,
        decimal? cervicalDilationCm, int? cervicalEffacementPercent, int? fetalStation,
        string? providerId, string? providerName,
        string? notes, DateTime? nextVisitDate)
    {
        string visitId = $"OB-VISIT:{Guid.NewGuid()}";
        IPrenatalVisitGrain grain = PrenatalVisit(visitId);

        await grain.CreateAsync(
            pregnancyId, PatientId, visitDate,
            gestationalAgeWeeks, gestationalAgeDays,
            weight, bloodPressureSystolic, bloodPressureDiastolic,
            fundalHeightCm, fetalHeartRate, fetalPresentation, fetalMovement,
            urineProtein, urineGlucose, edema,
            cervicalDilationCm, cervicalEffacementPercent, fetalStation,
            providerId, providerName, notes, nextVisitDate);

        await PrenatalVisitIndex(pregnancyId).AddEntryAsync(new GrainStates.PrenatalVisitIndexEntry
        {
            VisitId             = visitId,
            PregnancyId         = pregnancyId,
            VisitDate           = visitDate,
            GestationalAgeWeeks = gestationalAgeWeeks,
            GestationalAgeDays  = gestationalAgeDays,
            FetalHeartRate      = fetalHeartRate,
            FundalHeightCm      = fundalHeightCm,
            Weight              = weight,
            ProviderName        = providerName,
        });

        return visitId;
    }

    public async Task<GrainStates.PrenatalVisitState> GetPrenatalVisitAsync(string visitId)
        => await PrenatalVisit(visitId).GetAsync();

    public async Task<List<GrainStates.PrenatalVisitIndexEntry>> GetPrenatalVisitsAsync(string pregnancyId)
        => await PrenatalVisitIndex(pregnancyId).GetAllAsync();

    public async Task<int> GetPrenatalVisitCountAsync(string pregnancyId)
        => await PrenatalVisitIndex(pregnancyId).GetVisitCountAsync();

    // ─── Substance Abuse Treatment — see PatientWorkflowGrain.SAT.cs (feature-gated) ─

    // ─── Spinal Cord Injury / Dysfunction Registry (VistA File #154) ─────────────

    private ISCIPatientGrain SCIPatient()
        => GrainFactory.GetGrain<ISCIPatientGrain>($"SCI-PATIENT:{PatientId}");

    private ISCIIndexGrain SCIIndex()
        => GrainFactory.GetGrain<ISCIIndexGrain>("SCI-INDEX");

    public async Task EnrollInSCIRegistryAsync(
        DateTime enrollmentDate,
        string? sciCenter,
        DateTime? dateOfInjuryOnset,
        GrainStates.SCIInjuryType injuryType,
        GrainStates.SCIEtiology etiology,
        string neurologicalLevelOfInjury,
        GrainStates.SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        string? enrollingProviderId,
        string? enrollingProviderName,
        string? primaryProviderId,
        string? primaryProviderName,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILocomotionMethod? locomotionMethod,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? notes)
    {
        await SCIPatient().EnrollAsync(
            PatientId, enrollmentDate, sciCenter, dateOfInjuryOnset,
            injuryType, etiology, neurologicalLevelOfInjury, aisGrade,
            primaryDiagnosisCode, primaryDiagnosisDescription,
            enrollingProviderId, enrollingProviderName,
            primaryProviderId, primaryProviderName,
            bladderManagement, bowelProgram, locomotionMethod, livingSituation,
            associatedConditions, notes);

        await SCIIndex().AddEntryAsync(new GrainStates.SCIIndexEntry
        {
            PatientId            = PatientId,
            EnrollmentDate       = enrollmentDate,
            Status               = GrainStates.SCIRegistryStatus.Active,
            NeurologicalLevel    = neurologicalLevelOfInjury,
            AisGrade             = aisGrade,
            SCICenter            = sciCenter,
            EnrollingProviderName = enrollingProviderName,
            DateOfInjuryOnset    = dateOfInjuryOnset,
            InjuryType           = injuryType
        });
    }

    public async Task UpdateSCIPatientAsync(
        string neurologicalLevelOfInjury,
        GrainStates.SCIAisGrade aisGrade,
        string? primaryDiagnosisCode,
        string? primaryDiagnosisDescription,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILocomotionMethod? locomotionMethod,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? associatedConditions,
        string? primaryProviderId,
        string? primaryProviderName,
        string? notes)
    {
        await SCIPatient().UpdateClinicalDataAsync(
            neurologicalLevelOfInjury, aisGrade,
            primaryDiagnosisCode, primaryDiagnosisDescription,
            bladderManagement, bowelProgram, locomotionMethod, livingSituation,
            associatedConditions, primaryProviderId, primaryProviderName, notes);

        await SCIIndex().UpdateEntryAsync(
            PatientId, GrainStates.SCIRegistryStatus.Active,
            neurologicalLevelOfInjury, aisGrade);
    }

    public async Task UpdateSCIStatusAsync(GrainStates.SCIRegistryStatus status, string? notes)
    {
        await SCIPatient().UpdateStatusAsync(status, notes);
        GrainStates.SCIPatientState current = await SCIPatient().GetAsync();
        await SCIIndex().UpdateEntryAsync(
            PatientId, status,
            current.NeurologicalLevelOfInjury, current.AisGrade);
    }

    public async Task<string> AddSCIAnnualEncounterAsync(
        int fiscalYear,
        DateTime encounterDate,
        GrainStates.SCIEncounterType encounterType,
        GrainStates.SCIAisGrade aisGrade,
        string neurologicalLevel,
        int hospitalAdmissions,
        int urinaryTractInfections,
        int pressureInjuryCount,
        int highestPressureInjuryStage,
        GrainStates.SCIBladderManagement? bladderManagement,
        GrainStates.SCIBowelProgram? bowelProgram,
        GrainStates.SCILivingSituation? livingSituation,
        List<string>? equipmentNeeds,
        string? providerId,
        string? providerName,
        string? notes)
    {
        string encounterId = await SCIPatient().AddAnnualEncounterAsync(
            fiscalYear, encounterDate, encounterType, aisGrade, neurologicalLevel,
            hospitalAdmissions, urinaryTractInfections,
            pressureInjuryCount, highestPressureInjuryStage,
            bladderManagement, bowelProgram, livingSituation,
            equipmentNeeds, providerId, providerName, notes);

        // Keep index in sync with latest NLI / AIS
        GrainStates.SCIPatientState current = await SCIPatient().GetAsync();
        await SCIIndex().UpdateEntryAsync(
            PatientId, current.Status, neurologicalLevel, aisGrade);

        return encounterId;
    }

    public async Task<GrainStates.SCIPatientState> GetSCIPatientAsync()
        => await SCIPatient().GetAsync();

    public async Task<List<GrainStates.SCIAnnualEncounterRecord>> GetSCIAnnualEncountersAsync()
        => await SCIPatient().GetAnnualEncountersAsync();

    // ═══════════════════════════════════════════════════════════════════════════
    // Blind Rehabilitation (VistA File #782) — ANRV.m, ANRUTIL.m, ANRVAD.m
    // ═══════════════════════════════════════════════════════════════════════════

    private IBRPatientGrain BRPatient()
        => GrainFactory.GetGrain<IBRPatientGrain>($"BR-PATIENT:{PatientId}");

    private IBRAdmissionIndexGrain BRAdmissionIndex()
        => GrainFactory.GetGrain<IBRAdmissionIndexGrain>($"BR-ADMIT-IDX:{PatientId}");

    private IBROutpatientVisitIndexGrain BRVisitIndex()
        => GrainFactory.GetGrain<IBROutpatientVisitIndexGrain>($"BR-VISIT-IDX:{PatientId}");

    public async Task<GrainStates.BRPatientState> GetBRPatientAsync()
    {
        await BRPatient().InitializeAsync(PatientId);
        return await BRPatient().GetAsync();
    }

    public async Task RecordVisualAcuityAsync(
        string rightEyeDistance,
        string leftEyeDistance,
        string bestCorrectedRight,
        string bestCorrectedLeft,
        GrainStates.VisualField visualFieldRight,
        GrainStates.VisualField visualFieldLeft,
        string? contrastSensitivity,
        DateTime examDate,
        string examinerId,
        string examinerName,
        string? notes)
    {
        await BRPatient().InitializeAsync(PatientId);
        await BRPatient().RecordVisualAcuityAsync(
            rightEyeDistance, leftEyeDistance,
            bestCorrectedRight, bestCorrectedLeft,
            visualFieldRight, visualFieldLeft,
            contrastSensitivity, examDate,
            examinerId, examinerName, notes);
    }

    public async Task UpdateBRDiagnosisAsync(
        string primaryDiagnosis,
        string? secondaryDiagnosis,
        GrainStates.BROnsetType onsetType,
        DateTime? onsetDate,
        bool serviceConnected,
        int? serviceConnectedPercentage,
        string? icd10Code,
        string? notes)
    {
        await BRPatient().InitializeAsync(PatientId);
        await BRPatient().UpdateDiagnosisAsync(
            primaryDiagnosis, secondaryDiagnosis,
            onsetType, onsetDate,
            serviceConnected, serviceConnectedPercentage,
            icd10Code, notes);
    }

    public async Task AddBRDeviceAsync(GrainStates.BRDeviceEntry device)
    {
        await BRPatient().InitializeAsync(PatientId);
        await BRPatient().AddDeviceAsync(device);
    }

    public async Task AddBRTrainingGoalAsync(string goal, GrainStates.BRTrainingArea area)
    {
        await BRPatient().InitializeAsync(PatientId);
        await BRPatient().AddTrainingGoalAsync(goal, area);
    }

    public async Task UpdateBREligibilityAsync(GrainStates.BREligibilityStatus eligibility, string? reason)
    {
        await BRPatient().InitializeAsync(PatientId);
        await BRPatient().UpdateEligibilityAsync(eligibility, reason);
    }

    public async Task<string> CreateBRAdmissionAsync(
        string centerId,
        string centerName,
        DateTime admitDate,
        DateTime? plannedDischargeDate,
        List<GrainStates.BRTrainingArea> programAreas,
        GrainStates.BRAdmissionPriority priority,
        string referringProviderId,
        string referringProviderName,
        string? goals,
        string? notes)
    {
        string admitId = $"BR-ADMIT-{Guid.NewGuid()}";
        IBRAdmissionGrain admissionGrain = GrainFactory.GetGrain<IBRAdmissionGrain>(admitId);
        await admissionGrain.CreateAsync(
            admitId, PatientId, centerId, centerName,
            admitDate, plannedDischargeDate, programAreas, priority,
            referringProviderId, referringProviderName, goals, notes);
        await BRAdmissionIndex().AddAsync(new GrainStates.BRAdmissionIndexEntry
        {
            AdmitId = admitId,
            PatientId = PatientId,
            CenterName = centerName,
            AdmitDate = admitDate,
            Status = GrainStates.BRAdmissionStatus.Pending
        });
        return admitId;
    }

    public async Task<List<GrainStates.BRAdmissionIndexEntry>> GetBRAdmissionsAsync()
        => await BRAdmissionIndex().GetAllAsync();

    public async Task<string> ScheduleBROutpatientVisitAsync(
        DateTime visitDate,
        GrainStates.BRTrainingArea trainingArea,
        string therapistId,
        string therapistName,
        string location,
        int durationMinutes,
        string? sessionNotes,
        List<string> skillsAddressed)
    {
        string visitId = $"BR-VISIT-{Guid.NewGuid()}";
        IBROutpatientVisitGrain visitGrain = GrainFactory.GetGrain<IBROutpatientVisitGrain>(visitId);
        await visitGrain.CreateAsync(
            visitId, PatientId, visitDate, trainingArea,
            therapistId, therapistName, location, durationMinutes,
            sessionNotes, skillsAddressed);
        await BRVisitIndex().AddAsync(new GrainStates.BROutpatientVisitIndexEntry
        {
            VisitId = visitId,
            PatientId = PatientId,
            VisitDate = visitDate,
            TrainingArea = trainingArea,
            TherapistName = therapistName,
            Status = GrainStates.BRVisitStatus.Scheduled
        });
        return visitId;
    }

    public async Task<List<GrainStates.BROutpatientVisitIndexEntry>> GetBROutpatientVisitsAsync()
        => await BRVisitIndex().GetAllAsync();

    // ═══════════════════════════════════════════════════════════════════════════
    // Home Telehealth / Remote Patient Monitoring (VistA Files #720–720.9)
    // ═══════════════════════════════════════════════════════════════════════════

    private IHomeTelehealthPatientGrain HtPatient()
        => GrainFactory.GetGrain<IHomeTelehealthPatientGrain>($"HT-PATIENT:{PatientId}");

    private IHomeTelehealthReadingIndexGrain HtReadingIndex()
        => GrainFactory.GetGrain<IHomeTelehealthReadingIndexGrain>($"HT-READING-IDX:{PatientId}");

    private IHomeTelehealthAlertIndexGrain HtAlertIndex()
        => GrainFactory.GetGrain<IHomeTelehealthAlertIndexGrain>($"HT-ALERT-IDX:{PatientId}");

    private IHomeTelehealthDeviceGrain HtDevice(string deviceId)
        => GrainFactory.GetGrain<IHomeTelehealthDeviceGrain>($"HT-DEVICE:{deviceId}");

    private IHomeTelehealthDeviceIndexGrain HtDeviceIndex()
        => GrainFactory.GetGrain<IHomeTelehealthDeviceIndexGrain>("HT-DEVICE-IDX");

    private IHomeTelehealthReadingGrain HtReading(string readingId)
        => GrainFactory.GetGrain<IHomeTelehealthReadingGrain>($"HT-READING:{readingId}");

    private IHomeTelehealthAlertGrain HtAlert(string alertId)
        => GrainFactory.GetGrain<IHomeTelehealthAlertGrain>($"HT-ALERT:{alertId}");

    public async Task<GrainStates.HomeTelehealthPatientState> GetHtPatientAsync()
        => await HtPatient().GetAsync();

    public async Task EnrollInHomeTelehealthAsync(
        string? careCoordinatorId,
        string? careCoordinatorName,
        string? primaryCareProviderId,
        string? primaryCareProviderName,
        GrainStates.HtCareProtocol protocol,
        string? notes)
    {
        await HtPatient().EnrollAsync(PatientId, careCoordinatorId, careCoordinatorName,
            primaryCareProviderId, primaryCareProviderName, protocol, notes);
    }

    public async Task DisenrollFromHomeTelehealthAsync(string? reason)
        => await HtPatient().DisenrollAsync(reason);

    public async Task AssignHtDeviceAsync(string deviceId, string deviceName, GrainStates.HtDeviceType deviceType)
    {
        await HtPatient().AssignDeviceAsync(deviceId, deviceName, deviceType);
        await HtDevice(deviceId).AssignToPatientAsync(PatientId);
        await HtDeviceIndex().UpdateStatusAsync(deviceId, GrainStates.HtDeviceStatus.Assigned, PatientId);
    }

    public async Task ReturnHtDeviceAsync(string deviceId)
    {
        await HtPatient().ReturnDeviceAsync(deviceId);
        await HtDevice(deviceId).ReturnToInventoryAsync();
        await HtDeviceIndex().UpdateStatusAsync(deviceId, GrainStates.HtDeviceStatus.Available, null);
    }

    public async Task SetHtAlertThresholdsAsync(List<GrainStates.HtAlertThreshold> thresholds)
        => await HtPatient().SetAlertThresholdsAsync(thresholds);

    public async Task<string> RecordHtReadingAsync(
        GrainStates.HtMeasurementType measurementType,
        decimal? value1,
        decimal? value2,
        string unit,
        DateTime readingDateTime,
        GrainStates.HtReadingSource source,
        string? deviceId,
        string? notes)
    {
        string readingId = $"HT-READING-{Guid.NewGuid()}";
        IHomeTelehealthReadingGrain readingGrain = HtReading(readingId);

        await readingGrain.RecordAsync(readingId, PatientId, measurementType,
            value1, value2, unit, readingDateTime, source, deviceId, notes);

        // Add to patient index
        await HtReadingIndex().AddAsync(new GrainStates.HtReadingIndexEntry
        {
            ReadingId = readingId,
            PatientId = PatientId,
            MeasurementType = measurementType,
            Value1 = value1,
            Value2 = value2,
            Unit = unit,
            ReadingDateTime = readingDateTime,
            AlertGenerated = false,
            IsReviewed = false,
            Source = source
        });

        // Check thresholds and generate alert if needed
        GrainStates.HomeTelehealthPatientState patientState = await HtPatient().GetAsync();
        GrainStates.HtAlertThreshold? threshold = patientState.AlertThresholds
            .FirstOrDefault(t => t.MeasurementType == measurementType);

        if (threshold != null)
        {
            bool outOfRange = false;
            string description = string.Empty;
            GrainStates.HtAlertSeverity severity = GrainStates.HtAlertSeverity.Moderate;

            bool v1Low  = threshold.LowValue.HasValue  && value1.HasValue && value1 < threshold.LowValue;
            bool v1High = threshold.HighValue.HasValue && value1.HasValue && value1 > threshold.HighValue;
            bool v2Low  = threshold.LowValue2.HasValue  && value2.HasValue && value2 < threshold.LowValue2;
            bool v2High = threshold.HighValue2.HasValue && value2.HasValue && value2 > threshold.HighValue2;

            if (v1Low || v1High || v2Low || v2High)
            {
                outOfRange = true;
                string dir1 = v1Low ? "low" : (v1High ? "high" : string.Empty);
                string dir2 = v2Low ? "low" : (v2High ? "high" : string.Empty);

                description = measurementType == GrainStates.HtMeasurementType.BloodPressure
                    ? $"Blood pressure {value1}/{value2} {unit} is out of range (threshold: {threshold.LowValue}/{threshold.LowValue2}–{threshold.HighValue}/{threshold.HighValue2})"
                    : $"{measurementType} reading {value1} {unit} is {dir1 + dir2} (threshold: {threshold.LowValue}–{threshold.HighValue})";

                // Escalate severity for extreme deviations
                if (value1.HasValue && threshold.HighValue.HasValue && value1 > threshold.HighValue * 1.2m)
                    severity = GrainStates.HtAlertSeverity.Critical;
                else if (value1.HasValue && threshold.LowValue.HasValue && value1 < threshold.LowValue * 0.8m)
                    severity = GrainStates.HtAlertSeverity.Critical;
            }

            if (outOfRange)
            {
                string alertId = $"HT-ALERT-{Guid.NewGuid()}";
                await HtAlert(alertId).CreateAsync(alertId, PatientId, readingId,
                    measurementType, value1, value2, severity, description);

                await HtAlertIndex().AddAsync(new GrainStates.HtAlertIndexEntry
                {
                    AlertId = alertId,
                    PatientId = PatientId,
                    ReadingId = readingId,
                    MeasurementType = measurementType,
                    Severity = severity,
                    Status = GrainStates.HtAlertStatus.Active,
                    AlertDateTime = DateTime.UtcNow,
                    AlertDescription = description
                });

                await readingGrain.SetAlertGeneratedAsync(alertId);
            }
        }

        return readingId;
    }

    public async Task<List<GrainStates.HtReadingIndexEntry>> GetHtReadingsAsync(
        GrainStates.HtMeasurementType? measurementType,
        int? days,
        int maxResults)
        => await HtReadingIndex().GetAsync(measurementType, days, maxResults);

    public async Task ReviewHtReadingAsync(string readingId, string reviewedById, string reviewedByName)
    {
        await HtReading(readingId).MarkReviewedAsync(reviewedById, reviewedByName);
        await HtReadingIndex().MarkReviewedAsync(readingId);
    }

    public async Task<List<GrainStates.HtAlertIndexEntry>> GetHtAlertsAsync(GrainStates.HtAlertStatus? status)
        => await HtAlertIndex().GetAsync(status);

    public async Task AcknowledgeHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse)
    {
        await HtAlert(alertId).AcknowledgeAsync(clinicianId, clinicianName, clinicalResponse);
        await HtAlertIndex().UpdateStatusAsync(alertId, GrainStates.HtAlertStatus.Acknowledged);
    }

    public async Task ResolveHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse)
    {
        await HtAlert(alertId).ResolveAsync(clinicianId, clinicianName, clinicalResponse);
        await HtAlertIndex().UpdateStatusAsync(alertId, GrainStates.HtAlertStatus.Resolved);
    }

    public async Task DismissHtAlertAsync(string alertId, string clinicianId, string clinicianName, string? clinicalResponse)
    {
        await HtAlert(alertId).DismissAsync(clinicianId, clinicianName, clinicalResponse);
        await HtAlertIndex().UpdateStatusAsync(alertId, GrainStates.HtAlertStatus.Dismissed);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Event Capture (VistA Files #721, #724) — ECPEC.m, ECPEEN.m, ECPEWL.m
    // ═══════════════════════════════════════════════════════════════════════════

    private IEventCaptureEncounterGrain EcEncounter(string encounterId)
        => GrainFactory.GetGrain<IEventCaptureEncounterGrain>(encounterId);

    private IEventCapturePatientGrain EcPatient()
        => GrainFactory.GetGrain<IEventCapturePatientGrain>($"EC-PATIENT:{PatientId}");

    private IEventCaptureEncounterIndexGrain EcEncounterIndex()
        => GrainFactory.GetGrain<IEventCaptureEncounterIndexGrain>("EC-ENCOUNTER-IDX");

    public async Task<string> CreateEventCaptureEncounterAsync(
        DateTime encounterDateTime,
        string dssUnitId,
        string dssUnitName,
        string? dssUnitCode,
        string? clinicId,
        string? clinicName,
        string? locationId,
        string? locationName,
        string primaryProviderId,
        string primaryProviderName,
        string? attendingProviderId,
        string? attendingProviderName,
        GrainStates.EcEncounterType encounterType,
        GrainStates.EcPatientCategory patientCategory,
        string? primaryStopCode,
        string? creditStopCode,
        string? comments)
    {
        string encounterId = $"EC-ENCOUNTER:{Guid.NewGuid()}";

        await EcEncounter(encounterId).CreateAsync(
            PatientId,
            encounterDateTime,
            dssUnitId,
            dssUnitName,
            dssUnitCode,
            clinicId,
            clinicName,
            locationId,
            locationName,
            primaryProviderId,
            primaryProviderName,
            attendingProviderId,
            attendingProviderName,
            encounterType,
            patientCategory,
            primaryStopCode,
            creditStopCode,
            comments);

        // Update per-patient index
        await EcPatient().AddEncounterAsync(encounterId, encounterDateTime);

        // Update application-wide index
        GrainStates.EventCaptureIndexEntry indexEntry = new()
        {
            EncounterId = encounterId,
            PatientId = PatientId,
            EncounterDateTime = encounterDateTime,
            DssUnitId = dssUnitId,
            DssUnitName = dssUnitName,
            DssUnitCode = dssUnitCode,
            ClinicName = clinicName,
            PrimaryProviderId = primaryProviderId,
            PrimaryProviderName = primaryProviderName,
            EncounterType = encounterType,
            Status = GrainStates.EcEncounterStatus.Open,
            ProcedureCount = 0,
        };
        await EcEncounterIndex().AddOrUpdateAsync(indexEntry);

        return encounterId;
    }

    public async Task<GrainStates.EventCaptureEncounterState> GetEventCaptureEncounterAsync(string encounterId)
        => await EcEncounter(encounterId).GetEncounterAsync();

    public async Task<List<GrainStates.EventCaptureIndexEntry>> GetEventCaptureEncountersAsync(int maxResults)
        => await EcEncounterIndex().GetByPatientAsync(PatientId, maxResults);

    public async Task AddEcProcedureAsync(
        string encounterId,
        string cptCode,
        string procedureDescription,
        int quantity,
        string providerId,
        string providerName,
        string? modifierCode)
    {
        await EcEncounter(encounterId).AddProcedureAsync(
            cptCode, procedureDescription, quantity, providerId, providerName, modifierCode);

        // Refresh the index entry procedure count
        GrainStates.EventCaptureEncounterState state = await EcEncounter(encounterId).GetEncounterAsync();
        GrainStates.EventCaptureIndexEntry updatedEntry = BuildIndexEntry(state);
        await EcEncounterIndex().AddOrUpdateAsync(updatedEntry);
    }

    public async Task AddEcDiagnosisAsync(
        string encounterId,
        string icd10Code,
        string description,
        bool isPrimary)
    {
        await EcEncounter(encounterId).AddDiagnosisAsync(icd10Code, description, isPrimary);
    }

    public async Task CompleteEventCaptureEncounterAsync(
        string encounterId,
        DateTime checkOutDateTime,
        int? visitLengthMinutes)
    {
        await EcEncounter(encounterId).CompleteAsync(checkOutDateTime, visitLengthMinutes);

        GrainStates.EventCaptureEncounterState state = await EcEncounter(encounterId).GetEncounterAsync();
        await EcEncounterIndex().AddOrUpdateAsync(BuildIndexEntry(state));
    }

    public async Task DeleteEventCaptureEncounterAsync(
        string encounterId,
        string deletedByProviderId,
        string deletedByProviderName,
        string? reason)
    {
        await EcEncounter(encounterId).DeleteAsync(deletedByProviderId, deletedByProviderName, reason);

        GrainStates.EventCaptureEncounterState state = await EcEncounter(encounterId).GetEncounterAsync();
        await EcEncounterIndex().AddOrUpdateAsync(BuildIndexEntry(state));
    }

    private static GrainStates.EventCaptureIndexEntry BuildIndexEntry(GrainStates.EventCaptureEncounterState state)
        => new()
        {
            EncounterId = state.EncounterId,
            PatientId = state.PatientId,
            EncounterDateTime = state.EncounterDateTime,
            DssUnitId = state.DssUnitId,
            DssUnitName = state.DssUnitName,
            DssUnitCode = state.DssUnitCode,
            ClinicName = state.ClinicName,
            PrimaryProviderId = state.PrimaryProviderId,
            PrimaryProviderName = state.PrimaryProviderName,
            EncounterType = state.EncounterType,
            Status = state.Status,
            ProcedureCount = state.Procedures.Count,
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // Health Summary (VistA File #142) — GMTS.m, GMTSS.m, GMTSUP.m
    // ═══════════════════════════════════════════════════════════════════════════

    private IHealthSummaryTypeGrain HsType(string typeId)
        => GrainFactory.GetGrain<IHealthSummaryTypeGrain>($"HS-TYPE:{typeId}");

    private IHealthSummaryTypeIndexGrain HsTypeIndex()
        => GrainFactory.GetGrain<IHealthSummaryTypeIndexGrain>("HS-TYPE-IDX");

    private IHealthSummaryGrain HsReport(string reportId)
        => GrainFactory.GetGrain<IHealthSummaryGrain>(reportId);

    private IHealthSummaryIndexGrain HsIndex()
        => GrainFactory.GetGrain<IHealthSummaryIndexGrain>($"HS-IDX:{PatientId}");

    public async Task<string> GenerateHealthSummaryAsync(
        string typeId,
        string requestedById,
        string requestedByName)
    {
        GrainStates.HealthSummaryTypeState template = await HsType(typeId).GetAsync();

        List<GrainStates.HealthSummarySectionResult> sections = new();

        foreach (GrainStates.HealthSummaryComponentConfig component in
            template.Components.Where(c => c.IsEnabled).OrderBy(c => c.DisplayOrder))
        {
            GrainStates.HealthSummarySectionResult section =
                await BuildSectionAsync(component);
            sections.Add(section);
        }

        string reportId = $"HS-REPORT:{Guid.NewGuid()}";
        GrainStates.HealthSummaryState report = new()
        {
            ReportId = reportId,
            PatientId = PatientId,
            TypeId = typeId,
            TypeName = template.Name,
            GeneratedDate = DateTime.UtcNow,
            GeneratedById = requestedById,
            GeneratedByName = requestedByName,
            Sections = sections
        };

        await HsReport(reportId).SaveAsync(report);
        await HsIndex().AddEntryAsync(new GrainStates.HealthSummaryIndexEntry
        {
            ReportId = reportId,
            PatientId = PatientId,
            TypeId = typeId,
            TypeName = template.Name,
            GeneratedDate = report.GeneratedDate,
            GeneratedById = requestedById,
            GeneratedByName = requestedByName,
            SectionCount = sections.Count
        });

        return reportId;
    }

    private async Task<GrainStates.HealthSummarySectionResult> BuildSectionAsync(
        GrainStates.HealthSummaryComponentConfig component)
    {
        string header = component.SectionHeader
            ?? GetDefaultSectionHeader(component.ComponentType);

        List<string> lines = new();

        try
        {
            switch (component.ComponentType)
            {
                case GrainStates.HealthSummaryComponentType.ActiveProblems:
                {
                    List<GrainStates.ProblemSummary> problems = await GetActiveProblemsAsync();
                    IEnumerable<GrainStates.ProblemSummary> set = component.MaxOccurrences > 0
                        ? problems.Take(component.MaxOccurrences)
                        : problems;
                    foreach (GrainStates.ProblemSummary p in set)
                        lines.Add($"{p.Diagnosis} ({p.DiagnosisCode ?? "no code"}) — {p.Status}");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.Allergies:
                {
                    List<GrainStates.AllergySummary> allergies = await GetAllergiesAsync();
                    IEnumerable<GrainStates.AllergySummary> set = component.MaxOccurrences > 0
                        ? allergies.Take(component.MaxOccurrences)
                        : allergies;
                    foreach (GrainStates.AllergySummary a in set)
                        lines.Add($"{a.Allergen} — {a.Severity ?? "unknown severity"}: {string.Join(", ", a.Reactions)}");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.CurrentMedications:
                {
                    List<GrainStates.MedicationSummary> meds = await GetActiveMedicationsAsync();
                    IEnumerable<GrainStates.MedicationSummary> set = component.MaxOccurrences > 0
                        ? meds.Take(component.MaxOccurrences)
                        : meds;
                    foreach (GrainStates.MedicationSummary m in set)
                        lines.Add($"{m.DrugName} — {m.Sig ?? "no sig"} (Rx#{m.PrescriptionId}, {m.Status})");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.VitalSigns:
                {
                    List<GrainStates.VitalSummary> vitals = await GetLatestVitalsAsync();
                    IEnumerable<GrainStates.VitalSummary> set = component.MaxOccurrences > 0
                        ? vitals.Take(component.MaxOccurrences)
                        : vitals;
                    foreach (GrainStates.VitalSummary v in set)
                        lines.Add($"{v.VitalType}: {v.Value} {v.Units} — {v.DateTimeTaken:yyyy-MM-dd HH:mm}");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.Appointments:
                {
                    List<GrainStates.AppointmentEntry> appts = await GetAllAppointmentsAsync(
                        component.MaxOccurrences > 0 ? component.MaxOccurrences : 20);
                    foreach (GrainStates.AppointmentEntry a in appts)
                        lines.Add($"{a.AppointmentDateTime:yyyy-MM-dd HH:mm} — {a.ClinicName} ({a.Status})");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.ClinicalNotes:
                {
                    List<GrainStates.TiuNoteSummary> notes = await GetNotesAsync(
                        null, component.MaxOccurrences > 0 ? component.MaxOccurrences : 10);
                    foreach (GrainStates.TiuNoteSummary n in notes)
                        lines.Add($"{n.ReferenceDate:yyyy-MM-dd} — {n.Subject ?? n.DocumentType} [{n.AuthorName}] ({n.Status})");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.Consults:
                {
                    List<GrainStates.ConsultSummary> consults = await GetConsultsAsync(
                        null, component.MaxOccurrences > 0 ? component.MaxOccurrences : 10);
                    foreach (GrainStates.ConsultSummary c in consults)
                        lines.Add($"{c.RequestDateTime:yyyy-MM-dd} — {c.ToService} ({c.Status})");
                    break;
                }

                case GrainStates.HealthSummaryComponentType.Demographics:
                {
                    GrainStates.PatientDemographicsSummary demo = await GetPatientInfoAsync();
                    lines.Add($"Name: {demo.Name}");
                    if (demo.DateOfBirth.HasValue)
                        lines.Add($"DOB: {demo.DateOfBirth.Value:yyyy-MM-dd}");
                    lines.Add($"Sex: {demo.Sex}");
                    if (!string.IsNullOrEmpty(demo.Ssn))
                        lines.Add($"SSN: {demo.Ssn}");
                    break;
                }

                default:
                    lines.Add($"[{component.ComponentType} data not available in this summary]");
                    break;
            }
        }
        catch
        {
            lines.Add($"[Error retrieving {component.ComponentType} data]");
        }

        return new GrainStates.HealthSummarySectionResult
        {
            ComponentType = component.ComponentType,
            SectionHeader = header,
            ContentLines = lines,
            EntryCount = lines.Count
        };
    }

    private static string GetDefaultSectionHeader(GrainStates.HealthSummaryComponentType type)
        => type switch
        {
            GrainStates.HealthSummaryComponentType.Demographics            => "PATIENT DEMOGRAPHICS",
            GrainStates.HealthSummaryComponentType.ActiveProblems          => "ACTIVE PROBLEMS",
            GrainStates.HealthSummaryComponentType.Allergies               => "ALLERGIES/ADVERSE REACTIONS",
            GrainStates.HealthSummaryComponentType.CurrentMedications      => "CURRENT OUTPATIENT MEDICATIONS",
            GrainStates.HealthSummaryComponentType.InpatientMedications    => "INPATIENT MEDICATIONS",
            GrainStates.HealthSummaryComponentType.VitalSigns              => "VITAL SIGNS",
            GrainStates.HealthSummaryComponentType.LabResults              => "RECENT LAB RESULTS",
            GrainStates.HealthSummaryComponentType.Radiology               => "RADIOLOGY REPORTS",
            GrainStates.HealthSummaryComponentType.Consults                => "CONSULTS/PROCEDURES",
            GrainStates.HealthSummaryComponentType.ClinicalNotes           => "CLINICAL NOTES",
            GrainStates.HealthSummaryComponentType.Appointments            => "APPOINTMENTS",
            GrainStates.HealthSummaryComponentType.Immunizations           => "IMMUNIZATIONS",
            GrainStates.HealthSummaryComponentType.ClinicalReminders       => "CLINICAL REMINDERS",
            GrainStates.HealthSummaryComponentType.HealthFactors           => "HEALTH FACTORS",
            GrainStates.HealthSummaryComponentType.SurgicalProcedures      => "SURGICAL PROCEDURES",
            GrainStates.HealthSummaryComponentType.ServiceConnectedConditions => "SERVICE-CONNECTED CONDITIONS",
            GrainStates.HealthSummaryComponentType.MentalHealth            => "MENTAL HEALTH",
            GrainStates.HealthSummaryComponentType.Dietetics               => "DIETETICS",
            _                                                               => type.ToString().ToUpperInvariant()
        };

    public async Task<GrainStates.HealthSummaryState> GetHealthSummaryAsync(string reportId)
        => await HsReport(reportId).GetAsync();

    public async Task<List<GrainStates.HealthSummaryIndexEntry>> GetHealthSummaryListAsync()
        => await HsIndex().GetAllAsync();

    public async Task<List<GrainStates.HealthSummaryIndexEntry>> GetHealthSummaryByTypeAsync(string typeId)
        => await HsIndex().GetByTypeAsync(typeId);

    // ═══════════════════════════════════════════════════════════════════════════
    // Oncology / Tumor Registry (VistA Files #160–#165)
    // MUMPS routines: ONCRP.m, ONCS.m, ONCTREAT.m
    // ═══════════════════════════════════════════════════════════════════════════

    private IOncologyTumorGrain OncTumor(string tumorId)
        => GrainFactory.GetGrain<IOncologyTumorGrain>(tumorId);

    private IOncologyTumorIndexGrain OncTumorIndex()
        => GrainFactory.GetGrain<IOncologyTumorIndexGrain>($"ONC-TUMOR-IDX:{PatientId}");

    private IOncologyTreatmentGrain OncTreatment(string treatmentId)
        => GrainFactory.GetGrain<IOncologyTreatmentGrain>(treatmentId);

    private IOncologyTreatmentIndexGrain OncTreatmentIndex()
        => GrainFactory.GetGrain<IOncologyTreatmentIndexGrain>($"ONC-TX-IDX:{PatientId}");

    private static GrainStates.OncologyTumorIndexEntry BuildOncTumorIndexEntry(GrainStates.OncologyTumorState s) =>
        new()
        {
            TumorId          = s.TumorId,
            PrimarySite      = s.PrimarySite,
            PrimarySiteText  = s.PrimarySiteText,
            Histology        = s.Histology,
            HistologyText    = s.HistologyText.Length > 80 ? s.HistologyText[..80] + "…" : s.HistologyText,
            DateOfDiagnosis  = s.DateOfDiagnosis,
            Status           = s.Status,
            StageGroup       = s.StageGroup,
            SequenceNumber   = s.SequenceNumber,
            OncologistName   = s.OncologistName
        };

    private static GrainStates.OncologyTreatmentIndexEntry BuildOncTreatmentIndexEntry(GrainStates.OncologyTreatmentState s) =>
        new()
        {
            TreatmentId        = s.TreatmentId,
            TumorId            = s.TumorId,
            TreatmentType      = s.TreatmentType,
            AgentName          = s.AgentName,
            StartDate          = s.StartDate,
            EndDate            = s.EndDate,
            Status             = s.Status,
            ResponseAssessment = s.ResponseAssessment,
            ProviderName       = s.ProviderName
        };

    public async Task<string> RegisterOncologyTumorAsync(
        string primarySite,
        string primarySiteText,
        string histology,
        string histologyText,
        GrainStates.TumorLaterality laterality,
        DateTime dateOfDiagnosis,
        GrainStates.DiagnosisBasis diagnosisBasis,
        int sequenceNumber,
        string? oncologistId,
        string? oncologistName)
    {
        string tumorId = $"ONC-TUMOR:{Guid.NewGuid()}";
        IOncologyTumorGrain tumor = OncTumor(tumorId);
        await tumor.RegisterTumorAsync(
            PatientId, primarySite, primarySiteText, histology, histologyText,
            laterality, dateOfDiagnosis, diagnosisBasis, sequenceNumber,
            oncologistId, oncologistName);
        GrainStates.OncologyTumorState state = await tumor.GetTumorAsync();
        await OncTumorIndex().UpsertTumorAsync(BuildOncTumorIndexEntry(state));
        return tumorId;
    }

    public async Task RecordOncologyStagingAsync(
        string tumorId,
        string? clinicalT,
        string? clinicalN,
        string? clinicalM,
        string? pathologicT,
        string? pathologicN,
        string? pathologicM,
        string? stageGroup,
        string? seerSummaryStage)
    {
        await OncTumor(tumorId).RecordStagingAsync(
            clinicalT, clinicalN, clinicalM,
            pathologicT, pathologicN, pathologicM,
            stageGroup, seerSummaryStage);
        GrainStates.OncologyTumorState state = await OncTumor(tumorId).GetTumorAsync();
        await OncTumorIndex().UpsertTumorAsync(BuildOncTumorIndexEntry(state));
    }

    public async Task UpdateOncologyStatusAsync(
        string tumorId,
        GrainStates.OncologyStatus status,
        DateTime? statusChangeDate,
        string? notes)
    {
        await OncTumor(tumorId).UpdateStatusAsync(status, statusChangeDate, notes);
        GrainStates.OncologyTumorState state = await OncTumor(tumorId).GetTumorAsync();
        await OncTumorIndex().UpsertTumorAsync(BuildOncTumorIndexEntry(state));
    }

    public async Task RecordOncologyRecurrenceAsync(
        string tumorId,
        DateTime recurrenceDate,
        string? recurrenceSite,
        string? notes)
    {
        await OncTumor(tumorId).RecordRecurrenceAsync(recurrenceDate, recurrenceSite, notes);
        GrainStates.OncologyTumorState state = await OncTumor(tumorId).GetTumorAsync();
        await OncTumorIndex().UpsertTumorAsync(BuildOncTumorIndexEntry(state));
    }

    public Task<GrainStates.OncologyTumorState> GetOncologyTumorAsync(string tumorId)
        => OncTumor(tumorId).GetTumorAsync();

    public Task<List<GrainStates.OncologyTumorIndexEntry>> GetOncologyTumorsAsync()
        => OncTumorIndex().GetAllTumorsAsync();

    public Task<List<GrainStates.OncologyTumorIndexEntry>> GetActiveOncologyTumorsAsync()
        => OncTumorIndex().GetActiveTumorsAsync();

    public async Task<string> CreateOncologyTreatmentAsync(
        string tumorId,
        GrainStates.OncologyTreatmentType treatmentType,
        string agentName,
        string? doseDescription,
        string? providerId,
        string? providerName,
        string? facilityName,
        string? notes)
    {
        string treatmentId = $"ONC-TX:{Guid.NewGuid()}";
        IOncologyTreatmentGrain tx = OncTreatment(treatmentId);
        await tx.CreateTreatmentAsync(
            tumorId, PatientId, treatmentType, agentName,
            doseDescription, providerId, providerName, facilityName, notes);
        GrainStates.OncologyTreatmentState state = await tx.GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
        await OncTumor(tumorId).AddTreatmentIdAsync(treatmentId);
        return treatmentId;
    }

    public async Task StartOncologyTreatmentAsync(string treatmentId, DateTime startDate)
    {
        await OncTreatment(treatmentId).StartTreatmentAsync(startDate);
        GrainStates.OncologyTreatmentState state = await OncTreatment(treatmentId).GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
    }

    public async Task CompleteOncologyTreatmentAsync(
        string treatmentId,
        DateTime endDate,
        GrainStates.TreatmentResponseAssessment responseAssessment,
        string? notes)
    {
        await OncTreatment(treatmentId).CompleteTreatmentAsync(endDate, responseAssessment, notes);
        GrainStates.OncologyTreatmentState state = await OncTreatment(treatmentId).GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
    }

    public async Task DiscontinueOncologyTreatmentAsync(
        string treatmentId,
        DateTime endDate,
        string discontinuationReason,
        string? notes)
    {
        await OncTreatment(treatmentId).DiscontinueTreatmentAsync(endDate, discontinuationReason, notes);
        GrainStates.OncologyTreatmentState state = await OncTreatment(treatmentId).GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
    }

    public async Task RecordOncologyResponseAsync(
        string treatmentId,
        GrainStates.TreatmentResponseAssessment responseAssessment,
        DateTime assessmentDate,
        string? notes)
    {
        await OncTreatment(treatmentId).RecordResponseAsync(responseAssessment, assessmentDate, notes);
        GrainStates.OncologyTreatmentState state = await OncTreatment(treatmentId).GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
    }

    public async Task UpdateOncologyCyclesAsync(string treatmentId, int cyclesCompleted)
    {
        await OncTreatment(treatmentId).UpdateCyclesAsync(cyclesCompleted);
        GrainStates.OncologyTreatmentState state = await OncTreatment(treatmentId).GetTreatmentAsync();
        await OncTreatmentIndex().UpsertTreatmentAsync(BuildOncTreatmentIndexEntry(state));
    }

    public Task<GrainStates.OncologyTreatmentState> GetOncologyTreatmentAsync(string treatmentId)
        => OncTreatment(treatmentId).GetTreatmentAsync();

    public Task<List<GrainStates.OncologyTreatmentIndexEntry>> GetOncologyTreatmentsAsync()
        => OncTreatmentIndex().GetAllTreatmentsAsync();

    public Task<List<GrainStates.OncologyTreatmentIndexEntry>> GetOncologyTreatmentsByTumorAsync(string tumorId)
        => OncTreatmentIndex().GetTreatmentsByTumorAsync(tumorId);

    // ═══════════════════════════════════════════════════════════════════════════
    // DS4P — Data Segmentation for Privacy (§170.315(b)(7)/(b)(8))
    // HL7 Data Segmentation for Privacy Implementation Guide
    // ═══════════════════════════════════════════════════════════════════════════

    public Task<string> GenerateDs4pCcdaAsync(string documentType, List<string> sensitivityCategories)
    {
        IDs4pCcdaGeneratorGrain gen = GrainFactory.GetGrain<IDs4pCcdaGeneratorGrain>($"DS4P-GEN:{PatientId}");
        return gen.GenerateDs4pCcdAsync(documentType, sensitivityCategories);
    }

    public Task<GrainStates.Ds4pAnalysisResult> AnalyzeDs4pCcdaAsync(string messageId, string ccdaXml)
    {
        IDs4pProcessorGrain proc = GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");
        return proc.AnalyzeCcdaAsync(ccdaXml);
    }

    public Task<GrainStates.Ds4pAnalysisResult> GetDs4pAnalysisAsync(string messageId)
    {
        IDs4pProcessorGrain proc = GrainFactory.GetGrain<IDs4pProcessorGrain>($"DS4P-PROC:{messageId}");
        return proc.GetAnalysisAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Cancer Registry Reporting (§170.315(f)(4))
    // NAACCR abstract generation for state/central cancer registries
    // ═══════════════════════════════════════════════════════════════════════════

    private ICancerRegistryReportGrain CrReport(string reportId)
        => GrainFactory.GetGrain<ICancerRegistryReportGrain>(reportId);

    private ICancerRegistryReportIndexGrain CrReportIndex()
        => GrainFactory.GetGrain<ICancerRegistryReportIndexGrain>("CR-REPORT-INDEX");

    private static GrainStates.CancerRegistryReportIndexEntry BuildCrIndexEntry(GrainStates.CancerRegistryReportState s) =>
        new()
        {
            ReportId = s.ReportId,
            PatientId = s.PatientId,
            PatientName = s.PatientName,
            TumorId = s.TumorId,
            PrimarySiteText = s.PrimarySiteText,
            DateOfDiagnosis = s.DateOfDiagnosis,
            Status = s.Status,
            ReportingFacility = s.ReportingFacility,
            CreatedDate = s.CreatedDate,
            RegistryName = s.RegistryName
        };

    public async Task<string> GenerateCancerRegistryReportAsync(
        string tumorId,
        string reportingFacility,
        string registrarId,
        string registrarName)
    {
        string reportId = $"CR-REPORT:{Guid.NewGuid()}";
        ICancerRegistryReportGrain report = CrReport(reportId);
        await report.GenerateReportAsync(PatientId, tumorId, reportingFacility, registrarId, registrarName);
        GrainStates.CancerRegistryReportState state = await report.GetReportAsync();
        await CrReportIndex().UpsertReportAsync(BuildCrIndexEntry(state));
        return reportId;
    }

    public Task<GrainStates.CancerRegistryReportState> GetCancerRegistryReportAsync(string reportId)
        => CrReport(reportId).GetReportAsync();

    public Task<string> GetCancerRegistryNaaccrAbstractAsync(string reportId)
        => CrReport(reportId).GetNaaccrAbstractAsync();

    public async Task SubmitCancerRegistryReportAsync(string reportId, string registryName, string? confirmationNumber)
    {
        await CrReport(reportId).SubmitReportAsync(registryName, confirmationNumber);
        GrainStates.CancerRegistryReportState state = await CrReport(reportId).GetReportAsync();
        await CrReportIndex().UpsertReportAsync(BuildCrIndexEntry(state));
    }

    public async Task AcceptCancerRegistryReportAsync(string reportId, string? registryResponse)
    {
        await CrReport(reportId).AcceptReportAsync(registryResponse);
        GrainStates.CancerRegistryReportState state = await CrReport(reportId).GetReportAsync();
        await CrReportIndex().UpsertReportAsync(BuildCrIndexEntry(state));
    }

    public async Task RejectCancerRegistryReportAsync(string reportId, string rejectionReason)
    {
        await CrReport(reportId).RejectReportAsync(rejectionReason);
        GrainStates.CancerRegistryReportState state = await CrReport(reportId).GetReportAsync();
        await CrReportIndex().UpsertReportAsync(BuildCrIndexEntry(state));
    }
}
