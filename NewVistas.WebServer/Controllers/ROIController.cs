// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Release of Information: record requests, HIPAA disclosures,
/// and accounting of disclosures.
/// VistA File #195 (RELEASE OF INFORMATION). ROIS.m, ROI.m, ROIA.m
/// </summary>
[Authorize(Roles = "PrivacyOfficer,Administrator")]
[ApiController]
[Route("api/[controller]")]
public class ROIController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ROIController> _logger;

    public ROIController(IGrainFactory grainFactory, ILogger<ROIController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IROIRequestGrain RoiRequest(string id)
        => _grainFactory.GetGrain<IROIRequestGrain>(Uri.UnescapeDataString(id));

    private IROIRequestIndexGrain RequestIndex()
        => _grainFactory.GetGrain<IROIRequestIndexGrain>("ROI-REQUEST-IDX");

    private IHIPAADisclosureGrain Disclosure(string id)
        => _grainFactory.GetGrain<IHIPAADisclosureGrain>(Uri.UnescapeDataString(id));

    private IHIPAADisclosureIndexGrain DisclosureIndex(string patientId)
        => _grainFactory.GetGrain<IHIPAADisclosureIndexGrain>($"ROI-DISC-IDX:{patientId}");

    private static ROIRequestIndexEntry BuildRequestIndex(ROIRequestState s) => new()
    {
        RequestId = s.RequestId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        ReceivedDate = s.ReceivedDate,
        RequestType = s.RequestType,
        RequesterType = s.RequesterType,
        RequesterName = s.RequesterName,
        Status = s.Status,
        DueDate = s.DueDate,
        AssignedStaffName = s.AssignedStaffName,
        Priority = s.Priority
    };

    private static HIPAADisclosureIndexEntry BuildDisclosureIndex(HIPAADisclosureState s) => new()
    {
        DisclosureId = s.DisclosureId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        DisclosureDate = s.DisclosureDate,
        DisclosureType = s.DisclosureType,
        RecipientName = s.RecipientName,
        PurposeOfDisclosure = s.PurposeOfDisclosure,
        IsSubjectToAccounting = s.IsSubjectToAccounting,
        LinkedRequestId = s.LinkedRequestId
    };

    // ── ROI Requests ──────────────────────────────────────────────────────────

    [HttpGet("requests")]
    public async Task<IActionResult> GetAllRequests()
    {
        try
        {
            return Ok(await RequestIndex().GetAllRequestsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all ROI requests");
            return StatusCode(500, "Error retrieving requests.");
        }
    }

    [HttpGet("requests/status/{status}")]
    public async Task<IActionResult> GetRequestsByStatus(ROIRequestStatus status)
    {
        try
        {
            return Ok(await RequestIndex().GetRequestsByStatusAsync(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requests by status {Status}", status);
            return StatusCode(500, "Error retrieving requests.");
        }
    }

    [HttpGet("requests/patient/{patientId}")]
    public async Task<IActionResult> GetRequestsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await RequestIndex().GetRequestsByPatientAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requests for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving requests.");
        }
    }

    [HttpGet("requests/overdue")]
    public async Task<IActionResult> GetOverdueRequests()
    {
        try
        {
            return Ok(await RequestIndex().GetOverdueRequestsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue ROI requests");
            return StatusCode(500, "Error retrieving overdue requests.");
        }
    }

    [HttpGet("requests/{requestId}")]
    public async Task<IActionResult> GetRequest(string requestId)
    {
        try
        {
            return Ok(await RoiRequest(requestId).GetRequestAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving request {RequestId}", requestId);
            return StatusCode(500, "Error retrieving request.");
        }
    }

    [HttpPost("requests")]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitROIRequestDto dto)
    {
        try
        {
            string requestId = $"ROI-REQUEST:{Guid.NewGuid()}";
            await RoiRequest(requestId).SubmitRequestAsync(
                dto.PatientId, dto.PatientName, dto.PatientDOB,
                dto.RequestType, dto.RequesterType,
                dto.RequesterName, dto.RequesterOrganization, dto.RequesterAddress,
                dto.RequesterPhone, dto.RequesterFax, dto.RequesterEmail,
                dto.PurposeOfRequest, dto.RecordsRequested,
                dto.DateRangeStart, dto.DateRangeEnd,
                dto.Priority, dto.CreatedBy);
            ROIRequestState state = await RoiRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            return Created($"/api/roi/requests/{Uri.EscapeDataString(requestId)}", new { requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting ROI request for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error submitting request.");
        }
    }

    [HttpPost("requests/{requestId}/assign")]
    public async Task<IActionResult> AssignStaff(string requestId, [FromBody] AssignROIStaffDto dto)
    {
        try
        {
            await RoiRequest(requestId).AssignStaffAsync(dto.StaffId, dto.StaffName);
            ROIRequestState state = await RoiRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning staff to request {RequestId}", requestId);
            return StatusCode(500, "Error assigning staff.");
        }
    }

    [HttpPost("requests/{requestId}/authorization")]
    public async Task<IActionResult> UpdateAuthorization(string requestId, [FromBody] UpdateAuthorizationDto dto)
    {
        try
        {
            await RoiRequest(requestId).UpdateAuthorizationAsync(dto.AuthorizationStatus, dto.AuthorizationDate, dto.AuthorizationExpirationDate);
            ROIRequestState state = await RoiRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating authorization for request {RequestId}", requestId);
            return StatusCode(500, "Error updating authorization.");
        }
    }

    [HttpPost("requests/{requestId}/fulfill")]
    public async Task<IActionResult> FulfillRequest(string requestId, [FromBody] FulfillRequestDto dto)
    {
        try
        {
            await RoiRequest(requestId).FulfillRequestAsync(dto.FulfillmentMethod, dto.Notes, dto.NumberOfPages, dto.FeeCharged);
            ROIRequestState state = await RoiRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            // Automatically record HIPAA disclosure when fulfilling
            string disclosureId = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
            await Disclosure(disclosureId).RecordDisclosureAsync(
                state.PatientId, state.PatientName,
                HIPAADisclosureType.PatientAuthorization,
                state.RequesterName, state.RequesterOrganization, state.RequesterAddress,
                state.PurposeOfRequest, $"Records released: {string.Join(", ", state.RecordsRequested)}",
                $"{state.DateRangeStart:d} - {state.DateRangeEnd:d}",
                dto.NumberOfPages, true, requestId,
                dto.FulfilledBy, dto.FulfilledByTitle);
            HIPAADisclosureState disclosureState = await Disclosure(disclosureId).GetDisclosureAsync();
            await DisclosureIndex(state.PatientId).UpsertDisclosureAsync(BuildDisclosureIndex(disclosureState));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fulfilling request {RequestId}", requestId);
            return StatusCode(500, "Error fulfilling request.");
        }
    }

    [HttpPost("requests/{requestId}/deny")]
    public async Task<IActionResult> DenyRequest(string requestId, [FromBody] DenyRequestDto dto)
    {
        try
        {
            await RoiRequest(requestId).DenyRequestAsync(dto.DenialReason);
            ROIRequestState state = await RoiRequest(requestId).GetRequestAsync();
            await RequestIndex().UpsertRequestAsync(BuildRequestIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying request {RequestId}", requestId);
            return StatusCode(500, "Error denying request.");
        }
    }

    // ── HIPAA Disclosures ─────────────────────────────────────────────────────

    [HttpPost("disclosures")]
    public async Task<IActionResult> RecordDisclosure([FromBody] RecordDisclosureDto dto)
    {
        try
        {
            string disclosureId = $"ROI-DISCLOSURE:{Guid.NewGuid()}";
            await Disclosure(disclosureId).RecordDisclosureAsync(
                dto.PatientId, dto.PatientName, dto.DisclosureType,
                dto.RecipientName, dto.RecipientOrganization, dto.RecipientAddress,
                dto.PurposeOfDisclosure, dto.InformationDisclosed,
                dto.DateRangeOfInformation, dto.NumberOfPages,
                dto.AuthorizationReceived, dto.LinkedRequestId,
                dto.DisclosedBy, dto.DisclosedByTitle);
            HIPAADisclosureState state = await Disclosure(disclosureId).GetDisclosureAsync();
            await DisclosureIndex(dto.PatientId).UpsertDisclosureAsync(BuildDisclosureIndex(state));
            return Created($"/api/roi/disclosures/{Uri.EscapeDataString(disclosureId)}", new { disclosureId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording disclosure for patient {PatientId}", dto.PatientId);
            return StatusCode(500, "Error recording disclosure.");
        }
    }

    [HttpGet("disclosures/{disclosureId}")]
    public async Task<IActionResult> GetDisclosure(string disclosureId)
    {
        try
        {
            return Ok(await Disclosure(disclosureId).GetDisclosureAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disclosure {DisclosureId}", disclosureId);
            return StatusCode(500, "Error retrieving disclosure.");
        }
    }

    [HttpGet("disclosures/patient/{patientId}")]
    public async Task<IActionResult> GetDisclosuresForPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await DisclosureIndex(id).GetAllDisclosuresAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disclosures for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving disclosures.");
        }
    }

    [HttpGet("disclosures/patient/{patientId}/accounting")]
    public async Task<IActionResult> GetAccountingOfDisclosures(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            // HIPAA requires 6-year lookback for accounting
            List<HIPAADisclosureIndexEntry> all = await DisclosureIndex(id).GetDisclosuresSubjectToAccountingAsync();
            DateTime cutoff = DateTime.UtcNow.AddYears(-6);
            List<HIPAADisclosureIndexEntry> sixYear = all.Where(d => d.DisclosureDate >= cutoff).ToList();
            return Ok(sixYear);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating accounting of disclosures for patient {PatientId}", patientId);
            return StatusCode(500, "Error generating accounting of disclosures.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<ROIRequestIndexEntry> requests = await RequestIndex().GetAllRequestsAsync();
            DateTime now = DateTime.UtcNow;

            var dashboard = new
            {
                TotalRequests = requests.Count,
                OpenRequests = requests.Count(r => r.Status is ROIRequestStatus.Received
                    or ROIRequestStatus.Acknowledged or ROIRequestStatus.InProcess
                    or ROIRequestStatus.PendingAuthorization),
                OverdueRequests = requests.Count(r => r.DueDate < now
                    && r.Status is ROIRequestStatus.Received or ROIRequestStatus.Acknowledged
                    or ROIRequestStatus.InProcess or ROIRequestStatus.PendingAuthorization),
                PendingAuthorization = requests.Count(r => r.Status == ROIRequestStatus.PendingAuthorization),
                FulfilledRequests = requests.Count(r => r.Status == ROIRequestStatus.Fulfilled),
                DeniedRequests = requests.Count(r => r.Status == ROIRequestStatus.Denied),
                UrgentOpen = requests.Count(r => r.Priority == ROIRequestPriority.Urgent
                    && r.Status is ROIRequestStatus.Received or ROIRequestStatus.Acknowledged
                    or ROIRequestStatus.InProcess)
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ROI dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record SubmitROIRequestDto(
    string PatientId,
    string PatientName,
    DateTime? PatientDOB,
    ROIRequestType RequestType,
    RequesterType RequesterType,
    string RequesterName,
    string RequesterOrganization,
    string RequesterAddress,
    string RequesterPhone,
    string RequesterFax,
    string RequesterEmail,
    string PurposeOfRequest,
    List<string> RecordsRequested,
    DateTime? DateRangeStart,
    DateTime? DateRangeEnd,
    ROIRequestPriority Priority,
    string CreatedBy);

public record AssignROIStaffDto(string StaffId, string StaffName);

public record UpdateAuthorizationDto(
    AuthorizationStatus AuthorizationStatus,
    DateTime? AuthorizationDate,
    DateTime? AuthorizationExpirationDate);

public record FulfillRequestDto(
    FulfillmentMethod FulfillmentMethod,
    string Notes,
    int NumberOfPages,
    decimal FeeCharged,
    string FulfilledBy,
    string FulfilledByTitle);

public record DenyRequestDto(string DenialReason);

public record RecordDisclosureDto(
    string PatientId,
    string PatientName,
    HIPAADisclosureType DisclosureType,
    string RecipientName,
    string RecipientOrganization,
    string RecipientAddress,
    string PurposeOfDisclosure,
    string InformationDisclosed,
    string DateRangeOfInformation,
    int NumberOfPages,
    bool AuthorizationReceived,
    string LinkedRequestId,
    string DisclosedBy,
    string DisclosedByTitle);
