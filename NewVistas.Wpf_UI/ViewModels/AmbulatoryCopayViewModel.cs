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

public partial class AmbulatoryCopayViewModel : BasePatientViewModel
{
    [ObservableProperty] private AmbulatoryCopaySheetState? _sheet;
    [ObservableProperty] private string _sheetIdInput = string.Empty;
    [ObservableProperty] private string? _actionMessage;

    // Check item fields
    [ObservableProperty] private string _checkItemCode = string.Empty;
    [ObservableProperty] private string _checkItemDesc = string.Empty;
    [ObservableProperty] private string _checkUserId = string.Empty;
    [ObservableProperty] private string _checkUserName = string.Empty;

    public AmbulatoryCopayViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        if (string.IsNullOrWhiteSpace(SheetIdInput)) return;
        var grain = Grains.GetGrain<IAmbulatoryCopaySheetGrain>($"IB-SHEET:{SheetIdInput.Trim()}");
        Sheet = await grain.GetAsync();
    }

    [RelayCommand]
    private async Task CheckItem()
    {
        if (Sheet is null || string.IsNullOrWhiteSpace(CheckItemCode)) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IAmbulatoryCopaySheetGrain>($"IB-SHEET:{Sheet.SheetId}");
            await grain.CheckItemAsync(CheckItemCode, CheckItemDesc, CheckUserId, CheckUserName);
            ActionMessage = $"Item {CheckItemCode} checked.";
            CheckItemCode = string.Empty; CheckItemDesc = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteSheet()
    {
        if (Sheet is null) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IAmbulatoryCopaySheetGrain>($"IB-SHEET:{Sheet.SheetId}");
            await grain.CompleteAsync(CheckUserId, CheckUserName);
            ActionMessage = "Sheet marked complete.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
