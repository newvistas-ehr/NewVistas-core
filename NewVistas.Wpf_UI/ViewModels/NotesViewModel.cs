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

public partial class NotesViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<TiuNoteSummary> _notes = new();
    [ObservableProperty] private TiuDocumentState? _selectedNote;

    /// <summary>List selection; loads the full document into <see cref="SelectedNote"/>.</summary>
    [ObservableProperty] private TiuNoteSummary? _selectedNoteSummary;

    partial void OnSelectedNoteSummaryChanged(TiuNoteSummary? value)
    {
        // Actions gate on the detail object; clear it before the async fetch so they can never target the previously selected record.
        SelectedNote = null;
        if (value is not null) _ = SelectNote(value);
    }

    // Create note form
    [ObservableProperty] private bool _showCreateForm;
    [ObservableProperty] private string _documentType = "PROGRESS NOTE";
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _reportText = string.Empty;
    [ObservableProperty] private string _authorName = "Provider, Test";

    public string[] DocumentTypes { get; } = [
        "PROGRESS NOTE", "DISCHARGE SUMMARY", "CONSULT", "HISTORY & PHYSICAL",
        "OPERATIVE REPORT", "RADIOLOGY REPORT", "EMERGENCY DEPARTMENT NOTE"
    ];

    public NotesViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetNotesAsync(null, 50);
        Notes.Clear();
        foreach (var n in list) Notes.Add(n);
    }

    [RelayCommand]
    private async Task SelectNote(TiuNoteSummary note)
    {
        if (!HasPatient) return;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedNote = await workflow.GetNoteAsync(note.DocumentId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleCreateForm() => ShowCreateForm = !ShowCreateForm;

    [RelayCommand]
    private async Task CreateNote()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ReportText)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateNoteAsync(
                DocumentType,
                null, // documentTypeId
                ReportText,
                Subject.Length > 0 ? Subject : null,
                null, AuthorName, // author
                null, null, // cosigner
                null, null, // location
                null, // visitId
                DateTime.UtcNow);
            ShowCreateForm = false;
            ReportText = string.Empty;
            Subject = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>The user's e-signature code, verified by the workflow grain on sign.</summary>
    [ObservableProperty] private string _signatureCode = string.Empty;

    [RelayCommand]
    private async Task SignNote()
    {
        if (SelectedNote is null || !HasPatient) return;
        if (string.IsNullOrWhiteSpace(SignatureCode))
        {
            Error = "Enter your electronic signature code to sign.";
            return;
        }
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            // The grain verifies the code against the signed-in user's stored hash — this
            // client previously signed with a hardcoded placeholder string.
            await workflow.SignNoteAsync(SelectedNote.DocumentId, SignatureCode);
            SignatureCode = string.Empty;
            await LoadDataAsync();
        }
        catch (UnauthorizedAccessException) { Error = "That electronic signature code was not accepted."; }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    // Note History
    [ObservableProperty] private ObservableCollection<TiuNoteSummary> _historyNotes = new();
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddDays(-90);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;

    [RelayCommand]
    private void ToggleHistory()
    {
        ShowHistory = !ShowHistory;
        if (ShowHistory) ShowCreateForm = false;
    }

    [RelayCommand]
    private async Task LoadHistory()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var list = await workflow.GetNoteHistoryAsync(HistoryFrom, HistoryTo, 100);
            HistoryNotes.Clear();
            foreach (var n in list) HistoryNotes.Add(n);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
