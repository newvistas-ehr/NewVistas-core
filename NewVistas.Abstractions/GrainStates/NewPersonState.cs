// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the NEW PERSON grain — staff/provider directory (VistA File #200).
///
/// This grain is the source of truth for provider/staff identity referenced by
/// all clinical grains. It does NOT store authentication credentials (owned by
/// ASP.NET Core Identity) or role assignments (owned by ASP.NET Core authorization).
///
/// VistA File #200 field references included in comments.
/// </summary>
[GenerateSerializer]
public class NewPersonState
{
    /// <summary>
    /// User ID — maps to ASP.NET Core Identity user ID.
    /// Grain key: "USER:{UserId}".
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Name in VistA format: LAST,FIRST MIDDLE.
    /// File #200, Field .01 (NAME).
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Professional title (e.g., "Staff Physician", "Nurse Practitioner").
    /// File #200, Field 8 (TITLE).
    /// </summary>
    [Id(2)]
    public string? Title { get; set; }

    /// <summary>
    /// Degree abbreviation (e.g., "MD", "DO", "RN", "PharmD").
    /// File #200, Field 10.6 (DEGREE).
    /// </summary>
    [Id(3)]
    public string? Degree { get; set; }

    /// <summary>
    /// Service/Section assignment (e.g., "MEDICINE", "SURGERY", "PHARMACY").
    /// File #200, Field 29 (SERVICE/SECTION).
    /// </summary>
    [Id(4)]
    public string? ServiceSection { get; set; }

    /// <summary>
    /// User class from File #8930 (e.g., "PHYSICIAN", "NURSE", "PHARMACIST", "CLERK").
    /// Determines what clinical actions the person can perform.
    /// File #200, Field 9.5 (USER CLASS).
    /// </summary>
    [Id(5)]
    public string? UserClass { get; set; }

    /// <summary>
    /// Whether this person record is active.
    /// Inactive users cannot log in or perform clinical actions.
    /// Derived from File #200, Field .204 (DISUSER) and TERMINATION DATE.
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Associated institution/facility (e.g., "BOSTON VAMC").
    /// File #200, Field 13 (INSTITUTION FILE POINTER → File #4).
    /// </summary>
    [Id(7)]
    public string? InstitutionId { get; set; }

    /// <summary>
    /// Display name of the institution.
    /// </summary>
    [Id(8)]
    public string? InstitutionName { get; set; }

    /// <summary>
    /// Division within the institution.
    /// File #200, Field 16 (DIVISION → File #4.2).
    /// </summary>
    [Id(9)]
    public string? DivisionId { get; set; }

    /// <summary>
    /// Display name of the division.
    /// </summary>
    [Id(10)]
    public string? DivisionName { get; set; }

    /// <summary>
    /// Provider type (e.g., "STAFF", "RESIDENT", "FEE BASIS", "C&A").
    /// File #200, Field 53.5 (PROVIDER TYPE).
    /// </summary>
    [Id(11)]
    public string? ProviderType { get; set; }

    /// <summary>
    /// Medical specialty (e.g., "INTERNAL MEDICINE", "CARDIOLOGY").
    /// File #200, Field 53 (SPECIALTY).
    /// </summary>
    [Id(12)]
    public string? Specialty { get; set; }

    /// <summary>
    /// Hashed electronic signature code. The raw code is NEVER stored.
    /// File #200, Field .165 (ELECTRONIC SIGNATURE CODE).
    /// Set by the user, never visible to admins. Admins can only clear/reset.
    /// </summary>
    [Id(13)]
    public string? ElectronicSignatureHash { get; set; }

    /// <summary>
    /// Date/time the electronic signature was last set or changed.
    /// </summary>
    [Id(14)]
    public DateTime? ElectronicSignatureSetDate { get; set; }

    /// <summary>
    /// Termination/separation date. When set with IsActive=false, the user is terminated.
    /// File #200, Field 9.2 (TERMINATION DATE).
    /// </summary>
    [Id(15)]
    public DateTime? TerminationDate { get; set; }

    /// <summary>
    /// Date this record was created.
    /// </summary>
    [Id(16)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date this record was last modified.
    /// </summary>
    [Id(17)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ─── Multi-Factor Authentication (§170.315(d)(13)) ───────────────

    /// <summary>
    /// Whether MFA is enabled for this user.
    /// When true, login requires a valid TOTP code or backup code after password.
    /// </summary>
    [Id(18)]
    public bool IsMfaEnabled { get; set; }

    /// <summary>
    /// Base32-encoded TOTP secret key for authenticator apps (RFC 6238).
    /// Generated during MFA setup; stored encrypted at rest via Orleans persistence.
    /// </summary>
    [Id(19)]
    public string? TotpSecretKey { get; set; }

    /// <summary>
    /// One-time backup codes for MFA recovery (hashed, SHA-256).
    /// Each code can only be used once — consumed codes are removed from the list.
    /// </summary>
    [Id(20)]
    public List<string> BackupCodeHashes { get; set; } = new();

    /// <summary>
    /// Date MFA was enabled.
    /// </summary>
    [Id(21)]
    public DateTime? MfaEnabledDate { get; set; }

    /// <summary>
    /// Default ward assignment for floor staff (VistA File #200, field equivalent).
    /// Used to auto-populate ward census view for inpatient nurses.
    /// </summary>
    [Id(22)]
    public string DefaultWardId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the default ward assignment.
    /// </summary>
    [Id(23)]
    public string DefaultWardName { get; set; } = string.Empty;
}

/// <summary>
/// Lightweight summary for staff directory listings and provider lookups.
/// Used in UI dropdowns, provider pickers, and cosigner selection.
/// </summary>
[GenerateSerializer]
public class NewPersonSummary
{
    /// <summary>
    /// User ID (grain key suffix).
    /// </summary>
    [Id(0)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name in VistA format (LAST,FIRST MI).
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Professional title.
    /// </summary>
    [Id(2)]
    public string? Title { get; set; }

    /// <summary>
    /// Degree abbreviation.
    /// </summary>
    [Id(3)]
    public string? Degree { get; set; }

    /// <summary>
    /// User class (PHYSICIAN, NURSE, PHARMACIST, etc.).
    /// </summary>
    [Id(4)]
    public string? UserClass { get; set; }

    /// <summary>
    /// Service/Section assignment.
    /// </summary>
    [Id(5)]
    public string? ServiceSection { get; set; }

    /// <summary>
    /// Whether the staff record is active.
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the user has an electronic signature on file.
    /// </summary>
    [Id(7)]
    public bool HasElectronicSignature { get; set; }

    /// <summary>
    /// Whether MFA is enabled for this user (§170.315(d)(13)).
    /// </summary>
    [Id(8)]
    public bool IsMfaEnabled { get; set; }
}
