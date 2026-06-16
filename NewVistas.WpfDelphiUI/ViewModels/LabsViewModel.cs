// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class LabsViewModel : ChartTabViewModelBase
{
    public ObservableCollection<LabResultDto> Results { get; } = new();

    [ObservableProperty] private bool _showOrderForm;
    [ObservableProperty] private string _newTestName = string.Empty;
    [ObservableProperty] private string _newLoincCode = string.Empty;
    [ObservableProperty] private string _newPriority = "ROUTINE";
    [ObservableProperty] private bool _showAbnormalOnly;

    public string[] Priorities { get; } = ["ROUTINE", "STAT", "ASAP"];

    public LabsViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = ShowAbnormalOnly
            ? await Api.GetAbnormalLabsAsync(PatientId)
            : await Api.GetLabResultsAsync(PatientId);
        Results.Clear();
        foreach (var r in items) Results.Add(r);
    }

    protected override void ClearData() => Results.Clear();

    [RelayCommand]
    private void ToggleOrderForm() => ShowOrderForm = !ShowOrderForm;

    [RelayCommand]
    private async Task ToggleAbnormalAsync()
    {
        ShowAbnormalOnly = !ShowAbnormalOnly;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task OrderLabAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTestName)) return;
        ErrorText = string.Empty;
        try
        {
            await Api.OrderLabTestAsync(PatientId, new
            {
                TestName = NewTestName,
                LoincCode = NewLoincCode,
                Priority = NewPriority
            });
            NewTestName = string.Empty;
            NewLoincCode = string.Empty;
            ShowOrderForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
