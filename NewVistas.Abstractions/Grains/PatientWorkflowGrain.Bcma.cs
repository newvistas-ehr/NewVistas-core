// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── BCMA (File #53.79) ──────────────────────────────────────────────

    public async Task<string> RecordMedicationAdministrationAsync(
        string drugName, string? drugId, string? dosage, string? route,
        string actionStatus, DateTime? scheduledDateTime, DateTime administrationDateTime,
        string? administeredById, string? administeredByName,
        string? injectionSite, string? prescriptionId, string? orderId, string? comments)
    {
        var bcmaId = $"BCMA-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IBcmaGrain>(bcmaId);
        await grain.RecordAdministrationAsync(PatientId, drugName, drugId, dosage, route,
            actionStatus, scheduledDateTime, administrationDateTime,
            administeredById, administeredByName, injectionSite, prescriptionId, orderId, comments);
        await AppendCappedIdAsync(PatientHistoryDomains.Bcma, bcmaId, DateTime.UtcNow);
        return bcmaId;
    }

    public async Task<List<BcmaSummary>> GetMedicationAdministrationsAsync(int maxResults)
    {
        var ids = await GetPatientGrain().GetBcmaIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IBcmaGrain>(id).GetAdministrationAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.AdministrationDateTime).Take(maxResults)
            .Select(s => new BcmaSummary
            {
                BcmaId = s.AdministrationId, DrugName = s.DrugName, Dosage = s.Dosage,
                ActionStatus = s.ActionStatus, AdministrationDateTime = s.AdministrationDateTime ?? DateTime.MinValue,
                AdministeredByName = s.AdministeredByName
            }).ToList();
    }

    /// <summary>
    /// Paged full administration history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<BcmaSummary>> GetBcmaHistoryAsync(int offset, int maxResults)
    {
        var ids = await GetHistoryPageIdsAsync(PatientHistoryDomains.Bcma, offset, maxResults);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IBcmaGrain>(id).GetAdministrationAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(s => new BcmaSummary
            {
                BcmaId = s.AdministrationId, DrugName = s.DrugName, Dosage = s.Dosage,
                ActionStatus = s.ActionStatus, AdministrationDateTime = s.AdministrationDateTime ?? DateTime.MinValue,
                AdministeredByName = s.AdministeredByName
            }).ToList();
    }

    // ─── BCMA MAR (Medication Administration Record) ─────────────────────

    private IPatientMARGrain GetMARGrain() =>
        GrainFactory.GetGrain<IPatientMARGrain>($"BCMA-MAR:{PatientId}");

    public Task<List<MarEntry>> GetPatientMARAsync() =>
        GetMARGrain().GetMARAsync();

    public Task<List<MarEntry>> GetDueMedicationsAsync() =>
        GetMARGrain().GetDueNowAsync();

    public async Task SyncOrderToMARAsync(string orderId)
    {
        InpatientOrderState order = await GrainFactory
            .GetGrain<IInpatientOrderGrain>(orderId)
            .GetOrderAsync();

        MarEntry entry = new()
        {
            OrderId = orderId,
            DrugName = order.DrugName,
            Dosage = order.Dosage,
            Route = order.Route,
            Schedule = order.Schedule,
            OrderType = order.OrderType,
            Priority = order.Priority,
            WardId = order.WardId,
            RoomBed = order.RoomBed,
            ProviderName = order.ProviderName,
            StartDate = order.StartDate,
            StopDate = order.StopDate,
            ScheduledTimes = new List<DateTime>(order.ScheduledAdminTimes),
            BcmaAdministrationIds = new List<string>(order.BcmaAdministrationIds),
            IsActive = order.Status == "ACTIVE"
        };

        // Carry over admin counts from the order's existing BCMA link list
        entry.TotalAdministrations = order.BcmaAdministrationIds.Count;

        await GetMARGrain().AddOrUpdateEntryAsync(entry);
    }

    public Task DeactivateOrderOnMARAsync(string orderId) =>
        GetMARGrain().DeactivateEntryAsync(orderId);

    public async Task<string> AdministerMedicationAsync(
        string orderId,
        string actionStatus,
        DateTime administrationDateTime,
        string? administeredById,
        string? administeredByName,
        string? injectionSite,
        string? prnReason,
        string? comments)
    {
        // Fetch order context for drug name / dosage / route
        InpatientOrderState order = await GrainFactory
            .GetGrain<IInpatientOrderGrain>(orderId)
            .GetOrderAsync();

        // Create the individual BCMA administration grain
        string bcmaId = $"BCMA-{Guid.NewGuid()}";
        IBcmaGrain bcmaGrain = GrainFactory.GetGrain<IBcmaGrain>(bcmaId);
        await bcmaGrain.RecordAdministrationAsync(
            PatientId, order.DrugName, order.DrugId, order.Dosage, order.Route,
            actionStatus, null, administrationDateTime,
            administeredById, administeredByName, injectionSite,
            null, orderId, comments);

        if (!string.IsNullOrEmpty(prnReason))
            await bcmaGrain.RecordPrnReasonAsync(prnReason);

        if (actionStatus == "NOT GIVEN" && !string.IsNullOrEmpty(comments))
            await bcmaGrain.MarkNotGivenAsync(comments);

        // Link BCMA record to the inpatient order
        await GrainFactory.GetGrain<IInpatientOrderGrain>(orderId)
            .RecordAdministrationAsync(bcmaId, administrationDateTime);

        // Track on the patient's flat BCMA ID list
        await AppendCappedIdAsync(PatientHistoryDomains.Bcma, bcmaId, DateTime.UtcNow);

        // Update the MAR entry
        await GetMARGrain().RecordAdministrationAsync(
            orderId, bcmaId, actionStatus, administrationDateTime, administeredByName);

        return bcmaId;
    }

    // ─── Imaging (File #2005) ────────────────────────────────────────────

    public async Task<string> CaptureImageAsync(
        string objectType, string? procedureDescription, string? specialtyIndex,
        string? imageUrl, string? thumbnailUrl,
        string? dicomSeriesUid, string? dicomStudyUid,
        DateTime? procedureDate, DateTime captureDate, int imageCount,
        string? radiologyId, string? tiuDocumentId,
        string? capturedById, string? capturedByName,
        string? locationId, string? locationName, string? comments)
    {
        var imageId = $"IMG-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.CaptureImageAsync(PatientId, objectType, procedureDescription, specialtyIndex,
            imageUrl, thumbnailUrl, dicomSeriesUid, dicomStudyUid, procedureDate, captureDate,
            imageCount, radiologyId, tiuDocumentId, capturedById, capturedByName,
            locationId, locationName, comments);
        await AppendCappedIdAsync(PatientHistoryDomains.Imaging, imageId, DateTime.UtcNow);
        return imageId;
    }

    public async Task<List<ImagingSummary>> GetImagesAsync(int maxResults)
    {
        var ids = await GetPatientGrain().GetImagingIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IImagingGrain>(id).GetImageAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.Where(s => s.Status != "DELETED").OrderByDescending(s => s.CaptureDate).Take(maxResults)
            .Select(s => new ImagingSummary
            {
                ImageId = s.ImageId, ObjectType = s.ObjectType,
                ProcedureDescription = s.ProcedureDescription, Status = s.Status,
                CaptureDate = s.CaptureDate ?? DateTime.MinValue, ImageCount = s.ImageCount
            }).ToList();
    }

    /// <summary>
    /// Paged full imaging history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<ImagingSummary>> GetImagingHistoryAsync(int offset, int maxResults)
    {
        var ids = await GetHistoryPageIdsAsync(PatientHistoryDomains.Imaging, offset, maxResults);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IImagingGrain>(id).GetImageAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.Where(s => s.Status != "DELETED")
            .Select(s => new ImagingSummary
            {
                ImageId = s.ImageId, ObjectType = s.ObjectType,
                ProcedureDescription = s.ProcedureDescription, Status = s.Status,
                CaptureDate = s.CaptureDate ?? DateTime.MinValue, ImageCount = s.ImageCount
            }).ToList();
    }

    // ─── Imaging Depth (File #2005 Phase 5) ─────────────────────────────────

    public async Task RecordImagingDicomMetadataAsync(string imageId, string? studyUid, string? seriesUid, string? instanceUid, string? modality, string? bodyPart, string? transferSyntax)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.RecordDicomMetadataAsync(studyUid, seriesUid, instanceUid, modality, bodyPart, transferSyntax);
    }

    public async Task SetImagingDimensionsAsync(string imageId, int width, int height, long? fileSizeBytes, string? compressionType)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.SetImageDimensionsAsync(width, height, fileSizeBytes, compressionType);
    }

    public async Task SetImagingClinicalDisplayStatusAsync(string imageId, string status)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.SetClinicalDisplayStatusAsync(status);
    }

    public async Task LinkImagingToPackageAsync(string imageId, string packageType, string packageReference)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.LinkToPackageAsync(packageType, packageReference);
    }

    public async Task RecordImagingAcquisitionAsync(string imageId, string? acquisitionSite, DateTime acquisitionDateTime, string? patientOrientation)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.RecordAcquisitionAsync(acquisitionSite, acquisitionDateTime, patientOrientation);
    }

    public async Task AddImagingAnnotationAsync(string imageId, string annotationType, string content, string? authorName)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.AddAnnotationAsync(annotationType, content, authorName);
    }

    public async Task AddImagingSeriesInfoAsync(string imageId, string seriesUid, string? seriesDescription, string? modality, int imageCount, int? seriesNumber)
    {
        var grain = GrainFactory.GetGrain<IImagingGrain>(imageId);
        await grain.AddSeriesInfoAsync(seriesUid, seriesDescription, modality, imageCount, seriesNumber);
    }
}
