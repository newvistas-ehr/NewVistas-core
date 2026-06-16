// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Patient portal account state — stores patient self-service credentials.
/// Grain key: "PORTAL-ACCT:{patientId}".
/// Separate from clinician Identity (ASP.NET Core Identity).
/// </summary>
[GenerateSerializer]
public class PatientAccountState
{
    /// <summary>The patient ID this account is linked to.</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient's email address (used for login).</summary>
    [Id(1)]
    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the patient's password.</summary>
    [Id(2)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Display name (e.g., "John Smith").</summary>
    [Id(3)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Whether the account has been registered.</summary>
    [Id(4)]
    public bool IsRegistered { get; set; }

    /// <summary>Whether the account is active (can be deactivated by admin).</summary>
    [Id(5)]
    public bool IsActive { get; set; } = true;

    /// <summary>Account creation date.</summary>
    [Id(6)]
    public DateTime CreatedDate { get; set; }

    /// <summary>Last login timestamp.</summary>
    [Id(7)]
    public DateTime? LastLoginDate { get; set; }

    /// <summary>Last modification timestamp.</summary>
    [Id(8)]
    public DateTime LastModifiedDate { get; set; }
}
