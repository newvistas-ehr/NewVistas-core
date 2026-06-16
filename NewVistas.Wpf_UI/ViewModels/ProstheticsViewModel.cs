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

public partial class ProstheticsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ProstheticsSummary> _items = new();

    // Issue form
    [ObservableProperty] private bool _showIssueForm;
    [ObservableProperty] private string _itemDescription = string.Empty;
    [ObservableProperty] private string _hcpcsCode = string.Empty;
    [ObservableProperty] private string _itemCategory = string.Empty;
    [ObservableProperty] private int _quantity = 1;
    [ObservableProperty] private decimal? _cost;
    [ObservableProperty] private bool _isServiceConnected = true;
    [ObservableProperty] private string _providerName = "Provider, Test";

    public string[] CategoryOptions { get; } = [
        "LIMB PROSTHETICS", "ORTHOTIC DEVICES", "HEARING AIDS",
        "VISUAL AIDS", "WHEELCHAIR/MOBILITY", "DURABLE MEDICAL EQUIPMENT", "OTHER"
    ];

    public ProstheticsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetProstheticsAsync();
        Items.Clear();
        foreach (var p in list) Items.Add(p);
    }

    [RelayCommand]
    private void ToggleIssueForm() => ShowIssueForm = !ShowIssueForm;

    [RelayCommand]
    private async Task IssueProsthetic()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ItemDescription)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.IssueProstheticAsync(
                ItemDescription,
                HcpcsCode.Length > 0 ? HcpcsCode : null,
                ItemCategory.Length > 0 ? ItemCategory : null,
                DateTime.UtcNow,
                Quantity,
                Cost,
                null, // providerId
                ProviderName,
                null, // locationId
                null, // locationName
                IsServiceConnected,
                null); // comments
            ShowIssueForm = false;
            ItemDescription = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
