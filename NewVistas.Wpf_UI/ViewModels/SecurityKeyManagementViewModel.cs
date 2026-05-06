// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class SecurityKeyManagementViewModel : ObservableObject
{
    private readonly ApiClient _api;
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

    public SecurityKeyManagementViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

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
            var stateResponse = await _api.Http.GetAsync($"api/accesscontrol/users/{Esc(UserId)}");
            if (stateResponse.IsSuccessStatusCode)
            {
                UserState = await stateResponse.Content.ReadFromJsonAsync<AccessControlStateDto>(ApiClient.Json);
            }
            else
            {
                Error = $"Error loading user: {stateResponse.StatusCode}";
                return;
            }

            if (AvailableKeys.Count == 0)
            {
                var keysResponse = await _api.Http.GetAsync("api/accesscontrol/keys");
                if (keysResponse.IsSuccessStatusCode)
                {
                    var keys = await keysResponse.Content.ReadFromJsonAsync<List<KeyDefinitionDto>>(ApiClient.Json) ?? [];
                    AvailableKeys.Clear();
                    foreach (var k in keys) AvailableKeys.Add(k);

                    Categories.Clear();
                    Categories.Add(string.Empty); // "All Categories"
                    foreach (var cat in keys.Select(k => k.Category).Distinct().OrderBy(c => c))
                        Categories.Add(cat);
                }
            }

            var auditResponse = await _api.Http.GetAsync($"api/accesscontrol/users/{Esc(UserId)}/keys/audit");
            if (auditResponse.IsSuccessStatusCode)
            {
                var log = await auditResponse.Content.ReadFromJsonAsync<List<KeyAuditEntryDto>>(ApiClient.Json) ?? [];
                AuditLog.Clear();
                foreach (var e in log.OrderByDescending(e => e.ActionDateTime)) AuditLog.Add(e);
            }

            ApplyKeyFilter();
        }
        catch (Exception ex) { Error = $"Cannot connect to API: {ex.Message}"; }
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
            var payload = new { KeyName = keyName, Reason = "Granted via admin UI" };
            var response = await _api.Http.PostAsJsonAsync($"api/accesscontrol/users/{Esc(UserId)}/keys", payload);
            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = $"Key {keyName} granted.";
                await LoadUserAsync();
            }
            else
            {
                Error = $"Error granting key: {response.StatusCode}";
            }
        }
        catch (Exception ex) { Error = $"Cannot connect to API: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task RevokeKeyAsync(string keyName)
    {
        if (string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(keyName)) return;
        Error = null;
        SuccessMessage = null;
        try
        {
            var response = await _api.Http.DeleteAsync(
                $"api/accesscontrol/users/{Esc(UserId)}/keys/{Esc(keyName)}?reason=Revoked+via+admin+UI");
            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = $"Key {keyName} revoked.";
                await LoadUserAsync();
            }
            else
            {
                Error = $"Error revoking key: {response.StatusCode}";
            }
        }
        catch (Exception ex) { Error = $"Cannot connect to API: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ForceEndSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(UserId)) return;
        Error = null;
        SuccessMessage = null;
        try
        {
            var response = await _api.Http.PostAsync($"api/accesscontrol/users/{Esc(UserId)}/session/end", null);
            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = "Session ended.";
                await LoadUserAsync();
            }
            else
            {
                Error = $"Error ending session: {response.StatusCode}";
            }
        }
        catch (Exception ex) { Error = $"Cannot connect to API: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            var response = await _api.Http.PostAsync("api/accesscontrol/demo/load", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DemoLoadResult>(ApiClient.Json);
                SuccessMessage = result?.Message ?? "Demo keys loaded.";
                if (!string.IsNullOrWhiteSpace(UserId)) await LoadUserAsync();
            }
            else
            {
                Error = $"Error loading demo: {response.StatusCode}";
            }
        }
        catch (Exception ex) { Error = $"Cannot connect to API: {ex.Message}"; }
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
