// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http.Json;
using System.Text.Json;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// VistA-style Access/Verify code login — mirrors XU Kernel sign-on
/// (XUSRB/SETUP^XUSRB).
///
/// Flow:
///   1. Prompt for Access Code (username) and Verify Code (password).
///   2. Authenticate via the WebServer HTTP API (/api/auth/login).
///   3. On success, start an Orleans session (IAccessControlGrain.StartSessionAsync).
///   4. Load the user's security keys into SessionState.
///   5. If MFA is required, prompt for TOTP code (/api/auth/mfa/verify).
///
/// Three failed attempts locks the terminal (VistA convention).
/// </summary>
public class LoginMenu : IMenu
{
    public string Title => "Sign-On";

    private const int MaxAttempts = 3;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task RunAsync(MenuContext ctx)
    {
        int attempts = 0;

        while (attempts < MaxAttempts)
        {
            TerminalIO.Clear();
            TerminalIO.WriteDivider('=');
            TerminalIO.WriteLine("  NEWVISTAS CLINICAL INFORMATION SYSTEM");
            TerminalIO.WriteLine("  VistA-Style Character User Interface");
            TerminalIO.WriteDivider('=');
            TerminalIO.WriteBlank();

            if (attempts > 0)
                TerminalIO.WriteWarning($"** {MaxAttempts - attempts} attempt(s) remaining **");

            TerminalIO.WriteBlank();
            string accessCode = TerminalIO.Prompt("Access Code");
            if (string.IsNullOrWhiteSpace(accessCode)) { attempts++; continue; }

            // Read Verify Code with masked input
            TerminalIO.Write("Verify Code: ");
            string verifyCode = ReadMaskedInput();
            TerminalIO.WriteBlank();

            if (string.IsNullOrWhiteSpace(verifyCode)) { attempts++; continue; }

            TerminalIO.WriteBlank();
            TerminalIO.WriteLine("Validating credentials...");

            bool success = await AttemptLoginAsync(ctx, accessCode, verifyCode);
            if (success)
            {
                // Load security keys from the AccessControl grain
                await LoadSecurityKeysAsync(ctx);

                // Start the Orleans session (mirrors SETUP^XUSRB)
                await StartSessionAsync(ctx);

                ctx.Session.IsLoggedIn = true;
                ctx.Session.LoginTime = DateTime.UtcNow;
                ctx.Session.LastActivity = DateTime.UtcNow;

                TerminalIO.WriteBlank();
                TerminalIO.WriteSuccess($"Good {GetTimeOfDayGreeting()}, {TerminalColor.BrightWhite(ctx.Session.UserName)}");
                TerminalIO.WriteLine($"  You last signed on {ctx.Session.LoginTime:MM/dd/yyyy HH:mm}");
                TerminalIO.WriteBlank();

                int keyCount = ctx.Session.SecurityKeys.Count;
                TerminalIO.WriteLine($"  {keyCount} security key(s) on file.");
                TerminalIO.Pause();
                return;
            }

            attempts++;
            TerminalIO.WriteBlank();
            TerminalIO.WriteError("Not a valid ACCESS CODE/VERIFY CODE pair.");
            TerminalIO.Pause();
        }

        // Locked out after max attempts
        TerminalIO.WriteBlank();
        TerminalIO.WriteDivider('*');
        TerminalIO.WriteError("Maximum sign-on attempts exceeded.");
        TerminalIO.WriteError("Contact your system administrator (IRM).");
        TerminalIO.WriteDivider('*');
        TerminalIO.Pause();

        // Signal that login failed — caller should exit
        ctx.Session.IsLoggedIn = false;
    }

    private static async Task<bool> AttemptLoginAsync(MenuContext ctx, string accessCode, string verifyCode)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5298") };
            var loginPayload = new { Username = accessCode, Password = verifyCode };
            HttpResponseMessage response = await http.PostAsJsonAsync("/api/auth/login", loginPayload);

