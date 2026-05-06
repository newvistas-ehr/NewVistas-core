// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Aggregate debtor record tracking a patient's overall AR balance summary
/// (VistA File #340 AR DEBTOR). Managed by RCDP*.m MUMPS routines.
/// </summary>
[GenerateSerializer]
public class ARDebtorState
{
    /// <summary>Debtor identifier (same as patient ID for individual veteran debtors).</summary>
    [Id(0)] public string DebtorId { get; set; } = string.Empty;

    /// <summary>Patient identifier (links to PatientGrain).</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Debtor full name (denormalized from patient record).</summary>
    [Id(2)] public string Name { get; set; } = string.Empty;

    /// <summary>Mailing address for AR statements.</summary>
    [Id(3)] public string? Address { get; set; }

    /// <summary>Contact telephone number.</summary>
    [Id(4)] public string? Phone { get; set; }

    /// <summary>Sum of original amounts across all AR accounts.</summary>
    [Id(5)] public decimal TotalAmountOwed { get; set; }

    /// <summary>Total amount paid across all AR accounts.</summary>
    [Id(6)] public decimal TotalAmountPaid { get; set; }

    /// <summary>Current outstanding balance (TotalAmountOwed − TotalAmountPaid − Waivers).</summary>
    [Id(7)] public decimal CurrentBalance { get; set; }

    /// <summary>Whether the debtor account is currently delinquent.</summary>
    [Id(8)] public bool IsDelinquent { get; set; }

    /// <summary>Whether the debtor has been referred to a collection agency.</summary>
    [Id(9)] public bool IsInCollection { get; set; }

    /// <summary>Whether the entire debtor balance has been waived.</summary>
    [Id(10)] public bool IsWaived { get; set; }

    /// <summary>Date of the most recent activity on any AR account.</summary>
    [Id(11)] public DateTime? LastActivityDate { get; set; }

    /// <summary>Date the debtor was first flagged delinquent.</summary>
    [Id(12)] public DateTime? DelinquentSince { get; set; }

    /// <summary>Date the debtor was referred to a collection agency.</summary>
    [Id(13)] public DateTime? CollectionReferralDate { get; set; }

    /// <summary>Free-text notes about this debtor.</summary>
    [Id(14)] public string? Notes { get; set; }

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(15)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(16)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
