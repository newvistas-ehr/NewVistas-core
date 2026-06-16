// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a Direct address registration.
/// §170.315(h)(1) — Direct Project.
///
/// Represents a provider or organization's Direct address (e.g., dr.smith@direct.newvistas.health)
/// with associated X.509 certificate for S/MIME encryption and signing.
///
/// Grain Key: "DIRECT-ADDR:{directAddress}"
/// </summary>
[GenerateSerializer]
public class DirectAddressState
{
    /// <summary>Direct address (e.g., "dr.smith@direct.newvistas.health").</summary>
    [Id(0)]
    public string DirectAddress { get; set; } = string.Empty;

    /// <summary>Display name (e.g., "Dr. John Smith").</summary>
    [Id(1)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Owner type: "provider", "organization", "department".</summary>
    [Id(2)]
    public string OwnerType { get; set; } = "provider";

    /// <summary>NewVistas user ID (if provider-owned).</summary>
    [Id(3)]
    public string? OwnerId { get; set; }

    /// <summary>Organization/facility name.</summary>
    [Id(4)]
    public string? OrganizationName { get; set; }

    /// <summary>X.509 certificate thumbprint for S/MIME signing.</summary>
    [Id(5)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>X.509 certificate subject (CN).</summary>
    [Id(6)]
    public string? CertificateSubject { get; set; }

    /// <summary>Certificate expiration date.</summary>
    [Id(7)]
    public DateTime? CertificateExpiration { get; set; }

    /// <summary>Whether this address is active and can send/receive.</summary>
    [Id(8)]
    public bool IsActive { get; set; } = true;

    /// <summary>HISP domain for this address.</summary>
    [Id(9)]
    public string HispDomain { get; set; } = string.Empty;

    [Id(10)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(11)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry for Direct address listing.
/// </summary>
[GenerateSerializer]
public class DirectAddressSummary
{
    [Id(0)]
    public string DirectAddress { get; set; } = string.Empty;

    [Id(1)]
    public string DisplayName { get; set; } = string.Empty;

    [Id(2)]
    public string OwnerType { get; set; } = string.Empty;

    [Id(3)]
    public bool IsActive { get; set; }

    [Id(4)]
    public string? OrganizationName { get; set; }

    [Id(5)]
    public DateTime? CertificateExpiration { get; set; }
}

/// <summary>
/// Index state for Direct address registry.
/// Grain Key: "DIRECT-ADDR-INDEX"
/// </summary>
[GenerateSerializer]
public class DirectAddressIndexState
{
    [Id(0)]
    public List<DirectAddressSummary> Addresses { get; set; } = new();
}

/// <summary>
/// State for a Direct message (C-CDA exchange).
/// §170.315(h)(1) — Applicability Statement for Secure Health Transport (§170.202(a)(2)).
///
/// Lifecycle: draft → sending → sent → delivered / failed
/// MDN tracking per §170.202(e)(1) — Delivery Notification in Direct.
///
/// Grain Key: "DIRECT-MSG:{messageId}"
/// </summary>
[GenerateSerializer]
public class DirectMessageState
{
    /// <summary>Unique message identifier.</summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>Direction: "outbound" or "inbound".</summary>
    [Id(1)]
    public string Direction { get; set; } = "outbound";

    /// <summary>Sender Direct address.</summary>
    [Id(2)]
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Recipient Direct address.</summary>
    [Id(3)]
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>Message subject line.</summary>
    [Id(4)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Patient ID this message relates to.</summary>
    [Id(5)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name (denormalized).</summary>
    [Id(6)]
    public string? PatientName { get; set; }

    /// <summary>Document type: "CCD", "Referral", "Discharge", "Consult", "Lab".</summary>
    [Id(7)]
    public string DocumentType { get; set; } = "CCD";

    /// <summary>The C-CDA XML document content.</summary>
    [Id(8)]
    public string CcdaContent { get; set; } = string.Empty;

    /// <summary>
    /// Message status: "draft", "sending", "sent", "delivered", "failed", "received".
    /// </summary>
    [Id(9)]
    public string Status { get; set; } = "draft";

    /// <summary>When the message was created.</summary>
    [Id(10)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the message was sent.</summary>
    [Id(11)]
    public DateTime? SentDate { get; set; }

    /// <summary>When delivery was confirmed (MDN received).</summary>
    [Id(12)]
    public DateTime? DeliveredDate { get; set; }

    /// <summary>Failure reason if status is "failed".</summary>
    [Id(13)]
    public string? FailureReason { get; set; }

    // ─── S/MIME Security (§170.202(a)(2)) ─────────────────────────────────────

    /// <summary>Whether the message was S/MIME encrypted.</summary>
    [Id(14)]
    public bool IsEncrypted { get; set; }

    /// <summary>Whether the message was S/MIME signed.</summary>
    [Id(15)]
    public bool IsSigned { get; set; }

    /// <summary>Signing certificate thumbprint.</summary>
    [Id(16)]
    public string? SigningCertThumbprint { get; set; }

    /// <summary>Encryption certificate thumbprint (recipient's).</summary>
    [Id(17)]
    public string? EncryptionCertThumbprint { get; set; }

    // ─── MDN Tracking (§170.202(e)(1)) ────────────────────────────────────────

    /// <summary>
    /// MDN (Message Disposition Notification) status:
    /// "none", "dispatched", "processed", "displayed", "deleted", "denied", "failed".
    /// Per §170.202(e)(1) — Delivery Notification in Direct.
    /// </summary>
    [Id(18)]
    public string MdnStatus { get; set; } = "none";

    /// <summary>Raw MDN response content.</summary>
    [Id(19)]
    public string? MdnContent { get; set; }

    /// <summary>When the MDN was received.</summary>
    [Id(20)]
    public DateTime? MdnDate { get; set; }

    /// <summary>User who initiated the send (for outbound).</summary>
    [Id(21)]
    public string? SentBy { get; set; }

    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry for message listing.
/// </summary>
[GenerateSerializer]
public class DirectMessageSummary
{
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    [Id(1)]
    public string Direction { get; set; } = string.Empty;

    [Id(2)]
    public string FromAddress { get; set; } = string.Empty;

    [Id(3)]
    public string ToAddress { get; set; } = string.Empty;

    [Id(4)]
    public string Subject { get; set; } = string.Empty;

    [Id(5)]
    public string PatientId { get; set; } = string.Empty;

    [Id(6)]
    public string DocumentType { get; set; } = string.Empty;

    [Id(7)]
    public string Status { get; set; } = string.Empty;

    [Id(8)]
    public DateTime CreatedDate { get; set; }

    [Id(9)]
    public string MdnStatus { get; set; } = string.Empty;
}

/// <summary>
/// Index state for Direct messages.
/// Grain Key: "DIRECT-MSG-INDEX"
/// </summary>
[GenerateSerializer]
public class DirectMessageIndexState
{
    [Id(0)]
    public List<DirectMessageSummary> Messages { get; set; } = new();
}
