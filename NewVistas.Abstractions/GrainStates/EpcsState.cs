// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// NCPDP SCRIPT transaction type for e-prescribing.
/// </summary>
[GenerateSerializer]
public enum EpcsScriptTransactionType
{
    NewRx = 0,
    RefillRequest = 1,
    RefillResponse = 2,
    CancelRx = 3,
    CancelRxResponse = 4,
    RxChangeRequest = 5,
    RxChangeResponse = 6,
    RxRenewalRequest = 7,
    RxRenewalResponse = 8,
}

/// <summary>
/// E-prescription transmission status.
/// </summary>
[GenerateSerializer]
public enum EpcsTransmissionStatus
{
    Draft = 0,
    Signed = 1,
    Transmitted = 2,
    Acknowledged = 3,
    Error = 4,
    Cancelled = 5,
}

/// <summary>
/// Two-factor authentication method used for EPCS signing.
/// Per 21 CFR Part 1311.115.
/// </summary>
[GenerateSerializer]
public enum EpcsTwoFactorMethod
{
    None = 0,
    HardwareToken = 1,
    Biometric = 2,
    OneTimePassword = 3,
    SmartCard = 4,
    MobileAuthenticator = 5,
}

/// <summary>
/// NIST identity proofing assurance level — 21 CFR Part 1311.105.
/// </summary>
[GenerateSerializer]
public enum IdentityProofingLevel
{
    NotProofed = 0,
    NistLevel1 = 1,
    NistLevel2 = 2,
}

/// <summary>
/// Provider EPCS credential status.
/// </summary>
[GenerateSerializer]
public enum EpcsCredentialStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3,
    Expired = 4,
}

// ── Nested Entry Types ───────────────────────────────────────────────────────

/// <summary>
/// Digital signature record for DEA audit trail — 21 CFR Part 1311.120.
/// </summary>
[GenerateSerializer]
public class EpcsSignatureRecord
{
    /// <summary>Hash of the prescription data (SHA-256).</summary>
    [Id(0)]
    public string PrescriptionHash { get; set; } = string.Empty;

    /// <summary>X.509 certificate thumbprint used for signing.</summary>
    [Id(1)]
    public string CertificateThumbprint { get; set; } = string.Empty;

    /// <summary>Signing timestamp (UTC).</summary>
    [Id(2)]
    public DateTime SigningTime { get; set; }

    /// <summary>2FA method used at time of signing.</summary>
    [Id(3)]
    public EpcsTwoFactorMethod TwoFactorMethod { get; set; }

    /// <summary>2FA verification timestamp (UTC).</summary>
    [Id(4)]
    public DateTime TwoFactorVerificationTime { get; set; }

    /// <summary>Whether signature verification passed.</summary>
    [Id(5)]
    public bool IsValid { get; set; }
}

/// <summary>
/// Destination pharmacy for e-prescription routing.
/// </summary>
[GenerateSerializer]
public class EpcsPharmacyDestination
{
    /// <summary>Pharmacy NCPDP ID (7 digits).</summary>
    [Id(0)]
    public string NcpdpId { get; set; } = string.Empty;

    /// <summary>Pharmacy name.</summary>
    [Id(1)]
    public string PharmacyName { get; set; } = string.Empty;

    /// <summary>Pharmacy address.</summary>
    [Id(2)]
    public string? Address { get; set; }

    /// <summary>Pharmacy phone.</summary>
    [Id(3)]
    public string? Phone { get; set; }

    /// <summary>Pharmacy fax.</summary>
    [Id(4)]
    public string? Fax { get; set; }
}

