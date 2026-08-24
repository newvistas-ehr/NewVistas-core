// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class ProblemsViewModel : ChartTabViewModelBase
{
    public ObservableCollection<ProblemDto> Problems { get; } = new();

    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private string _newIcdCode = string.Empty;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private string _newOnsetDate = string.Empty;
    [ObservableProperty] private string _newStatus = "ACTIVE";

    public ProblemsViewModel(ChartDataService data, PatientContext context) : base(data, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Data.GetProblemsAsync(PatientId);
        Problems.Clear();
        foreach (var p in items) Problems.Add(p);
    }

    protected override void ClearData() => Problems.Clear();

    [RelayCommand]
    private void ToggleAddForm() => ShowAddForm = !ShowAddForm;

    [RelayCommand]
    private async Task AddProblemAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDescription)) return;
        ErrorText = string.Empty;
        try
        {
            await Data.AddProblemAsync(PatientId, NewDescription, NewIcdCode, ParseDate(NewOnsetDate));
            NewIcdCode = string.Empty;
            NewDescription = string.Empty;
            NewOnsetDate = string.Empty;
            ShowAddForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    /// <summary>Onset is a free-text box; treat anything unparseable as "not stated".</summary>
    private static DateTime? ParseDate(string? text) =>
        DateTime.TryParse(text, out DateTime d) ? d : null;
}
