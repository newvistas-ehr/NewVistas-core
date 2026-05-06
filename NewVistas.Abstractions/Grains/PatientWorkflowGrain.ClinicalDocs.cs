// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── TIU Notes (TIUSRVN.m, TIUSRVL.m, TIUSRVP.m) ───────────────────

    // ─── Helpers ─────────────────────────────────────────────────────────

    private IPatientNoteIndexGrain GetNoteIndexGrain()
        => GrainFactory.GetGrain<IPatientNoteIndexGrain>(PatientId);

    private NoteIndexEntry BuildNoteIndexEntry(string documentId, TiuDocumentState state, bool isAddendum = false)
        => new()
        {
            DocumentGrainKey = documentId,
            ReferenceDate = state.ReferenceDate,
            DocumentType = state.DocumentType,
            Status = state.Status,
            Subject = state.Subject,
            AuthorName = state.AuthorName,
            LocationName = state.LocationName,
            HasAddenda = state.AddendumIds.Count > 0,
            IsAddendum = isAddendum
        };

    private static TiuNoteSummary IndexEntryToSummary(NoteIndexEntry entry)
        => new()
        {
            DocumentId = entry.DocumentGrainKey,
            DocumentType = entry.DocumentType,
            Subject = entry.Subject,
            AuthorName = entry.AuthorName,
            Status = entry.Status,
            ReferenceDate = entry.ReferenceDate,
            LocationName = entry.LocationName,
            HasAddenda = entry.HasAddenda
        };

    private async Task SyncNoteToIndexAndCacheAsync(string documentId, TiuDocumentState state, bool isAddendum = false)
    {
        var indexEntry = BuildNoteIndexEntry(documentId, state, isAddendum);

        // Update the per-patient index
        await GetNoteIndexGrain().AddOrUpdateNoteAsync(indexEntry);

        // Update the hot cache on the patient grain (top-level, non-retracted notes only)
        if (!isAddendum && state.Status != "RETRACTED")
        {
            var siteGrain = GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            int displayCount = await siteGrain.GetNotesDisplayCountAsync();
            var summary = IndexEntryToSummary(indexEntry);
            await GetPatientGrain().AddRecentNoteAsync(summary, displayCount);
        }
    }

    /// <summary>
    /// Creates a new TIU document. Mirrors TIUSRVN.m CREATE.
    /// </summary>
    public async Task<string> CreateNoteAsync(
        string documentType, string? documentTypeId,
        string reportText, string? subject,
        string? authorId, string? authorName,
        string? cosignerId, string? cosignerName,
        string? locationId, string? locationName,
        string? visitId, DateTime referenceDate)
    {
        var documentId = $"TIU-{Guid.NewGuid()}";
        var tiuGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);

        await tiuGrain.CreateDocumentAsync(
            PatientId, documentType, documentTypeId,
            reportText, subject,
            authorId, authorName,
            cosignerId, cosignerName,
            locationId, locationName,
            visitId, referenceDate);

        await GetPatientGrain().AddTiuDocumentIdAsync(documentId);

        // Populate index + hot cache
        var state = await tiuGrain.GetDocumentAsync();
        await SyncNoteToIndexAndCacheAsync(documentId, state);

        return documentId;
    }

    /// <summary>
    /// Signs a TIU document. Mirrors TIUSRVA.m SIGN.
    /// If cosigner is required, transitions to UNCOSIGNED; otherwise COMPLETED.
    /// </summary>
    public async Task SignNoteAsync(string documentId)
    {
        var tiuGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);
        await tiuGrain.SignDocumentAsync(DateTime.UtcNow);

        // Sync updated status to index + cache
        var state = await tiuGrain.GetDocumentAsync();
        bool isAddendum = !string.IsNullOrEmpty(state.ParentDocumentId);
        await SyncNoteToIndexAndCacheAsync(documentId, state, isAddendum);
    }

    /// <summary>
    /// Cosigns a TIU document. Mirrors TIUSRVA.m COSIGN.
    /// Transitions from UNCOSIGNED → COMPLETED.
    /// </summary>
    public async Task CosignNoteAsync(string documentId)
    {
        var tiuGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);
        await tiuGrain.CosignDocumentAsync(DateTime.UtcNow);

        // Sync updated status to index + cache
        var state = await tiuGrain.GetDocumentAsync();
        bool isAddendum = !string.IsNullOrEmpty(state.ParentDocumentId);
        await SyncNoteToIndexAndCacheAsync(documentId, state, isAddendum);
    }

    /// <summary>
    /// Amends a completed TIU document. Mirrors TIUSRVA.m AMEND.
    /// </summary>
    public async Task AmendNoteAsync(string documentId, string amendedText)
    {
        var tiuGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);
        await tiuGrain.AmendDocumentAsync(amendedText);

        // Sync updated status to index + cache
        var state = await tiuGrain.GetDocumentAsync();
        bool isAddendum = !string.IsNullOrEmpty(state.ParentDocumentId);
        await SyncNoteToIndexAndCacheAsync(documentId, state, isAddendum);
    }

    /// <summary>
    /// Creates an addendum to an existing note. Mirrors TIU addendum workflow.
    /// </summary>
    public async Task<string> AddAddendumAsync(
        string parentDocumentId, string reportText,
        string? authorId, string? authorName,
        DateTime referenceDate)
    {
        var addendumId = $"TIU-{Guid.NewGuid()}";
        var addendumGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(addendumId);

        await addendumGrain.CreateDocumentAsync(
            PatientId, "ADDENDUM", null,
            reportText, $"Addendum to {parentDocumentId}",
            authorId, authorName,
            null, null,
            null, null,
            null, referenceDate);

        // Set parent link so addenda are excluded from top-level listings
        await addendumGrain.SetParentDocumentIdAsync(parentDocumentId);

        // Link addendum to parent
        var parentGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(parentDocumentId);
        await parentGrain.AddAddendumAsync(addendumId);

        // Track in patient's document list
        await GetPatientGrain().AddTiuDocumentIdAsync(addendumId);

        // Populate index (addendum — excluded from hot cache)
        var addendumState = await addendumGrain.GetDocumentAsync();
        await SyncNoteToIndexAndCacheAsync(addendumId, addendumState, isAddendum: true);

        // Update parent's HasAddenda in index
        var parentState = await parentGrain.GetDocumentAsync();
        await SyncNoteToIndexAndCacheAsync(parentDocumentId, parentState);

        return addendumId;
    }

    /// <summary>
    /// Gets a single note by ID. Mirrors TIUSRVP.m GET.
    /// </summary>
    public async Task<TiuDocumentState> GetNoteAsync(string documentId)
    {
        var tiuGrain = GrainFactory.GetGrain<ITiuDocumentGrain>(documentId);
        return await tiuGrain.GetDocumentAsync();
    }

    /// <summary>
    /// Lists notes for the patient. Mirrors TIUSRVL.m LIST.
    /// Reads from the per-patient index — no grain fan-out.
    /// Optionally filtered by document type (PROGRESS NOTE, DISCHARGE SUMMARY, etc.)
    /// </summary>
    public async Task<List<TiuNoteSummary>> GetNotesAsync(string? documentType, int maxResults)
    {
        var entries = await GetNoteIndexGrain().GetEntriesAsync(documentType, maxResults);
        return entries.Select(IndexEntryToSummary).ToList();
    }

    /// <summary>
    /// Gets the recent notes cache from the patient grain — zero fan-out.
    /// </summary>
    public async Task<List<TiuNoteSummary>> GetRecentNotesAsync()
    {
        return await GetPatientGrain().GetRecentNotesAsync();
    }

    /// <summary>
    /// Gets note history from the index grain with date range filtering.
    /// </summary>
    public async Task<List<TiuNoteSummary>> GetNoteHistoryAsync(DateTime? from, DateTime? to, int maxCount)
    {
        DateTime rangeFrom = from ?? DateTime.MinValue;
        DateTime rangeTo = to ?? DateTime.UtcNow;
        var entries = await GetNoteIndexGrain().GetEntriesByDateRangeAsync(rangeFrom, rangeTo);
        return entries.Take(maxCount).Select(IndexEntryToSummary).ToList();
    }

    // ─── Consults (GMRCACTM.m, File #123) ───────────────────────────────

    /// <summary>
    /// Requests a new consult. Mirrors GMRCACTM REQUEST action.
    /// </summary>
    public async Task<string> RequestConsultAsync(
        string toService, string? toServiceId,
        string? fromService, string? fromServiceId,
        string urgency,
        string? requestingProviderId, string? requestingProviderName,
        string? attentionProviderId, string? attentionProviderName,
        string? reasonForRequest, string? provisionalDiagnosis,
        string? orderId, string? locationId, string? locationName)
    {
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);

        await consultGrain.RequestConsultAsync(
            PatientId, toService, toServiceId,
            fromService, fromServiceId, urgency,
            requestingProviderId, requestingProviderName,
            attentionProviderId, attentionProviderName,
            reasonForRequest, provisionalDiagnosis,
            orderId, locationId, locationName);

        await GetPatientGrain().AddConsultIdAsync(consultId);

        return consultId;
    }

    /// <summary>
    /// Accepting service acknowledges the consult. PENDING → ACTIVE.
    /// </summary>
    public async Task AcceptConsultAsync(string consultId)
    {
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await consultGrain.AcceptAsync();
    }

    /// <summary>
    /// Consult is scheduled for a specific date. ACTIVE → SCHEDULED.
    /// </summary>
    public async Task ScheduleConsultAsync(string consultId)
    {
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await consultGrain.ScheduleAsync();
    }

    /// <summary>
    /// Completes a consult by writing a result note (TIU document) and linking it.
    /// Mirrors the CPRS workflow: consult completion requires a consult note.
    /// </summary>
    public async Task CompleteConsultAsync(string consultId, string? resultNoteText,
        string? authorId, string? authorName)
    {
        string? resultDocumentId = null;

        // If result note text is provided, create a CONSULT NOTE TIU document
        if (!string.IsNullOrWhiteSpace(resultNoteText))
        {
            resultDocumentId = await CreateNoteAsync(
                "CONSULT NOTE", null,
                resultNoteText, $"Consult Result: {consultId}",
                authorId, authorName,
                null, null,
                null, null,
                null, DateTime.UtcNow);
        }

        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await consultGrain.CompleteAsync(DateTime.UtcNow, resultDocumentId);
    }

    /// <summary>
    /// Cancels a consult. Terminal state.
    /// </summary>
    public async Task CancelConsultAsync(string consultId, string? comments)
    {
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await consultGrain.CancelAsync(comments);
    }

    /// <summary>
    /// Discontinues a consult. Terminal state.
    /// </summary>
    public async Task DiscontinueConsultAsync(string consultId, string? comments)
    {
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await consultGrain.DiscontinueAsync(comments);
    }

    /// <summary>
    /// Gets a single consult by ID.
    /// </summary>
    public async Task<ConsultState> GetConsultAsync(string consultId)
    {
        var consultGrain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        return await consultGrain.GetConsultAsync();
    }

    /// <summary>
    /// Lists consults for the patient. Optionally filtered by status.
    /// </summary>
    public async Task<List<ConsultSummary>> GetConsultsAsync(string? statusFilter, int maxResults)
    {
        var patientGrain = GetPatientGrain();
        var consultIds = await patientGrain.GetConsultIdsAsync();

        // Fan-out: fire all grain calls concurrently
        var tasks = consultIds.Select(id => GrainFactory.GetGrain<IConsultGrain>(id).GetConsultAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Where(state => statusFilter == null ||
                state.Status.Equals(statusFilter, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(state => state.RequestDateTime)
            .Take(maxResults)
            .Select(state => new ConsultSummary
            {
                ConsultId = state.ConsultId,
                ToService = state.ToService,
                FromService = state.FromService,
                Status = state.Status,
                Urgency = state.Urgency,
                RequestDateTime = state.RequestDateTime,
                RequestingProviderName = state.RequestingProviderName,
                AttentionProviderName = state.AttentionProviderName,
                ProvisionalDiagnosis = state.ProvisionalDiagnosis,
                HasResultDocument = !string.IsNullOrEmpty(state.ResultDocumentId)
            })
            .ToList();
    }

    // ─── Surgery (File #130) ─────────────────────────────────────────────

    public async Task<string> ScheduleSurgeryAsync(
        string principalProcedure, string? cptCode, DateTime dateOfOperation,
        string? surgeonId, string? surgeonName, string? anesthesiaTechnique,
        string? surgicalSpecialty, string? preOpDiagnosis,
        string? locationId, string? locationName, string? comments)
    {
        var surgeryId = $"SURG-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.ScheduleSurgeryAsync(PatientId, principalProcedure, cptCode, dateOfOperation,
            surgeonId, surgeonName, anesthesiaTechnique, surgicalSpecialty, preOpDiagnosis,
            locationId, locationName, comments);
        await GetPatientGrain().AddSurgeryIdAsync(surgeryId);
        return surgeryId;
    }

    public async Task CompleteSurgeryAsync(string surgeryId, string? operativeReport, string? postOpDiagnosis)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        if (!string.IsNullOrWhiteSpace(operativeReport))
            await grain.RecordOperativeReportAsync(operativeReport, postOpDiagnosis, null);
        await grain.CompleteAsync();
    }

    public async Task CancelSurgeryAsync(string surgeryId, string? comments)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.CancelAsync(comments);
    }

    public async Task<SurgeryState> GetSurgeryAsync(string surgeryId)
        => await GrainFactory.GetGrain<ISurgeryGrain>(surgeryId).GetSurgeryAsync();

    public async Task<List<SurgerySummary>> GetSurgeriesAsync(int maxResults)
    {
        var ids = await GetPatientGrain().GetSurgeryIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<ISurgeryGrain>(id).GetSurgeryAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.DateOfOperation).Take(maxResults)
            .Select(s => new SurgerySummary
            {
                SurgeryId = s.SurgeryId, PrincipalProcedure = s.PrincipalProcedure,
                CptCode = s.PrincipalProcedureCptCode, DateOfOperation = s.DateOfOperation ?? DateTime.MinValue,
                SurgeonName = s.SurgeonName, Status = s.Status, SurgicalSpecialty = s.SurgicalSpecialty
            }).ToList();
    }

    // ─── Radiology (File #75.1) ──────────────────────────────────────────

    public async Task<string> OrderRadiologyStudyAsync(
        string procedureName, string? procedureId, string? cptCode, string? imagingType,
        string? requestingProviderId, string? requestingProviderName,
        string? urgency, string? clinicalHistory, string? reasonForStudy,
        string? orderId, string? locationId, string? locationName)
    {
        var radiologyId = $"RAD-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(radiologyId);
        await grain.OrderStudyAsync(PatientId, procedureName, procedureId, cptCode, imagingType,
            requestingProviderId, requestingProviderName, urgency, clinicalHistory, reasonForStudy,
            orderId, locationId, locationName);
        await GetPatientGrain().AddRadiologyIdAsync(radiologyId);
        return radiologyId;
    }

    public async Task CompleteRadiologyAsync(string radiologyId, string? reportText, string? impression,
        string? interpretingPhysicianId, string? interpretingPhysicianName)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(radiologyId);
        await grain.RecordExamAsync(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(reportText))
            await grain.RecordReportAsync(reportText, impression, null,
                interpretingPhysicianId, interpretingPhysicianName, DateTime.UtcNow);
        await grain.CompleteAsync();
    }

    public async Task<RadiologyState> GetRadiologyStudyAsync(string radiologyId)
        => await GrainFactory.GetGrain<IRadiologyGrain>(radiologyId).GetRadiologyAsync();

    public async Task<List<RadiologySummary>> GetRadiologyStudiesAsync(int maxResults)
    {
        var ids = await GetPatientGrain().GetRadiologyIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IRadiologyGrain>(id).GetRadiologyAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.ExamDateTime ?? s.OrderDateTime).Take(maxResults)
            .Select(s => new RadiologySummary
            {
                RadiologyId = s.RadiologyId, ProcedureName = s.ProcedureName,
                ImagingType = s.ImagingType, Status = s.Status,
                ExamDateTime = s.ExamDateTime, RequestingProviderName = s.RequestingProviderName,
                HasReport = !string.IsNullOrEmpty(s.ReportText)
            }).ToList();
    }

    // ─── Surgery Depth (File #130 Phase 4) ────────────────────────────────

    public async Task RecordPreOpAssessmentAsync(string surgeryId, string notes, string providerId, string providerName)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.RecordPreOpAssessmentAsync(0, notes, providerId, providerName, DateTime.UtcNow);
    }

    public async Task AddSurgicalComplicationAsync(string surgeryId, string complicationType, string description, string? severity, string? treatment)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.AddComplicationAsync(complicationType, description, severity, DateTime.UtcNow, treatment);
    }

    public async Task AddSurgicalImplantAsync(string surgeryId, string implantName, string? manufacturer, string? serialNumber, string? lotNumber)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.AddImplantAsync(implantName, manufacturer, serialNumber, lotNumber, null);
    }

    public async Task AddSurgicalAssistantAsync(string surgeryId, string assistantId, string assistantName, string? role)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.AddSurgicalAssistantAsync(assistantId, assistantName, role);
    }

    public async Task RecordIntraOpDetailsAsync(string surgeryId, int? estimatedBloodLoss, int? spongeCountCorrect, int? needleCountCorrect, int? instrumentCountCorrect, string? dispositionAfterSurgery)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.RecordIntraOpDetailsAsync(estimatedBloodLoss, spongeCountCorrect, needleCountCorrect, instrumentCountCorrect, dispositionAfterSurgery);
    }

    public async Task AddAnesthesiaAgentAsync(string surgeryId, string agent)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.AddAnesthesiaAgentAsync(agent);
    }

    public async Task AddSurgicalSpecimenAsync(string surgeryId, string specimenType, string? description, string? pathologyResult)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        await grain.AddSpecimenAsync(specimenType, description, pathologyResult, DateTime.UtcNow);
    }

    public async Task<List<SurgicalComplication>> GetSurgicalComplicationsAsync(string surgeryId)
    {
        var grain = GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        return await grain.GetComplicationsAsync();
    }

    // ─── Radiology Depth (File #75.1 Phase 4) ─────────────────────────────

    public async Task RecordRadiologyContrastAsync(string studyId, string contrastAgent, string? route, double? volumeMl)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.RecordContrastAsync(contrastAgent, route, volumeMl);
    }

    public async Task RecordRadiologyContrastReactionAsync(string studyId, string reactionDetails)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.RecordContrastReactionAsync(reactionDetails);
    }

    public async Task RecordRadiationDoseAsync(string studyId, double? doseMSv, double? ctdiVol, double? doseLengthProduct)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.RecordRadiationDoseAsync(doseMSv, ctdiVol, doseLengthProduct);
    }

    public async Task SignRadiologyReportAsync(string studyId, string signedById, string signedByName)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.SignReportAsync(signedById, signedByName, DateTime.UtcNow);
    }

    public async Task FlagCriticalRadiologyResultAsync(string studyId)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.FlagCriticalResultAsync();
    }

    public async Task RecordCriticalResultNotificationAsync(string studyId, string notifiedTo)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.RecordCriticalResultNotificationAsync(notifiedTo, DateTime.UtcNow);
    }

    public async Task AcknowledgeCriticalResultAsync(string studyId, string acknowledgedBy)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.AcknowledgeCriticalResultAsync(acknowledgedBy);
    }

    public async Task AmendRadiologyReportAsync(string studyId, string amendmentText)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        await grain.AmendReportAsync(amendmentText);
    }

    public async Task<bool> IsRadiologyCriticalResultAsync(string studyId)
    {
        var grain = GrainFactory.GetGrain<IRadiologyGrain>(studyId);
        return await grain.IsCriticalResultAsync();
    }

    // ─── Consults Depth (File #123 Phase 4) ────────────────────────────────

    public async Task AddConsultTrackingCommentAsync(string consultId, string authorId, string authorName, string commentText, string? actionTaken)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.AddTrackingCommentAsync(authorId, authorName, commentText, actionTaken);
    }

    public async Task AcceptConsultWithDetailsAsync(string consultId, string acceptedById, string acceptedByName)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.AcceptWithDetailsAsync(acceptedById, acceptedByName);
    }

    public async Task ScheduleConsultWithDetailsAsync(string consultId, DateTime scheduledDateTime, string? clinicId, string? clinicName)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.ScheduleWithDetailsAsync(scheduledDateTime, clinicId, clinicName);
    }

    public async Task SetConsultTypeAsync(string consultId, string consultType)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.SetConsultTypeAsync(consultType);
    }

    public async Task SetConsultClinicalHistoryAsync(string consultId, string clinicalHistory)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.SetClinicalHistoryAsync(clinicalHistory);
    }

    public async Task SetConsultFollowUpRecommendationAsync(string consultId, string recommendation)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.SetFollowUpRecommendationAsync(recommendation);
    }

    public async Task SetConsultingProviderAsync(string consultId, string providerId, string providerName)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.SetConsultingProviderAsync(providerId, providerName);
    }

    public async Task MarkConsultInterfacilityAsync(string consultId, string externalFacilityId, string externalFacilityName)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        await grain.MarkInterfacilityAsync(externalFacilityId, externalFacilityName);
    }

    public async Task<List<ConsultTrackingComment>> GetConsultTrackingCommentsAsync(string consultId)
    {
        var grain = GrainFactory.GetGrain<IConsultGrain>(consultId);
        return await grain.GetTrackingCommentsAsync();
    }

    // ─── Radiology Tech Workflow — RARTE.m ──────────────────────────────────

    private IRadExamTrackingGrain RadExam(string radiologyId)
        => GrainFactory.GetGrain<IRadExamTrackingGrain>($"RAD-EXAM:{radiologyId}");

    private IRadProtocolIndexGrain RadProtocolIndex()
        => GrainFactory.GetGrain<IRadProtocolIndexGrain>("RAD-PROTOCOL-INDEX");

    public async Task InitializeRadExamTrackingAsync(string radiologyId, DateTime? scheduledDateTime, string? room)
    {
        await RadExam(radiologyId).InitializeAsync(radiologyId, PatientId);
        if (scheduledDateTime.HasValue)
            await RadExam(radiologyId).ScheduleAsync(scheduledDateTime.Value, room);
    }

    public Task<GrainStates.RadExamTrackingState> GetRadExamTrackingAsync(string radiologyId)
        => RadExam(radiologyId).GetAsync();

    public Task AssignRadProtocolAsync(string radiologyId, string protocolId, string protocolName, string? parameters)
        => RadExam(radiologyId).AssignProtocolAsync(protocolId, protocolName, parameters);

    public Task MarkRadPatientPreppedAsync(string radiologyId, string? prepNotes)
        => RadExam(radiologyId).MarkPatientPreppedAsync(prepNotes);

    public Task StartRadExamAsync(string radiologyId)
        => RadExam(radiologyId).StartExamAsync();

    public async Task CompleteRadExamAsync(string radiologyId, int? imageCount, string? techNotes)
    {
        await RadExam(radiologyId).CompleteExamAsync(imageCount, techNotes);
        // Also record exam on the base radiology grain
        IRadiologyGrain rad = GrainFactory.GetGrain<IRadiologyGrain>(radiologyId);
        await rad.RecordExamAsync(DateTime.UtcNow);
    }

    public Task SendRadImagesToPacsAsync(string radiologyId)
        => RadExam(radiologyId).SendImagesToPacsAsync();

    public async Task LinkImageToRadExamAsync(string radiologyId, string imageId)
    {
        await RadExam(radiologyId).LinkImageAsync(imageId);
    }

    public Task<List<GrainStates.RadProtocolIndexEntry>> GetRadProtocolsAsync()
        => RadProtocolIndex().GetAllAsync();

    public Task<List<GrainStates.RadProtocolIndexEntry>> GetRadProtocolsByTypeAsync(string imagingType)
        => RadProtocolIndex().GetByImagingTypeAsync(imagingType);

    public async Task<GrainStates.RadWorklistState> RefreshRadWorklistAsync(string locationId)
    {
        IRadWorklistGrain worklist = GrainFactory.GetGrain<IRadWorklistGrain>($"RAD-WORKLIST:{locationId}");
        List<GrainStates.RadWorklistItem> items = new();

        try
        {
            IPatientGrain patient = GrainFactory.GetGrain<IPatientGrain>(PatientId);
            List<string> radIds = await patient.GetRadiologyIdsAsync();

            foreach (string radId in radIds)
            {
                IRadiologyGrain rad = GrainFactory.GetGrain<IRadiologyGrain>(radId);
                GrainStates.RadiologyState rs = await rad.GetRadiologyAsync();
                if (rs.Status == "COMPLETE" || rs.Status == "CANCELLED") continue;

                // Try to get exam tracking state
                GrainStates.RadExamStatus examStatus = rs.Status switch
                {
                    "PENDING" => GrainStates.RadExamStatus.Ordered,
                    "EXAMINED" => GrainStates.RadExamStatus.ExamComplete,
                    _ => GrainStates.RadExamStatus.Ordered,
                };

                try
                {
                    GrainStates.RadExamTrackingState track = await RadExam(radId).GetAsync();
                    if (!string.IsNullOrEmpty(track.RadiologyId))
                        examStatus = track.ExamStatus;
                }
                catch { }

                items.Add(new GrainStates.RadWorklistItem
                {
                    RadiologyId    = radId,
                    PatientId      = PatientId,
                    ProcedureName  = rs.ProcedureName,
                    ImagingType    = rs.ImagingType,
                    CptCode        = rs.CptCode,
                    Urgency        = rs.Urgency,
                    ExamStatus     = examStatus,
                    BodyPart       = rs.BodyPart,
                    Laterality     = rs.Laterality,
                    TechnicianName = rs.TechnicianName,
                    OrderDateTime  = rs.OrderDateTime ?? rs.CreatedDate,
                });
            }
        }
        catch { }

        await worklist.RefreshAsync(items);
        return await worklist.GetAsync();
    }

    public Task<GrainStates.RadWorklistState> GetRadWorklistAsync(string locationId)
        => GrainFactory.GetGrain<IRadWorklistGrain>($"RAD-WORKLIST:{locationId}").GetAsync();
}
