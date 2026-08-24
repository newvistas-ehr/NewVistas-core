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

public partial class CollectionLettersViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<CollectionLetterIndexEntry> _letters = new();
    [ObservableProperty] private CollectionLetterState? _selectedLetter;
    [ObservableProperty] private string? _actionMessage;

    /// <summary>Grid selection; loads the full letter into <see cref="SelectedLetter"/>.</summary>
    [ObservableProperty] private CollectionLetterIndexEntry? _selectedEntry;

    partial void OnSelectedEntryChanged(CollectionLetterIndexEntry? value)
    {
        // Actions gate on the detail object; clear it before the async fetch so they can never target the previously selected record.
        SelectedLetter = null;
        if (value is not null) _ = ViewDetail(value);
    }

    public CollectionLettersViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<CollectionLetterIndexEntry> list = await workflow.GetCollectionLettersAsync();
        Letters.Clear();
        foreach (var l in list) Letters.Add(l);
        SelectedLetter = null;
    }

    [RelayCommand]
    private async Task ViewDetail(CollectionLetterIndexEntry entry)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedLetter = await workflow.GetCollectionLetterAsync(entry.LetterId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task GenerateLetter()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.GenerateCollectionLetterAsync(CollectionLetterType.FirstNotice, null, null, null);
            ActionMessage = $"Letter generated: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task MarkSent()
    {
        if (SelectedLetter is null) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.MarkCollectionLetterMailedAsync(SelectedLetter.LetterId);
            ActionMessage = "Letter marked as sent.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