/// <summary>
/// DEA audit trail entry — 21 CFR Part 1311.150.
/// </summary>
[GenerateSerializer]
public class EpcsAuditEntry
{
    [Id(0)] public DateTime Timestamp { get; set; }
    [Id(1)] public string Action { get; set; } = string.Empty;
    [Id(2)] public string UserId { get; set; } = string.Empty;
    [Id(3)] public string? Details { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for an EPCS e-prescription grain (EPCS-RX:{id}).
/// Models a single DEA-compliant electronic prescription for controlled substances.
/// Implements 21 CFR Part 1311 requirements: digital signature, 2FA, audit trail.
/// </summary>
[GenerateSerializer]
public class EpcsPrescriptionState
{
    /// <summary>Unique grain key (EPCS-RX:{guid}).</summary>
    [Id(0)]
    public string EpcsId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Link to IPharmacyGrain prescription.</summary>
    [Id(2)]
    public string? PrescriptionId { get; set; }

    // ── NCPDP SCRIPT Transaction ─────────────────────────────────────────

    /// <summary>NCPDP SCRIPT transaction type (NewRx, CancelRx, etc.).</summary>
    [Id(3)]
    public EpcsScriptTransactionType TransactionType { get; set; }

    /// <summary>Transmission status.</summary>
    [Id(4)]
    public EpcsTransmissionStatus Status { get; set; }

    // ── Drug / Prescription Data ─────────────────────────────────────────

    /// <summary>Drug name.</summary>
    [Id(5)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>NDC (National Drug Code).</summary>
    [Id(6)]
    public string? Ndc { get; set; }

    /// <summary>DEA schedule (II-V).</summary>
    [Id(7)]
    public string DeaSchedule { get; set; } = string.Empty;

    /// <summary>Quantity prescribed.</summary>
    [Id(8)]
    public decimal Quantity { get; set; }

    /// <summary>Days supply.</summary>
    [Id(9)]
    public int DaysSupply { get; set; }

    /// <summary>Number of refills authorized (0 for Schedule II).</summary>
    [Id(10)]
    public int RefillsAuthorized { get; set; }

    /// <summary>Sig / directions for use.</summary>
    [Id(11)]
    public string? Sig { get; set; }

    /// <summary>Diagnosis code (ICD-10) for the prescription.</summary>
    [Id(12)]
    public string? DiagnosisCode { get; set; }

    // ── Prescriber ───────────────────────────────────────────────────────

    /// <summary>Prescriber NPI.</summary>
    [Id(13)]
    public string? PrescriberNpi { get; set; }

    /// <summary>Prescriber DEA number.</summary>
    [Id(14)]
    public string? PrescriberDea { get; set; }

    /// <summary>Prescriber name.</summary>
    [Id(15)]
    public string? PrescriberName { get; set; }

    /// <summary>EPCS provider credential grain key.</summary>
    [Id(16)]
    public string? PrescriberCredentialId { get; set; }

    // ── Destination Pharmacy ─────────────────────────────────────────────

    /// <summary>Target pharmacy for e-prescription routing.</summary>
    [Id(17)]
    public EpcsPharmacyDestination? DestinationPharmacy { get; set; }

    // ── Digital Signature (21 CFR Part 1311.120) ─────────────────────────

    /// <summary>Digital signature record with 2FA verification.</summary>
    [Id(18)]
    public EpcsSignatureRecord? Signature { get; set; }

    // ── DEA Audit Trail (21 CFR Part 1311.150) ──────────────────────────

    /// <summary>DEA-required audit trail of all actions on this prescription.</summary>
    [Id(19)]
    public List<EpcsAuditEntry> AuditTrail { get; set; } = new();

    // ── Transmission ─────────────────────────────────────────────────────

    /// <summary>Surescripts/network message ID after transmission.</summary>
    [Id(20)]
    public string? TransmissionMessageId { get; set; }

    /// <summary>Date/time prescription was transmitted.</summary>
    [Id(21)]
    public DateTime? TransmittedDate { get; set; }

    /// <summary>Date/time acknowledgment was received.</summary>
    [Id(22)]
    public DateTime? AcknowledgedDate { get; set; }

    /// <summary>Error message if transmission failed.</summary>
    [Id(23)]
    public string? ErrorMessage { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────

    [Id(24)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(25)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persistent state for an EPCS Provider Credential grain (EPCS-PROVIDER:{providerId}).
/// Manages DEA-required identity proofing, 2FA configuration, and credential lifecycle.
/// Per 21 CFR Part 1311.105 (identity proofing) and 1311.115 (two-factor authentication).
/// </summary>
[GenerateSerializer]
public class EpcsProviderCredentialState
{
    [Id(0)] public string CredentialId { get; set; } = string.Empty;
    [Id(1)] public string ProviderId { get; set; } = string.Empty;
    [Id(2)] public string ProviderName { get; set; } = string.Empty;
    [Id(3)] public string? Npi { get; set; }
    [Id(4)] public string? DeaNumber { get; set; }

    /// <summary>Identity proofing level — NIST Level 2 required for EPCS.</summary>
    [Id(5)]
    public IdentityProofingLevel IdentityProofingLevel { get; set; }

    /// <summary>Date identity proofing was completed.</summary>
    [Id(6)]
    public DateTime? IdentityProofingDate { get; set; }

    /// <summary>Credential status.</summary>
    [Id(7)]
    public EpcsCredentialStatus CredentialStatus { get; set; }

    /// <summary>Configured 2FA methods for this provider.</summary>
    [Id(8)]
    public List<EpcsTwoFactorMethod> ConfiguredTwoFactorMethods { get; set; } = new();

    /// <summary>X.509 certificate thumbprint for digital signing.</summary>
    [Id(9)]
    public string? CertificateThumbprint { get; set; }

    /// <summary>Certificate expiration date.</summary>
    [Id(10)]
    public DateTime? CertificateExpiration { get; set; }

    /// <summary>Date credential was activated.</summary>
    [Id(11)]
    public DateTime? ActivatedDate { get; set; }

    /// <summary>Date credential was last used for signing.</summary>
    [Id(12)]
    public DateTime? LastUsedDate { get; set; }

    [Id(13)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(14)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class EpcsPrescriptionIndexEntry
{
    [Id(0)] public string EpcsId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public EpcsScriptTransactionType TransactionType { get; set; }
    [Id(3)] public EpcsTransmissionStatus Status { get; set; }
    [Id(4)] public string DrugName { get; set; } = string.Empty;
    [Id(5)] public string DeaSchedule { get; set; } = string.Empty;
    [Id(6)] public string? PrescriberName { get; set; }
    [Id(7)] public DateTime CreatedDate { get; set; }
    [Id(8)] public bool IsSigned { get; set; }
}

[GenerateSerializer]
public class EpcsPrescriptionIndexState
{
    [Id(0)] public List<EpcsPrescriptionIndexEntry> Entries { get; set; } = new();
}

[GenerateSerializer]
public class EpcsProviderIndexEntry
{
    [Id(0)] public string CredentialId { get; set; } = string.Empty;
    [Id(1)] public string ProviderId { get; set; } = string.Empty;
    [Id(2)] public string ProviderName { get; set; } = string.Empty;
    [Id(3)] public string? DeaNumber { get; set; }
    [Id(4)] public EpcsCredentialStatus CredentialStatus { get; set; }
    [Id(5)] public IdentityProofingLevel IdentityProofingLevel { get; set; }
}

[GenerateSerializer]
public class EpcsProviderIndexState
{
    [Id(0)] public List<EpcsProviderIndexEntry> Entries { get; set; } = new();
}
