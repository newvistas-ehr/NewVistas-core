// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Security.Cryptography;
using System.Text;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// NEW PERSON Grain — staff/provider directory (VistA File #200).
/// Stores who someone is and what clinical role they hold.
/// Electronic signature operations mirror XUSESIG.m ESVERIFY/ESSAVE.
/// </summary>
public class NewPersonGrain : Grain, INewPersonGrain
{
    private readonly IPersistentState<NewPersonState> _state;

    public NewPersonGrain(
        [PersistentState("newPersonState", "newPersonStore")] IPersistentState<NewPersonState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
        {
            // Extract userId from grain key "USER:{userId}"
            string key = this.GetPrimaryKeyString();
            _state.State.UserId = key.StartsWith("USER:") ? key[5..] : key;
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    // ─── Staff Directory ─────────────────────────────────────────────────

    private IProviderDirectoryGrain Directory()
        => GrainFactory.GetGrain<IProviderDirectoryGrain>("PROVIDER-DIRECTORY");

    /// <summary>Push the current record into the searchable provider directory.</summary>
    private Task SyncDirectoryAsync() => Directory().AddOrUpdateAsync(new ProviderDirectoryEntry
    {
        UserId = _state.State.UserId,
        Name = _state.State.Name,
        Title = _state.State.Title,
        ProviderType = _state.State.ProviderType,
        Specialty = _state.State.Specialty,
        ServiceSection = _state.State.ServiceSection,
        IsActive = _state.State.IsActive
    });

    public Task<NewPersonState> GetPersonAsync() => Task.FromResult(_state.State);

    public async Task UpdateProfileAsync(
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
        string? divisionName)
    {
        _state.State.Name = name;
        _state.State.Title = title;
        _state.State.Degree = degree;
        _state.State.ServiceSection = serviceSection;
        _state.State.UserClass = userClass;
        _state.State.ProviderType = providerType;
        _state.State.Specialty = specialty;
        _state.State.InstitutionId = institutionId;
        _state.State.InstitutionName = institutionName;
        _state.State.DivisionId = divisionId;
        _state.State.DivisionName = divisionName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncDirectoryAsync();
    }

    public async Task SetActiveStatusAsync(bool isActive, DateTime? terminationDate)
    {
        _state.State.IsActive = isActive;
        _state.State.TerminationDate = terminationDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await Directory().SetActiveAsync(_state.State.UserId, isActive);
    }

    public Task<string> GetDisplayNameAsync() => Task.FromResult(_state.State.Name);

    public Task<bool> IsActiveAsync() => Task.FromResult(_state.State.IsActive);

    // ─── Ward Assignment ──────────────────────────────────────────────────

    public async Task SetDefaultWardAsync(string wardId, string wardName)
    {
        _state.State.DefaultWardId = wardId;
        _state.State.DefaultWardName = wardName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Electronic Signature (Layer 4) ──────────────────────────────────

    public async Task SetElectronicSignatureHashAsync(string signatureHash)
    {
        _state.State.ElectronicSignatureHash = signatureHash;
        _state.State.ElectronicSignatureSetDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> VerifyElectronicSignatureAsync(string signatureHash)
    {
        bool valid = !string.IsNullOrEmpty(_state.State.ElectronicSignatureHash)
                     && _state.State.ElectronicSignatureHash == signatureHash;
        return Task.FromResult(valid);
    }

    public Task<bool> HasElectronicSignatureAsync() =>
        Task.FromResult(!string.IsNullOrEmpty(_state.State.ElectronicSignatureHash));

    public async Task ClearElectronicSignatureAsync()
    {
        _state.State.ElectronicSignatureHash = null;
        _state.State.ElectronicSignatureSetDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Multi-Factor Authentication (§170.315(d)(13)) ──────────────────

    public async Task<string> SetupMfaAsync()
    {
        // Generate a 20-byte (160-bit) secret per RFC 4226/6238
        byte[] secretBytes = RandomNumberGenerator.GetBytes(20);
        string base32Secret = Base32Encode(secretBytes);
        _state.State.TotpSecretKey = base32Secret;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return base32Secret;
    }

    public async Task<bool> EnableMfaAsync(string totpCode)
    {
        if (string.IsNullOrEmpty(_state.State.TotpSecretKey))
            return false;

        if (!ValidateTotp(_state.State.TotpSecretKey, totpCode))
            return false;

        _state.State.IsMfaEnabled = true;
        _state.State.MfaEnabledDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return true;
    }

    public async Task DisableMfaAsync()
    {
        _state.State.IsMfaEnabled = false;
        _state.State.TotpSecretKey = null;
        _state.State.BackupCodeHashes.Clear();
        _state.State.MfaEnabledDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> VerifyTotpCodeAsync(string totpCode)
    {
        if (string.IsNullOrEmpty(_state.State.TotpSecretKey))
            return Task.FromResult(false);

        return Task.FromResult(ValidateTotp(_state.State.TotpSecretKey, totpCode));
    }

    public Task<bool> IsMfaEnabledAsync() => Task.FromResult(_state.State.IsMfaEnabled);

    public async Task<List<string>> GenerateBackupCodesAsync()
    {
        var rawCodes = new List<string>();
        var hashes = new List<string>();

        for (int i = 0; i < 10; i++)
        {
            // 8-character alphanumeric backup codes
            string code = GenerateBackupCode();
            rawCodes.Add(code);
            hashes.Add(HashBackupCode(code));
        }

        _state.State.BackupCodeHashes = hashes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return rawCodes;
    }

    public async Task<bool> UseBackupCodeAsync(string backupCode)
    {
        string hash = HashBackupCode(backupCode);
        if (!_state.State.BackupCodeHashes.Contains(hash))
            return false;

        _state.State.BackupCodeHashes.Remove(hash);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return true;
    }

    // ─── TOTP helpers (RFC 6238) ──────────────────────────────────────

    /// <summary>
    /// Validate a TOTP code against the secret. Accepts current window ±1 step (30s).
    /// </summary>
    private static bool ValidateTotp(string base32Secret, string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6)
            return false;

        byte[] key = Base32Decode(base32Secret);
        long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long timeStep = unixTimestamp / 30;

        // Check current window and ±1 for clock skew
        for (long i = timeStep - 1; i <= timeStep + 1; i++)
        {
            string expected = ComputeTotp(key, i);
            if (string.Equals(expected, code, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Compute the TOTP value for a given time step per RFC 6238 / RFC 4226.
    /// </summary>
    private static string ComputeTotp(byte[] key, long timeStep)
    {
        byte[] timeBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(timeBytes);

        byte[] hash = HMACSHA1.HashData(key, timeBytes);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % 1_000_000;
        return otp.ToString("D6");
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
            sb.Append(alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);

        return sb.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        int buffer = 0, bitsLeft = 0;
        var result = new List<byte>();

        foreach (char c in base32.ToUpperInvariant())
        {
            int val = alphabet.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result.Add((byte)(buffer >> bitsLeft));
            }
        }

        return result.ToArray();
    }

    private static string GenerateBackupCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I/O/0/1 for readability
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        var sb = new StringBuilder(8);
        foreach (byte b in bytes)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }

    private static string HashBackupCode(string code)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(code.ToUpperInvariant()));
        return Convert.ToBase64String(hash);
    }

    // ─── Summary ─────────────────────────────────────────────────────────

    public Task<NewPersonSummary> GetSummaryAsync() => Task.FromResult(new NewPersonSummary
    {
        UserId = _state.State.UserId,
        Name = _state.State.Name,
        Title = _state.State.Title,
        Degree = _state.State.Degree,
        UserClass = _state.State.UserClass,
        ServiceSection = _state.State.ServiceSection,
        IsActive = _state.State.IsActive,
        HasElectronicSignature = !string.IsNullOrEmpty(_state.State.ElectronicSignatureHash),
        IsMfaEnabled = _state.State.IsMfaEnabled
    });
}
