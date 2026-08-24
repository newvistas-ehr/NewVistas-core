// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.Security;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Consults tab — internal consults plus external referrals (incl. Contract
/// Health Services / PRC authorization workflow under 25 CFR Part 136).
///
/// Internal consults: list + new-consult form (existing CPRS behavior).
/// External referrals: list with CHS columns (priority class, authorized
/// dollar amount, CHS coordinator). When the selected referral
/// <c>IsChsReferral</c> and the user holds <c>CanAuthorizeChs</c>, a small
/// CHS action bar exposes Request / Approve / Deny.
/// </summary>
public sealed partial class ConsultsViewModel : ChartTabViewModelBase
{
    private readonly AuthService _auth;

    // Internal consults
    public ObservableCollection<ConsultDto> Consults { get; } = new();
    [ObservableProperty] private ConsultDto? _selectedConsult;
    [ObservableProperty] private bool _showRequestForm;
    [ObservableProperty] private string _newService = string.Empty;
    [ObservableProperty] private string _newReason = string.Empty;
    [ObservableProperty] private string _newUrgency = "ROUTINE";

    public string[] Urgencies { get; } = ["ROUTINE", "STAT", "EMERGENT"];

    // External referrals + CHS
    public ObservableCollection<ExternalReferralDto> Referrals { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReferralIsChsCandidate))]
    [NotifyPropertyChangedFor(nameof(SelectedReferralAlreadyAuthorized))]
    [NotifyPropertyChangedFor(nameof(SelectedReferralDetails))]
    [NotifyCanExecuteChangedFor(nameof(RequestChsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApproveChsCommand))]
    [NotifyCanExecuteChangedFor(nameof(DenyChsCommand))]
    private ExternalReferralDto? _selectedReferral;

    /// <summary>True if the user holds the CHS authorization key.</summary>
    public bool CanAuthorizeChs => _auth.SecurityKeys.Contains(SecurityKeys.CanAuthorizeChs);

    /// <summary>True if a referral is selected and is flagged as a CHS-funded request.</summary>
    public bool SelectedReferralIsChsCandidate => SelectedReferral?.IsChsReferral == true;

    /// <summary>True once the CHS coordinator has approved or denied the request.</summary>
    public bool SelectedReferralAlreadyAuthorized => SelectedReferral?.ChsAuthorizationDate.HasValue == true;

    public string SelectedReferralDetails => SelectedReferral switch
    {
        null => string.Empty,
        var r => $"Referred to {r.ExternalFacilityName}\n" +
                 $"Status: {r.Status}\n" +
                 (r.IsChsReferral
                    ? $"CHS Priority Class: {r.MedicalPriorityClass ?? "(unset)"}\n" +
                      $"Authorized Amount: {(r.AuthorizedAmount.HasValue ? r.AuthorizedAmount.Value.ToString("C") : "(pending)")}\n" +
                      $"Alternate Resources Checked: {(r.AlternateResourcesChecked ? "Yes" : "No")}\n" +
                      (r.AlternateResourcesNote != null ? $"Notes: {r.AlternateResourcesNote}\n" : string.Empty) +
                      (r.ChsAuthorizationDate.HasValue
                          ? $"Decision: {r.ChsAuthorizationDate:yyyy-MM-dd} by {r.ChsAuthorizedByName ?? "—"}"
                          : "Decision: pending")
                    : "Not a CHS-funded referral."),
    };

    // CHS action form fields
    [ObservableProperty] private bool _showChsForm;
    [ObservableProperty] private string _chsAction = "REQUEST";   // REQUEST | APPROVE | DENY
    [ObservableProperty] private string _chsEstimatedCost = string.Empty;
    [ObservableProperty] private string _chsPriorityClass = "I";
    [ObservableProperty] private bool _chsAlternateResourcesChecked = true;
    [ObservableProperty] private string _chsAlternateResourcesNote = string.Empty;
    [ObservableProperty] private string _chsAuthorizedAmount = string.Empty;
    [ObservableProperty] private string _chsAuthorizationNumber = string.Empty;
    [ObservableProperty] private string _chsDenialReason = string.Empty;

    public string[] PriorityClasses { get; } = ["I", "II", "III", "IV", "V"];

    public ConsultsViewModel(ChartDataService data, PatientContext context, AuthService auth)
        : base(data, context)
    {
        _auth = auth;
    }

    protected override async Task LoadAsync()
    {
        var consults = Data.GetConsultsAsync(PatientId);
        var referrals = SafeGetReferralsAsync();

        await Task.WhenAll(consults, referrals);

        Consults.Clear();
        foreach (var c in await consults) Consults.Add(c);

        Referrals.Clear();
        foreach (var r in await referrals) Referrals.Add(r);
    }

    /// <summary>
    /// Referrals endpoint will 404 if EXTERNAL_REFERRAL_TRACKING is disabled
    /// at this site; swallow so the consults list still loads cleanly.
    /// </summary>
    private async Task<List<ExternalReferralDto>> SafeGetReferralsAsync()
    {
        try { return await Data.GetExternalReferralsAsync(PatientId); }
        catch { return []; }
    }

    protected override void ClearData()
    {
        Consults.Clear();
        Referrals.Clear();
        SelectedConsult = null;
        SelectedReferral = null;
        ShowChsForm = false;
    }

    [RelayCommand]
    private void ToggleRequestForm() => ShowRequestForm = !ShowRequestForm;

    [RelayCommand]
    private async Task RequestConsultAsync()
    {
        if (string.IsNullOrWhiteSpace(NewService) || string.IsNullOrWhiteSpace(NewReason)) return;
        ErrorText = string.Empty;
        try
        {
            await Data.RequestConsultAsync(PatientId, NewService, NewReason, NewUrgency);
            NewService = string.Empty;
            NewReason = string.Empty;
            ShowRequestForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    // ── CHS Authorization ─────────────────────────────────────────────────

    [RelayCommand]
    private void ShowChsRequestForm()  { ChsAction = "REQUEST"; ShowChsForm = true; }

    [RelayCommand]
    private void ShowChsApproveForm()  { ChsAction = "APPROVE"; ShowChsForm = true; }

    [RelayCommand]
    private void ShowChsDenyForm()     { ChsAction = "DENY";    ShowChsForm = true; }

    [RelayCommand]
    private void CancelChsForm()       { ShowChsForm = false; }

    [RelayCommand(CanExecute = nameof(CanRequestChs))]
    private async Task RequestChsAsync()
    {
        if (SelectedReferral is null) return;
        if (!decimal.TryParse(ChsEstimatedCost, out decimal cost))
        {
            ErrorText = "Estimated cost must be a number.";
            return;
        }
        ErrorText = string.Empty;
        try
        {
            await Data.RequestChsAuthorizationAsync(
                PatientId, SelectedReferral.ReferralId, cost, ChsPriorityClass,
                ChsAlternateResourcesChecked,
                string.IsNullOrWhiteSpace(ChsAlternateResourcesNote) ? null : ChsAlternateResourcesNote);
            ShowChsForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanApproveChs))]
    private async Task ApproveChsAsync()
    {
        if (SelectedReferral is null) return;
        if (!decimal.TryParse(ChsAuthorizedAmount, out decimal amount))
        {
            ErrorText = "Authorized amount must be a number.";
            return;
        }
        ErrorText = string.Empty;
        try
        {
            await Data.ApproveChsAuthorizationAsync(
                PatientId, SelectedReferral.ReferralId, amount,
                string.IsNullOrWhiteSpace(ChsAuthorizationNumber) ? null : ChsAuthorizationNumber);
            ShowChsForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand(CanExecute = nameof(CanDenyChs))]
    private async Task DenyChsAsync()
    {
        if (SelectedReferral is null) return;
        if (string.IsNullOrWhiteSpace(ChsDenialReason))
        {
            ErrorText = "Denial reason is required.";
            return;
        }
        ErrorText = string.Empty;
        try
        {
            await Data.DenyChsAuthorizationAsync(PatientId, SelectedReferral.ReferralId, ChsDenialReason);
            ShowChsForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    private bool CanRequestChs() => SelectedReferral is not null
                                    && SelectedReferralIsChsCandidate
                                    && !SelectedReferralAlreadyAuthorized
                                    && CanAuthorizeChs;

    private bool CanApproveChs() => CanRequestChs();
    private bool CanDenyChs()    => CanRequestChs();
}
