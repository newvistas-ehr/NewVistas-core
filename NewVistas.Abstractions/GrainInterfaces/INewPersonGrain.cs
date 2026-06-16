// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// NEW PERSON Grain — staff/provider directory (VistA File #200).
///
/// Separates three concerns that VistA's File #200 conflates:
///   1. Authentication → ASP.NET Core Identity (NOT this grain)
///   2. Authorization  → ASP.NET Core [Authorize] roles/policies (NOT this grain)
///   3. Staff directory → THIS GRAIN — who someone is and what clinical role they hold
///
/// Derived from MUMPS routines:
///   XUSERNEW.m — NEW PERSON file creation and maintenance
///   XUSER.m    — User attribute lookups (name, title, class)
///   XUESSO.m   — Electronic signature operations (ESVERIFY, ESSAVE)
///   XUSESIG.m  — Electronic signature hash/verify
///
/// Grain Key: "USER:{userId}" where userId is the ASP.NET Core Identity user ID.
/// </summary>
public interface INewPersonGrain : IGrainWithStringKey
{
    // ─── Staff Directory (File #200 demographic fields) ──────────────────

    /// <summary>
    /// Get the full staff/provider record.
    /// </summary>
    Task<GrainStates.NewPersonState> GetPersonAsync();

    /// <summary>
    /// Initialize or update the core identity fields.
    /// Called when a user account is first created or when admin updates staff record.
    /// </summary>
    Task UpdateProfileAsync(
        string name,
        string? title,
        string? degree,
        string? serviceSection,
        string? userClass,
        string? providerType,
        string? specialty,
        string? institutionId,
        string? institutionName,
        string? divisionId,
        string? divisionName);

    /// <summary>
    /// Activate or deactivate the staff record.
    /// Maps to VistA File #200 DISUSER field (.204) and TERMINATION DATE.
    /// </summary>
    Task SetActiveStatusAsync(bool isActive, DateTime? terminationDate = null);

    /// <summary>
    /// Get the display name in VistA format (LAST,FIRST MI).
    /// </summary>
    Task<string> GetDisplayNameAsync();

    /// <summary>
    /// Check if the user has an active record.
    /// </summary>
    Task<bool> IsActiveAsync();

    // ─── Electronic Signature (Layer 4 — XUESSO.m / XUSESIG.m) ──────────

    /// <summary>
    /// Set the electronic signature code hash.
    /// The raw signature code is NEVER stored — only its hash.
    /// Mirrors VistA ELECTRONIC SIGNATURE CODE field (#200, .165).
    /// Users set their own signature; admins can only clear it.
    /// </summary>
    Task SetElectronicSignatureHashAsync(string signatureHash);

    /// <summary>
    /// Verify the provided signature hash matches the stored hash.
    /// Returns true if the signature is valid.
    /// Called at the moment of signing clinical actions (orders, notes, etc.).
    /// Mirrors XUSESIG ESVERIFY entry point.
    /// </summary>
    Task<bool> VerifyElectronicSignatureAsync(string signatureHash);

    /// <summary>
    /// Check whether the user has an electronic signature on file.
    /// </summary>
    Task<bool> HasElectronicSignatureAsync();

    /// <summary>
    /// Admin-only: clear the electronic signature (forces user to set a new one).
    /// Mirrors VistA Kernel option "Electronic Signature Code Edit".
    /// </summary>
    Task ClearElectronicSignatureAsync();

    // ─── Multi-Factor Authentication (§170.315(d)(13)) ─────────────────

    /// <summary>
    /// Generate a new TOTP secret key for MFA setup.
    /// Returns the Base32-encoded secret for the user to register with their authenticator app.
    /// MFA is NOT yet enforced — call <see cref="EnableMfaAsync"/> after the user verifies a code.
    /// </summary>
    Task<string> SetupMfaAsync();

    /// <summary>
    /// Enable MFA enforcement after the user has verified their TOTP setup.
    /// Requires a valid TOTP code to confirm the authenticator is configured correctly.
    /// Returns true if the code was valid and MFA was enabled.
    /// </summary>
    Task<bool> EnableMfaAsync(string totpCode);

    /// <summary>
    /// Disable MFA for this user. Clears the TOTP secret and backup codes.
    /// </summary>
    Task DisableMfaAsync();

    /// <summary>
    /// Verify a 6-digit TOTP code against the stored secret.
    /// Accepts codes for the current and adjacent 30-second windows (±1 step).
    /// </summary>
    Task<bool> VerifyTotpCodeAsync(string totpCode);

    /// <summary>
    /// Check whether MFA is enabled for this user.
    /// </summary>
    Task<bool> IsMfaEnabledAsync();

    /// <summary>
    /// Generate a new set of one-time backup codes for MFA recovery.
    /// Returns the raw codes (display to user once); hashes are stored.
    /// Replaces any existing backup codes.
    /// </summary>
    Task<List<string>> GenerateBackupCodesAsync();

    /// <summary>
    /// Attempt to use a backup code for MFA. Returns true if the code was valid.
    /// Valid codes are consumed (removed) and cannot be reused.
    /// </summary>
    Task<bool> UseBackupCodeAsync(string backupCode);

    // ─── Ward Assignment ─────────────────────────────────────────────────

    /// <summary>
    /// Set the default ward assignment for floor staff (nurses, ward clerks).
    /// Maps to VistA File #200 ward-assignment equivalent.
    /// Used to auto-populate ward census view for inpatient nurses.
    /// </summary>
    Task SetDefaultWardAsync(string wardId, string wardName);

    // ─── Summary for lists/lookups ───────────────────────────────────────

    /// <summary>
    /// Lightweight summary for staff directory listings and provider lookups.
    /// </summary>
    Task<GrainStates.NewPersonSummary> GetSummaryAsync();
}
