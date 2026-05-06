// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Fee service type ─────────────────────────────────────────────────────────

/// <summary>Type of community care service authorized under a fee basis authorization.</summary>
[GenerateSerializer]
public enum FeeServiceType
{
    /// <summary>Outpatient medical care visit.</summary>
    Outpatient = 0,

    /// <summary>Inpatient hospital admission.</summary>
    Inpatient = 1,

    /// <summary>Dental care services.</summary>
    Dental = 2,

    /// <summary>Pharmacy / prescription services.</summary>
    Pharmacy = 3,

    /// <summary>Mental health / behavioral health services.</summary>
    Mental = 4,

    /// <summary>Optometry / vision care services.</summary>
    Optometry = 5,

    /// <summary>Other community care service type.</summary>
    Other = 6,
}

// ─── Fee authorization status ─────────────────────────────────────────────────

/// <summary>Lifecycle status of a fee basis authorization.</summary>
[GenerateSerializer]
public enum FeeAuthorizationStatus
{
    /// <summary>Authorization submitted but not yet reviewed.</summary>
    Pending = 0,

    /// <summary>Authorization is approved and active for service delivery.</summary>
    Active = 1,

    /// <summary>Authorization temporarily suspended.</summary>
    Suspended = 2,

    /// <summary>Authorization has passed its expiration date.</summary>
    Expired = 3,

    /// <summary>Authorization cancelled by staff before services completed.</summary>
    Cancelled = 4,

    /// <summary>Authorized dollar amount fully consumed by paid invoices.</summary>
    Exhausted = 5,
}

// ─── Fee Authorization — VistA File #162.6 FEE BASIS AUTHORIZATION ────────────

/// <summary>
/// An authorization approving a patient to receive specified community care services
/// from a designated vendor up to a stated dollar limit (VistA File #162.6).
/// Managed by FBSVBR.m, FBAUTH.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class FeeAuthorizationState
{
    /// <summary>Unique identifier for this authorization (Guid).</summary>
    [Id(0)] public string AuthorizationId { get; set; } = string.Empty;

    /// <summary>Patient for whom care is authorized.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Vendor (community care provider) authorized to deliver services.</summary>
    [Id(2)] public string VendorId { get; set; } = string.Empty;

    /// <summary>Vendor display name at the time of authorization.</summary>
    [Id(3)] public string VendorName { get; set; } = string.Empty;

    /// <summary>Type of community care service authorized.</summary>
    [Id(4)] public FeeServiceType ServiceType { get; set; }

    /// <summary>VA-assigned authorization number (optional; may be auto-generated).</summary>
    [Id(5)] public string? AuthorizationNumber { get; set; }

    /// <summary>Current lifecycle status of this authorization.</summary>
    [Id(6)] public FeeAuthorizationStatus Status { get; set; } = FeeAuthorizationStatus.Active;

    /// <summary>User ID of the VA staff member who authorized services.</summary>
    [Id(7)] public string AuthorizedByUserId { get; set; } = string.Empty;

    /// <summary>Display name of the VA staff member who authorized services.</summary>
    [Id(8)] public string AuthorizedByUserName { get; set; } = string.Empty;

    /// <summary>Date the authorization was issued.</summary>
    [Id(9)] public DateTime AuthorizationDate { get; set; }

    /// <summary>Date from which services may begin.</summary>
    [Id(10)] public DateTime EffectiveDate { get; set; }

    /// <summary>Date after which no new services may be rendered (optional).</summary>
    [Id(11)] public DateTime? ExpirationDate { get; set; }

    /// <summary>Maximum dollar amount approved for payment under this authorization.</summary>
    [Id(12)] public decimal AuthorizedAmount { get; set; }

    /// <summary>Total of all paid invoice amounts applied against this authorization.</summary>
    [Id(13)] public decimal SpentAmount { get; set; }

    /// <summary>Remaining available balance (AuthorizedAmount − SpentAmount).</summary>
    [Id(14)] public decimal RemainingAmount { get; set; }

    /// <summary>Description of the authorized service or treatment.</summary>
    [Id(15)] public string ServiceDescription { get; set; } = string.Empty;

    /// <summary>Primary ICD-10 diagnosis code justifying community care (optional).</summary>
    [Id(16)] public string? DiagnosisCode { get; set; }

    /// <summary>Maximum number of visits authorized under this approval (optional).</summary>
    [Id(17)] public int? MaxVisits { get; set; }

    /// <summary>Number of visits consumed so far (incremented when invoices are paid).</summary>
    [Id(18)] public int VisitsUsed { get; set; }

    /// <summary>IDs of all FeeInvoiceGrain records linked to this authorization.</summary>
    [Id(19)] public List<string> InvoiceIds { get; set; } = new();

    /// <summary>Free-text notes about this authorization.</summary>
    [Id(20)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(21)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(22)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
