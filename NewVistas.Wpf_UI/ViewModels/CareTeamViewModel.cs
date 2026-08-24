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

public partial class CareTeamViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<CareTeamMember> _members = new();
    [ObservableProperty] private CareTeamMember? _currentPcp;
    [ObservableProperty] private string? _actionMessage;

    // Add member fields
    [ObservableProperty] private string _newProviderId = string.Empty;
    [ObservableProperty] private string _newProviderName = string.Empty;
    [ObservableProperty] private string _newRole = "SPECIALIST";
    [ObservableProperty] private string _newSpecialty = string.Empty;

    public CareTeamViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<CareTeamMember> list = await workflow.GetCareTeamAsync();
        Members.Clear();
        foreach (var m in list) Members.Add(m);
        CurrentPcp = await workflow.GetPcpAsync();
    }

    [RelayCommand]
    private async Task AddMember()
    {
        if (string.IsNullOrWhiteSpace(NewProviderId)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AddCareTeamMemberAsync(NewProviderId, NewProviderName, NewRole,
                string.IsNullOrWhiteSpace(NewSpecialty) ? null : NewSpecialty, "MANUAL", null);
            ActionMessage = $"Added {NewProviderName} to care team.";
            NewProviderId = string.Empty; NewProviderName = string.Empty; NewSpecialty = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RemoveMember(CareTeamMember member)
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RemoveCareTeamMemberAsync(member.ProviderId);
            ActionMessage = $"Removed {member.ProviderName} from care team.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
