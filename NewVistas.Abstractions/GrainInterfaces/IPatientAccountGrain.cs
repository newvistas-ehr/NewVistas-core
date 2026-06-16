// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient portal account grain — manages patient self-service credentials.
/// Grain key: "PORTAL-ACCT:{patientId}" (one account per patient).
///
/// Separate from clinician Identity (NewVistasUser/ASP.NET Core Identity).
/// Patients register with their patient ID + email, set a password,
/// and receive a JWT scoped to their patientId only.
///
/// §170.315(e)(1) — View, Download, and Transmit to 3rd Party (patient access).
/// </summary>
public interface IPatientAccountGrain : IGrainWithStringKey
{
    /// <summary>Register a new patient portal account.</summary>
    Task<bool> RegisterAsync(string email, string passwordHash, string? displayName);

    /// <summary>Verify credentials for login. Returns true if password hash matches.</summary>
    Task<bool> VerifyCredentialsAsync(string passwordHash);

    /// <summary>Get account details (email, display name, status, etc.).</summary>
    Task<GrainStates.PatientAccountState> GetAccountAsync();

    /// <summary>Check if this patient has a portal account.</summary>
    Task<bool> IsRegisteredAsync();

    /// <summary>Update the display name.</summary>
    Task UpdateDisplayNameAsync(string displayName);

    /// <summary>Update the email address.</summary>
    Task UpdateEmailAsync(string email);

    /// <summary>Change password (caller must verify old password first).</summary>
    Task ChangePasswordAsync(string newPasswordHash);

    /// <summary>Deactivate the account (admin action).</summary>
    Task DeactivateAsync();

    /// <summary>Reactivate a deactivated account.</summary>
    Task ReactivateAsync();

    /// <summary>Record a login timestamp.</summary>
    Task RecordLoginAsync();
}