            if (!response.IsSuccessStatusCode)
                return false;

            string json = await response.Content.ReadAsStringAsync();
            LoginResponse? loginResult = JsonSerializer.Deserialize<LoginResponse>(json, JsonOpts);

            if (loginResult is null)
                return false;

            // Check if MFA is required
            if (loginResult.RequiresMfa)
            {
                bool mfaOk = await HandleMfaAsync(ctx, http, loginResult.MfaToken ?? "");
                if (!mfaOk) return false;
            }

            ctx.Session.UserId = loginResult.UserId ?? accessCode;
            ctx.Session.UserName = loginResult.UserName ?? accessCode.ToUpperInvariant();
            ctx.Session.Division = loginResult.Division ?? "500";
            ctx.Session.FacilityName = loginResult.FacilityName ?? "NEWVISTAS MEDICAL CENTER";

            // Set the Orleans RequestContext now that we have identity
            ctx.SetRequestContext();

            return true;
        }
        catch
        {
            // HTTP API not available — fall back to direct Orleans login
            return await AttemptDirectLoginAsync(ctx, accessCode, verifyCode);
        }
    }

    /// <summary>
    /// Fallback when WebServer is not running: authenticate directly via
    /// Orleans grains.  Creates a minimal session.
    /// </summary>
    private static async Task<bool> AttemptDirectLoginAsync(MenuContext ctx, string accessCode, string verifyCode)
    {
        try
        {
            // Use the access code as both user ID and name for direct mode
            ctx.Session.UserId = accessCode;
            ctx.Session.UserName = accessCode.ToUpperInvariant();
            ctx.SetRequestContext();

            // Try to start a session — if the grain rejects, login fails
            IAccessControlGrain acl = ctx.Client.GetGrain<IAccessControlGrain>($"ACL:{accessCode}");
            await acl.StartSessionAsync("500", "MAIN", "CharUI", "127.0.0.1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> HandleMfaAsync(MenuContext ctx, HttpClient http, string mfaToken)
    {
        TerminalIO.WriteBlank();
        TerminalIO.WriteLine("  Multi-factor authentication required.");
        string code = TerminalIO.Prompt("Enter TOTP Code");
        if (string.IsNullOrWhiteSpace(code)) return false;

        try
        {
            var mfaPayload = new { MfaToken = mfaToken, Code = code };
            HttpResponseMessage response = await http.PostAsJsonAsync("/api/auth/mfa/verify", mfaPayload);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task LoadSecurityKeysAsync(MenuContext ctx)
    {
        try
        {
            IAccessControlGrain acl = ctx.GetAccessControl();
            IReadOnlySet<string> keys = await acl.GetKeysAsync();
            ctx.Session.SecurityKeys = [.. keys];
        }
        catch
        {
            // If keys can't be loaded, continue with empty set
            ctx.Session.SecurityKeys = [];
        }
    }

    private static async Task StartSessionAsync(MenuContext ctx)
    {
        try
        {
            IAccessControlGrain acl = ctx.GetAccessControl();
            await acl.StartSessionAsync(
                ctx.Session.Division, ctx.Session.FacilityName,
                "CharUI", "127.0.0.1");
        }
        catch
        {
            // Session start failure is non-fatal for direct-connect mode
        }
    }

    private static string ReadMaskedInput()
    {
        var chars = new List<char>();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }

    private static string GetTimeOfDayGreeting()
    {
        int hour = DateTime.Now.Hour;
        return hour switch
        {
            < 12 => "morning",
            < 17 => "afternoon",
            _ => "evening"
        };
    }

    // ── DTO for login API response ─────────────────────────────────────

    private sealed class LoginResponse
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? Division { get; set; }
        public string? FacilityName { get; set; }
        public bool RequiresMfa { get; set; }
        public string? MfaToken { get; set; }
        public string? Token { get; set; }
    }
}
