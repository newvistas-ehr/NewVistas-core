// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// REST API for Patient Advocate: complaint tracking and Congressional inquiries.
/// VistA File #745 (PATIENT REPRESENTATIVE). PATREPE.m
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PatientAdvocateController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PatientAdvocateController> _logger;

    public PatientAdvocateController(IGrainFactory grainFactory, ILogger<PatientAdvocateController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IComplaintGrain Complaint(string id)
        => _grainFactory.GetGrain<IComplaintGrain>(Uri.UnescapeDataString(id));

    private IComplaintIndexGrain ComplaintIndex()
        => _grainFactory.GetGrain<IComplaintIndexGrain>("PA-COMPLAINT-IDX");

    private ICongressionalInquiryGrain Inquiry(string id)
        => _grainFactory.GetGrain<ICongressionalInquiryGrain>(Uri.UnescapeDataString(id));

    private ICongressionalInquiryIndexGrain InquiryIndex()
        => _grainFactory.GetGrain<ICongressionalInquiryIndexGrain>("PA-CONGRESS-IDX");

    private static ComplaintIndexEntry BuildComplaintIndex(ComplaintState s) => new()
    {
        ComplaintId = s.ComplaintId,
        PatientId = s.PatientId,
        PatientName = s.PatientName,
        ReceivedDate = s.ReceivedDate,
        ComplaintType = s.ComplaintType,
        Category = s.Category,
        Priority = s.Priority,
        Status = s.Status,
        AssignedAdvocateName = s.AssignedAdvocateName,
        ResponseDue = s.ResponseDue
    };

    private static CongressionalInquiryIndexEntry BuildInquiryIndex(CongressionalInquiryState s)
    {
        DateTime now = DateTime.UtcNow;
        bool pendingStatus = s.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
            or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted;
        return new()
        {
            InquiryId = s.InquiryId,
            PatientId = s.PatientId,
            PatientName = s.PatientName,
            ReceivedDate = s.ReceivedDate,
            InquiryType = s.InquiryType,
            CongressionalOfficeName = s.CongressionalOfficeName,
            Status = s.Status,
            AcknowledgmentDue = s.AcknowledgmentDue,
            ResponseDue = s.ResponseDue,
            IsAcknowledgmentOverdue = s.AcknowledgmentDate is null && s.AcknowledgmentDue < now && pendingStatus,
            IsResponseOverdue = s.ResponseDue < now && pendingStatus
        };
    }

    // ── Complaints ────────────────────────────────────────────────────────────

    [HttpGet("complaints")]
    public async Task<IActionResult> GetAllComplaints()
    {
        try
        {
            return Ok(await ComplaintIndex().GetAllComplaintsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all complaints");
            return StatusCode(500, "Error retrieving complaints.");
        }
    }

    [HttpGet("complaints/status/{status}")]
    public async Task<IActionResult> GetComplaintsByStatus(ComplaintStatus status)
    {
        try
        {
            return Ok(await ComplaintIndex().GetComplaintsByStatusAsync(status));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving complaints by status {Status}", status);
            return StatusCode(500, "Error retrieving complaints.");
        }
    }

    [HttpGet("complaints/patient/{patientId}")]
    public async Task<IActionResult> GetComplaintsByPatient(string patientId)
    {
        try
        {
            string id = Uri.UnescapeDataString(patientId.Trim());
            return Ok(await ComplaintIndex().GetComplaintsByPatientAsync(id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving complaints for patient {PatientId}", patientId);
            return StatusCode(500, "Error retrieving complaints.");
        }
    }

    [HttpGet("complaints/overdue")]
    public async Task<IActionResult> GetOverdueComplaints()
    {
        try
        {
            return Ok(await ComplaintIndex().GetOverdueComplaintsAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue complaints");
            return StatusCode(500, "Error retrieving overdue complaints.");
        }
    }

    [HttpGet("complaints/{complaintId}")]
    public async Task<IActionResult> GetComplaint(string complaintId)
    {
        try
        {
            return Ok(await Complaint(complaintId).GetComplaintAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving complaint {ComplaintId}", complaintId);
            return StatusCode(500, "Error retrieving complaint.");
        }
    }

    [HttpPost("complaints")]
    public async Task<IActionResult> FileComplaint([FromBody] FileComplaintRequest request)
    {
        try
        {
            string complaintId = $"PA-COMPLAINT:{Guid.NewGuid()}";
            await Complaint(complaintId).FileComplaintAsync(
                request.PatientId, request.PatientName, request.ComplaintDate,
                request.ComplaintType, request.Category, request.Priority,
                request.Source, request.NarrativeDescription, request.SpecificConcern,
                request.DepartmentInvolved, request.ReporterName, request.ReporterRelationship,
                request.IsConfidential, request.CreatedBy);
            ComplaintState state = await Complaint(complaintId).GetComplaintAsync();
            await ComplaintIndex().UpsertComplaintAsync(BuildComplaintIndex(state));
            return Created($"/api/patientadvocate/complaints/{Uri.EscapeDataString(complaintId)}", new { complaintId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error filing complaint for patient {PatientId}", request.PatientId);
            return StatusCode(500, "Error filing complaint.");
        }
    }

    [HttpPost("complaints/{complaintId}/assign")]
    public async Task<IActionResult> AssignAdvocate(string complaintId, [FromBody] AssignAdvocateRequest request)
    {
        try
        {
            await Complaint(complaintId).AssignAdvocateAsync(request.AdvocateId, request.AdvocateName);
            ComplaintState state = await Complaint(complaintId).GetComplaintAsync();
            await ComplaintIndex().UpsertComplaintAsync(BuildComplaintIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning advocate to complaint {ComplaintId}", complaintId);
            return StatusCode(500, "Error assigning advocate.");
        }
    }

    [HttpPost("complaints/{complaintId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeComplaint(string complaintId)
    {
        try
        {
            await Complaint(complaintId).AcknowledgeComplaintAsync(DateTime.UtcNow);
            ComplaintState state = await Complaint(complaintId).GetComplaintAsync();
            await ComplaintIndex().UpsertComplaintAsync(BuildComplaintIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging complaint {ComplaintId}", complaintId);
            return StatusCode(500, "Error acknowledging complaint.");
        }
    }

    [HttpPost("complaints/{complaintId}/resolve")]
    public async Task<IActionResult> ResolveComplaint(string complaintId, [FromBody] ResolveComplaintRequest request)
    {
        try
        {
            await Complaint(complaintId).ResolveComplaintAsync(request.Outcome, request.ResolutionSummary);
            ComplaintState state = await Complaint(complaintId).GetComplaintAsync();
            await ComplaintIndex().UpsertComplaintAsync(BuildComplaintIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving complaint {ComplaintId}", complaintId);
            return StatusCode(500, "Error resolving complaint.");
        }
    }

    [HttpPost("complaints/{complaintId}/close")]
    public async Task<IActionResult> CloseComplaint(string complaintId)
    {
        try
        {
            await Complaint(complaintId).CloseComplaintAsync();
            ComplaintState state = await Complaint(complaintId).GetComplaintAsync();
            await ComplaintIndex().UpsertComplaintAsync(BuildComplaintIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing complaint {ComplaintId}", complaintId);
            return StatusCode(500, "Error closing complaint.");
        }
    }

    // ── Congressional Inquiries ───────────────────────────────────────────────

    [HttpGet("congressional")]
    public async Task<IActionResult> GetAllInquiries()
    {
        try
        {
            return Ok(await InquiryIndex().GetAllInquiriesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all congressional inquiries");
            return StatusCode(500, "Error retrieving inquiries.");
        }
    }

    [HttpGet("congressional/pending")]
    public async Task<IActionResult> GetPendingInquiries()
    {
        try
        {
            return Ok(await InquiryIndex().GetPendingInquiriesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending congressional inquiries");
            return StatusCode(500, "Error retrieving pending inquiries.");
        }
    }

    [HttpGet("congressional/overdue")]
    public async Task<IActionResult> GetOverdueInquiries()
    {
        try
        {
            return Ok(await InquiryIndex().GetOverdueInquiriesAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue congressional inquiries");
            return StatusCode(500, "Error retrieving overdue inquiries.");
        }
    }

    [HttpGet("congressional/{inquiryId}")]
    public async Task<IActionResult> GetInquiry(string inquiryId)
    {
        try
        {
            return Ok(await Inquiry(inquiryId).GetInquiryAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving congressional inquiry {InquiryId}", inquiryId);
            return StatusCode(500, "Error retrieving inquiry.");
        }
    }

    [HttpPost("congressional")]
    public async Task<IActionResult> CreateInquiry([FromBody] CreateInquiryRequest request)
    {
        try
        {
            string inquiryId = $"PA-CONGRESS:{Guid.NewGuid()}";
            await Inquiry(inquiryId).CreateInquiryAsync(
                request.PatientId, request.PatientName, request.InquiryType,
                request.CongressionalOfficeName, request.CongressionalContactName,
                request.CongressionalPhone, request.CongressionalEmail,
                request.Subject, request.InquiryText,
                request.LinkedComplaintId, request.CreatedBy);
            CongressionalInquiryState state = await Inquiry(inquiryId).GetInquiryAsync();
            await InquiryIndex().UpsertInquiryAsync(BuildInquiryIndex(state));
            return Created($"/api/patientadvocate/congressional/{Uri.EscapeDataString(inquiryId)}", new { inquiryId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating congressional inquiry for patient {PatientId}", request.PatientId);
            return StatusCode(500, "Error creating inquiry.");
        }
    }

    [HttpPost("congressional/{inquiryId}/assign")]
    public async Task<IActionResult> AssignInquiryHandler(string inquiryId, [FromBody] AssignHandlerRequest request)
    {
        try
        {
            await Inquiry(inquiryId).AssignHandlerAsync(request.HandlerId, request.HandlerName);
            CongressionalInquiryState state = await Inquiry(inquiryId).GetInquiryAsync();
            await InquiryIndex().UpsertInquiryAsync(BuildInquiryIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning handler to inquiry {InquiryId}", inquiryId);
            return StatusCode(500, "Error assigning handler.");
        }
    }

    [HttpPost("congressional/{inquiryId}/acknowledge")]
    public async Task<IActionResult> AcknowledgeInquiry(string inquiryId)
    {
        try
        {
            await Inquiry(inquiryId).AcknowledgeInquiryAsync(DateTime.UtcNow);
            CongressionalInquiryState state = await Inquiry(inquiryId).GetInquiryAsync();
            await InquiryIndex().UpsertInquiryAsync(BuildInquiryIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging inquiry {InquiryId}", inquiryId);
            return StatusCode(500, "Error acknowledging inquiry.");
        }
    }

    [HttpPost("congressional/{inquiryId}/complete")]
    public async Task<IActionResult> CompleteInquiry(string inquiryId, [FromBody] CompleteInquiryRequest request)
    {
        try
        {
            await Inquiry(inquiryId).CompleteInquiryAsync(request.FinalResponseText, request.Outcome);
            CongressionalInquiryState state = await Inquiry(inquiryId).GetInquiryAsync();
            await InquiryIndex().UpsertInquiryAsync(BuildInquiryIndex(state));
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing inquiry {InquiryId}", inquiryId);
            return StatusCode(500, "Error completing inquiry.");
        }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            List<ComplaintIndexEntry> complaints = await ComplaintIndex().GetAllComplaintsAsync();
            List<CongressionalInquiryIndexEntry> inquiries = await InquiryIndex().GetAllInquiriesAsync();
            DateTime now = DateTime.UtcNow;

            var dashboard = new
            {
                TotalComplaints = complaints.Count,
                OpenComplaints = complaints.Count(c => c.Status is ComplaintStatus.Received
                    or ComplaintStatus.Acknowledged or ComplaintStatus.UnderInvestigation
                    or ComplaintStatus.ResponseDrafted or ComplaintStatus.Escalated),
                OverdueComplaints = complaints.Count(c => c.ResponseDue < now
                    && c.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
                    or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted
                    or ComplaintStatus.Escalated),
                TotalCongressional = inquiries.Count,
                PendingCongressional = inquiries.Count(i => i.Status is ComplaintStatus.Received
                    or ComplaintStatus.Acknowledged or ComplaintStatus.UnderInvestigation
                    or ComplaintStatus.ResponseDrafted),
                OverdueCongressional = inquiries.Count(i => i.ResponseDue < now
                    && i.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
                    or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted),
                AcknowledgmentOverdue = inquiries.Count(i => i.IsAcknowledgmentOverdue
                    && i.Status is ComplaintStatus.Received)
            };
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Patient Advocate dashboard");
            return StatusCode(500, "Error generating dashboard.");
        }
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record FileComplaintRequest(
    string PatientId,
    string PatientName,
    DateTime ComplaintDate,
    ComplaintType ComplaintType,
    ComplaintCategory Category,
    ComplaintPriority Priority,
    InquirySource Source,
    string NarrativeDescription,
    string SpecificConcern,
    string DepartmentInvolved,
    string ReporterName,
    string ReporterRelationship,
    bool IsConfidential,
    string CreatedBy);

public record AssignAdvocateRequest(string AdvocateId, string AdvocateName);

public record ResolveComplaintRequest(ResolutionOutcome Outcome, string ResolutionSummary);

public record CreateInquiryRequest(
    string PatientId,
    string PatientName,
    CongressionalInquiryType InquiryType,
    string CongressionalOfficeName,
    string CongressionalContactName,
    string CongressionalPhone,
    string CongressionalEmail,
    string Subject,
    string InquiryText,
    string LinkedComplaintId,
    string CreatedBy);

public record AssignHandlerRequest(string HandlerId, string HandlerName);

public record CompleteInquiryRequest(string FinalResponseText, ResolutionOutcome Outcome);
