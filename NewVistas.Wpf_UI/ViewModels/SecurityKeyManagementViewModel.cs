// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Security-key administration. Reads and writes go straight to
/// <see cref="IAccessControlGrain"/>: the Web tier answers "are you who you say you are"
/// (authentication), and the grain layer answers "may you do A but not B" (authorization).
/// Routing key grants through HTTP would put the authorization decision on the wrong side
/// of that line and drop the caller's grain-call context.
/// </summary>
public partial class SecurityKeyManagementViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _userId = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private string _activeTab = "keys";
    [ObservableProperty] private string _categoryFilter = string.Empty;

    [ObservableProperty] private AccessControlStateDto? _userState;
    [ObservableProperty] private ObservableCollection<KeyDefinitionDto> _availableKeys = new();
    [ObservableProperty] private ObservableCollection<KeyAuditEntryDto> _auditLog = new();

    // Filtered keys for Grant tab
    [ObservableProperty] private ObservableCollection<KeyDefinitionDto> _filteredKeys = new();

    // Categories for filter ComboBox
    [ObservableProperty] private ObservableCollection<string> _categories = new();

    // Tab visibility computed properties
    public bool IsKeysTabActive => ActiveTab == "keys";
    public bool IsGrantTabActive => ActiveTab == "grant";
    public bool IsAuditTabActive => ActiveTab == "audit";

    public SecurityKeyManagementViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    private IAccessControlGrain Acl() => _grains.GetGrain<IAccessControlGrain>($"ACL:{UserId.Trim()}");

    /// <summary>Who the audit trail should attribute this change to — the signed-in admin.</summary>
    private (string Id, string Name) Actor =>
        (_grains.CurrentUserId ?? "ADMIN", _grains.CurrentUserName ?? "Admin User");

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsKeysTabActive));
        OnPropertyChanged(nameof(IsGrantTabActive));
        OnPropertyChanged(nameof(IsAuditTabActive));
    }

    partial void OnCategoryFilterChanged(string value)
    {
        ApplyKeyFilter();
    }

    private static string Esc(string id) => Uri.EscapeDataString(id.Trim());

    [RelayCommand]
    private async Task LoadUserAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return;
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            IAccessControlGrain grain = Acl();
            AccessControlState state = await grain.GetAccessControlStateAsync();
            UserState = new AccessControlStateDto
            {
                UserId = state.UserId,
                SecurityKeys = state.SecurityKeys,
                HasActiveSession = state.HasActiveSession,
                SessionStartTime = state.SessionStartTime,
                LastActivityTime = state.LastActivityTime,
                ClientDevice = state.ClientDevice,
                ClientIpAddress = state.ClientIpAddress,
                SessionTimeoutMinutes = state.SessionTimeoutMinutes,
            };

            if (AvailableKeys.Count == 0)
            {
                // The key catalog is a static definition list, not stored data — it comes from
                // the shared SecurityKeys catalog rather than an HTTP round trip.
                AvailableKeys.Clear();
                foreach (KeyDefinitionDto k in KeyCatalog) AvailableKeys.Add(k);

                Categories.Clear();
                Categories.Add(string.Empty); // "All Categories"
                foreach (string cat in KeyCatalog.Select(k => k.Category).Distinct().OrderBy(c => c))
                    Categories.Add(cat);
            }

            List<SecurityKeyAuditEntry> log = await grain.GetKeyAuditLogAsync();
            AuditLog.Clear();
            foreach (SecurityKeyAuditEntry e in log.OrderByDescending(e => e.ActionDateTime))
            {
                AuditLog.Add(new KeyAuditEntryDto
                {
                    KeyName = e.KeyName,
                    Action = e.Action,
                    PerformedByUserId = e.PerformedByUserId,
                    PerformedByName = e.PerformedByName,
                    ActionDateTime = e.ActionDateTime,
                    Reason = e.Reason,
                });
            }

            ApplyKeyFilter();
        }
        catch (Exception ex) { Error = $"Error loading user: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task GrantKeyAsync(string keyName)
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(keyName)) return;
        Error = null;
        SuccessMessage = null;
        try
        {
            (string actorId, string actorName) = Actor;
            await Acl().GrantKeyAsync(keyName, actorId, actorName, "Granted via admin UI");
            SuccessMessage = $"Key {keyName} granted.";
            await LoadUserAsync();
        }
        catch (Exception ex) { Error = $"Error granting key: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task RevokeKeyAsync(string keyName)
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(keyName)) return;
        Error = null;
        SuccessMessage = null;
        try
        {
            (string actorId, string actorName) = Actor;
            await Acl().RevokeKeyAsync(keyName, actorId, actorName, "Revoked via admin UI");
            SuccessMessage = $"Key {keyName} revoked.";
            await LoadUserAsync();
        }
        catch (Exception ex) { Error = $"Error revoking key: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ForceEndSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return;
        Error = null;
        SuccessMessage = null;
        try
        {
            await Acl().EndSessionAsync();
            SuccessMessage = "Session ended.";
            await LoadUserAsync();
        }
        catch (Exception ex) { Error = $"Error ending session: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                SuccessMessage = "Enter a User ID to load demo keys onto.";
                return;
            }

            // Grants a standard demo key set to the named user, matching the Blazor admin page.
            // The old endpoint fanned out over the Identity user store by role, which is
            // WebServer-only data — a UI action should not depend on that.
            (string actorId, string actorName) = Actor;
            await Acl().SetKeysAsync(
                new List<string> { "ORES", "PROVIDER", "LRLAB", "DGADMIT" },
                actorId, actorName, "Demo key assignment");
            SuccessMessage = "Demo keys loaded.";
            await LoadUserAsync();
        }
        catch (Exception ex) { Error = $"Error loading demo: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SwitchTab(string tab)
    {
        ActiveTab = tab;
    }

    private void ApplyKeyFilter()
    {
        FilteredKeys.Clear();
        var source = string.IsNullOrEmpty(CategoryFilter)
            ? AvailableKeys
            : AvailableKeys.Where(k => k.Category == CategoryFilter);
        foreach (var k in source) FilteredKeys.Add(k);
    }

    /// <summary>
    /// Static catalog of the security keys an administrator can grant. These are
    /// definitions, not stored state, so they live in the client rather than behind a
    /// round trip. Mirrors the list on the Blazor admin page.
    /// </summary>
    private static readonly KeyDefinitionDto[] KeyCatalog =
    {
        new() { KeyName = "ORES", Description = "Order Entry/Results Reporting", Category = "CPRS" },
        new() { KeyName = "ORELSE", Description = "Order Entry/Results Reporting (Elevated)", Category = "CPRS" },
        new() { KeyName = "PROVIDER", Description = "Clinical Provider", Category = "Clinical" },
        new() { KeyName = "XUMGR", Description = "System Manager", Category = "System" },
        new() { KeyName = "XUPROG", Description = "Programmer", Category = "System" },
        new() { KeyName = "XUPROGMODE", Description = "Programmer Mode", Category = "System" },
        new() { KeyName = "PSJ RPHARM", Description = "Pharmacy - Registered Pharmacist", Category = "Pharmacy" },
        new() { KeyName = "PSORPH", Description = "Pharmacy - Outpatient Pharmacist", Category = "Pharmacy" },
        new() { KeyName = "LRVERIFY", Description = "Lab - Verify Results", Category = "Lab" },
        new() { KeyName = "LRLAB", Description = "Lab - General Access", Category = "Lab" },
        new() { KeyName = "DGADMIT", Description = "ADT - Admit Patients", Category = "ADT" },
        new() { KeyName = "DGDISCHARGE", Description = "ADT - Discharge Patients", Category = "ADT" },
    };

    // ── DTOs ──────────────────────────────────────────────────────────────

    public class AccessControlStateDto
    {
        public string UserId { get; set; } = "";
        public HashSet<string> SecurityKeys { get; set; } = new();
        public bool HasActiveSession { get; set; }
        public DateTime? SessionStartTime { get; set; }
        public DateTime? LastActivityTime { get; set; }
        public string? ClientDevice { get; set; }
        public string? ClientIpAddress { get; set; }
        public int SessionTimeoutMinutes { get; set; }
    }

    public class KeyDefinitionDto
    {
        public string KeyName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsHeld { get; set; }
    }

    public class KeyAuditEntryDto
    {
        public string KeyName { get; set; } = "";
        public string Action { get; set; } = "";
        public string PerformedByUserId { get; set; } = "";
        public string PerformedByName { get; set; } = "";
        public DateTime ActionDateTime { get; set; }
        public string? Reason { get; set; }
    }

    private class DemoLoadResult
    {
        public string Message { get; set; } = "";
    }
}
