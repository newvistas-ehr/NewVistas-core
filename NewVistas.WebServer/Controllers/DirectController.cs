// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Direct Project Controller — §170.315(h)(1).
///
/// Implements:
///   - Direct address management (provider/organization registration, X.509 certificates)
///   - C-CDA document generation (CCD/Referral/Discharge from patient clinical data)
///   - Secure message lifecycle (draft → sending → sent → delivered/failed)
///   - MDN tracking per §170.202(e)(1) — Delivery Notification in Direct
///   - Inbox/outbox/patient-based message filtering
/// </summary>
[ApiController]
[Route("api/direct")]
[Authorize]
public class DirectController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DirectController> _logger;

    public DirectController(IGrainFactory grainFactory, ILogger<DirectController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // ─── Address Management ───────────────────────────────────────────────────

    /// <summary>GET api/direct/addresses — List all registered Direct addresses.</summary>
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAllAddresses()
    {
        try
        {
            IDirectAddressIndexGrain index = _grainFactory.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
            List<DirectAddressSummary> addresses = await index.GetAllAddressesAsync();
            return Ok(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing Direct addresses");
            return StatusCode(500, "An error occurred listing addresses.");
        }
    }

    /// <summary>GET api/direct/addresses/active — Active addresses only.</summary>
    [HttpGet("addresses/active")]
    public async Task<IActionResult> GetActiveAddresses()
    {
        try
        {
            IDirectAddressIndexGrain index = _grainFactory.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
            List<DirectAddressSummary> addresses = await index.GetActiveAddressesAsync();
            return Ok(addresses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing active Direct addresses");
            return StatusCode(500, "An error occurred listing active addresses.");
        }
    }

    /// <summary>GET api/direct/addresses/{directAddress} — Get address details.</summary>
    [HttpGet("addresses/{directAddress}")]
    public async Task<IActionResult> GetAddress(string directAddress)
    {
        try
        {
            IDirectAddressGrain grain = _grainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{directAddress}");
            DirectAddressState address = await grain.GetAddressAsync();
            if (string.IsNullOrEmpty(address.DisplayName))
                return NotFound($"Address {directAddress} not found.");
            return Ok(address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting address {DirectAddress}", directAddress);
            return StatusCode(500, "An error occurred getting the address.");
        }
    }

    /// <summary>POST api/direct/addresses — Register a Direct address.</summary>
    [HttpPost("addresses")]
    public async Task<IActionResult> SaveAddress([FromBody] DirectAddressState address)
    {
        try
        {
            IDirectAddressGrain grain = _grainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{address.DirectAddress}");
            await grain.SaveAddressAsync(address);

            IDirectAddressIndexGrain index = _grainFactory.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
            await index.AddAddressAsync(new DirectAddressSummary
            {
                DirectAddress = address.DirectAddress,
                DisplayName = address.DisplayName,
                OwnerType = address.OwnerType,
                IsActive = address.IsActive,
                OrganizationName = address.OrganizationName,
                CertificateExpiration = address.CertificateExpiration
            });

            return Created($"api/direct/addresses/{address.DirectAddress}", address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving address {DirectAddress}", address.DirectAddress);
            return StatusCode(500, "An error occurred saving the address.");
        }
    }

    /// <summary>PUT api/direct/addresses/{directAddress}/active — Activate/deactivate.</summary>
    [HttpPut("addresses/{directAddress}/active")]
    public async Task<IActionResult> SetAddressActive(string directAddress, [FromBody] bool isActive)
    {
        try
        {
            IDirectAddressGrain grain = _grainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{directAddress}");
            await grain.SetActiveAsync(isActive);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating address {DirectAddress}", directAddress);
            return StatusCode(500, "An error occurred updating the address.");
        }
    }

    /// <summary>PUT api/direct/addresses/{directAddress}/certificate — Update X.509 certificate.</summary>
    [HttpPut("addresses/{directAddress}/certificate")]
    public async Task<IActionResult> UpdateCertificate(string directAddress, [FromBody] UpdateCertificateRequest request)
    {
        try
        {
            IDirectAddressGrain grain = _grainFactory.GetGrain<IDirectAddressGrain>($"DIRECT-ADDR:{directAddress}");
            await grain.UpdateCertificateAsync(request.Thumbprint, request.Subject, request.Expiration);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating certificate for {DirectAddress}", directAddress);
            return StatusCode(500, "An error occurred updating the certificate.");
        }
    }

    /// <summary>DELETE api/direct/addresses/{directAddress} — Remove from index.</summary>
    [HttpDelete("addresses/{directAddress}")]
    public async Task<IActionResult> RemoveAddress(string directAddress)
    {
        try
        {
            IDirectAddressIndexGrain index = _grainFactory.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
            await index.RemoveAddressAsync(directAddress);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing address {DirectAddress}", directAddress);
            return StatusCode(500, "An error occurred removing the address.");
        }
    }

    // ─── C-CDA Generation ─────────────────────────────────────────────────────

    /// <summary>
    /// GET api/direct/ccda/{patientId} — Generate a C-CDA CCD for a patient.
    /// </summary>
    [HttpGet("ccda/{patientId}")]
    [Produces("application/xml")]
    public async Task<IActionResult> GenerateCcda(
        string patientId, [FromQuery] string documentType = "CCD", CancellationToken cancellationToken = default)
    {
        try
        {
            // Skip the expensive document build if the client already disconnected.
            cancellationToken.ThrowIfCancellationRequested();

            IDirectCcdaGeneratorGrain gen = _grainFactory.GetGrain<IDirectCcdaGeneratorGrain>($"DIRECT-CCDA-GEN:{patientId}");
            string ccda = await gen.GenerateCcdAsync(documentType);
            return Content(ccda, "application/xml");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return StatusCode(499); // client closed request
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating C-CDA for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred generating the C-CDA document.");
        }
    }

    // ─── Message Management ───────────────────────────────────────────────────

    /// <summary>
    /// POST api/direct/messages/send — Create and send a Direct message with C-CDA.
    /// Auto-generates the C-CDA from the patient's clinical data.
    /// </summary>
    [HttpPost("messages/send")]
    public async Task<IActionResult> SendMessage([FromBody] SendDirectMessageRequest request)
    {
        try
        {
            // Generate C-CDA
            IDirectCcdaGeneratorGrain gen = _grainFactory.GetGrain<IDirectCcdaGeneratorGrain>(
                $"DIRECT-CCDA-GEN:{request.PatientId}");
            string ccda = await gen.GenerateCcdAsync(request.DocumentType);

            // Get patient name
            IPatientWorkflowGrain workflow = _grainFactory.GetGrain<IPatientWorkflowGrain>(request.PatientId);
            PatientState patient = await workflow.GetPatientAsync();

            // Create message
            string messageId = $"DIRECT-MSG:{Guid.NewGuid():N}";
            IDirectMessageGrain msgGrain = _grainFactory.GetGrain<IDirectMessageGrain>(messageId);
            await msgGrain.CreateMessageAsync(
                "outbound", request.FromAddress, request.ToAddress,
                request.Subject, request.PatientId, patient.Name,
                request.DocumentType, ccda, User.Identity?.Name);

            // Mark as sent (in production, this would go through SMTP/S/MIME transport)
            await msgGrain.MarkSentAsync(true, true, null, null);

            // Add to index
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            await index.AddMessageAsync(new DirectMessageSummary
            {
                MessageId = messageId,
                Direction = "outbound",
                FromAddress = request.FromAddress,
                ToAddress = request.ToAddress,
                Subject = request.Subject,
                PatientId = request.PatientId,
                DocumentType = request.DocumentType,
                Status = "sent",
                CreatedDate = DateTime.UtcNow,
                MdnStatus = "none"
            });

            return Created($"api/direct/messages/{messageId}", new { messageId, status = "sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending Direct message for patient {PatientId}", request.PatientId);
            return StatusCode(500, "An error occurred sending the message.");
        }
    }

    /// <summary>
    /// POST api/direct/messages/receive — Record an inbound Direct message.
    /// </summary>
    [HttpPost("messages/receive")]
    public async Task<IActionResult> ReceiveMessage([FromBody] ReceiveDirectMessageRequest request)
    {
        try
        {
            string messageId = $"DIRECT-MSG:{Guid.NewGuid():N}";
            IDirectMessageGrain msgGrain = _grainFactory.GetGrain<IDirectMessageGrain>(messageId);
            await msgGrain.CreateMessageAsync(
                "inbound", request.FromAddress, request.ToAddress,
                request.Subject, request.PatientId, null,
                request.DocumentType, request.CcdaContent, null);

            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            await index.AddMessageAsync(new DirectMessageSummary
            {
                MessageId = messageId,
                Direction = "inbound",
                FromAddress = request.FromAddress,
                ToAddress = request.ToAddress,
                Subject = request.Subject,
                PatientId = request.PatientId,
                DocumentType = request.DocumentType,
                Status = "received",
                CreatedDate = DateTime.UtcNow,
                MdnStatus = "none"
            });

            return Created($"api/direct/messages/{messageId}", new { messageId, status = "received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving Direct message");
            return StatusCode(500, "An error occurred receiving the message.");
        }
    }

    /// <summary>GET api/direct/messages — List all messages.</summary>
    [HttpGet("messages")]
    public async Task<IActionResult> GetAllMessages()
    {
        try
        {
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> messages = await index.GetAllMessagesAsync();
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing messages");
            return StatusCode(500, "An error occurred listing messages.");
        }
    }

    /// <summary>GET api/direct/messages/outbox — Outbound messages.</summary>
    [HttpGet("messages/outbox")]
    public async Task<IActionResult> GetOutbox()
    {
        try
        {
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> messages = await index.GetOutboundMessagesAsync();
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing outbound messages");
            return StatusCode(500, "An error occurred listing messages.");
        }
    }

    /// <summary>GET api/direct/messages/inbox — Inbound messages.</summary>
    [HttpGet("messages/inbox")]
    public async Task<IActionResult> GetInbox()
    {
        try
        {
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> messages = await index.GetInboundMessagesAsync();
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing inbound messages");
            return StatusCode(500, "An error occurred listing messages.");
        }
    }

    /// <summary>GET api/direct/messages/pending — Messages awaiting delivery confirmation.</summary>
    [HttpGet("messages/pending")]
    public async Task<IActionResult> GetPending()
    {
        try
        {
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> messages = await index.GetPendingDeliveryAsync();
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pending messages");
            return StatusCode(500, "An error occurred listing messages.");
        }
    }

    /// <summary>GET api/direct/messages/patient/{patientId} — Messages for a patient.</summary>
    [HttpGet("messages/patient/{patientId}")]
    public async Task<IActionResult> GetMessagesByPatient(string patientId)
    {
        try
        {
            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> messages = await index.GetMessagesByPatientAsync(patientId);
            return Ok(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing messages for patient {PatientId}", patientId);
            return StatusCode(500, "An error occurred listing messages.");
        }
    }

    /// <summary>GET api/direct/messages/{messageId} — Get full message details.</summary>
    [HttpGet("messages/{messageId}")]
    public async Task<IActionResult> GetMessage(string messageId)
    {
        try
        {
            IDirectMessageGrain grain = _grainFactory.GetGrain<IDirectMessageGrain>(messageId);
            DirectMessageState msg = await grain.GetMessageAsync();
            if (string.IsNullOrEmpty(msg.FromAddress))
                return NotFound($"Message {messageId} not found.");
            return Ok(msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message {MessageId}", messageId);
            return StatusCode(500, "An error occurred getting the message.");
        }
    }

    /// <summary>GET api/direct/messages/{messageId}/ccda — Get the C-CDA attachment.</summary>
    [HttpGet("messages/{messageId}/ccda")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetMessageCcda(string messageId)
    {
        try
        {
            IDirectMessageGrain grain = _grainFactory.GetGrain<IDirectMessageGrain>(messageId);
            DirectMessageState msg = await grain.GetMessageAsync();
            if (string.IsNullOrEmpty(msg.CcdaContent))
                return NotFound("No C-CDA document attached.");
            return Content(msg.CcdaContent, "application/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting C-CDA for message {MessageId}", messageId);
            return StatusCode(500, "An error occurred getting the C-CDA document.");
        }
    }

    /// <summary>
    /// POST api/direct/messages/{messageId}/mdn — Record MDN (Delivery Notification).
    /// §170.202(e)(1) — Delivery Notification in Direct.
    /// </summary>
    [HttpPost("messages/{messageId}/mdn")]
    public async Task<IActionResult> RecordMdn(string messageId, [FromBody] DirectMdnRequest request)
    {
        try
        {
            IDirectMessageGrain grain = _grainFactory.GetGrain<IDirectMessageGrain>(messageId);
            await grain.RecordMdnAsync(request.MdnStatus, request.MdnContent);

            IDirectMessageIndexGrain index = _grainFactory.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            await index.UpdateMdnStatusAsync(messageId, request.MdnStatus);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording MDN for message {MessageId}", messageId);
            return StatusCode(500, "An error occurred recording the MDN.");
        }
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public record SendDirectMessageRequest(
    string FromAddress,
    string ToAddress,
    string Subject,
    string PatientId,
    string DocumentType = "CCD");

public record ReceiveDirectMessageRequest(
    string FromAddress,
    string ToAddress,
    string Subject,
    string PatientId,
    string DocumentType,
    string CcdaContent);

public record UpdateCertificateRequest(
    string Thumbprint,
    string Subject,
    DateTime Expiration);

public record DirectMdnRequest(
    string MdnStatus,
    string? MdnContent);
