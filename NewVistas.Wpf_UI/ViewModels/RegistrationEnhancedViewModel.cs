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

public partial class RegistrationEnhancedViewModel : BasePatientViewModel
{
    [ObservableProperty] private AdvanceDirectiveState? _advanceDirectives;
    [ObservableProperty] private IdentityVerificationState? _identityVerification;
    [ObservableProperty] private string? _actionMessage;

    public RegistrationEnhancedViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        AdvanceDirectives = await workflow.GetAdvanceDirectivesAsync();
        IdentityVerification = await workflow.GetIdentityVerificationAsync();
    }

    [RelayCommand]
    private async Task UpdateCodeStatus()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateCodeStatusAsync(CodeStatus.FullCode, "REG");
            ActionMessage = "Code status updated.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task VerifyIdentity()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordIdentityVerificationAsync(
                IdentityDocumentType.VaIdCard, "ID-001", null, null,
                IdentityVerificationResult.Verified, false, null, null, "REG", "Clerk", null);
            ActionMessage = "Identity verified.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AddLivingWill()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AddAdvanceDirectiveDocumentAsync(
                AdvanceDirectiveType.LivingWill, DateTime.UtcNow, "Patient provided", null, null);
            ActionMessage = "Living will added.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
