// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class NotesViewModel : ChartTabViewModelBase
{
    public ObservableCollection<NoteDto> Notes { get; } = new();

    [ObservableProperty] private NoteDto? _selectedNote;
    [ObservableProperty] private bool _showCreateForm;
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private string _newDocumentType = "PROGRESS NOTE";
    [ObservableProperty] private string _newNoteText = string.Empty;
    [ObservableProperty] private string _newAuthor = string.Empty;

    public string[] DocumentTypes { get; } =
        ["PROGRESS NOTE", "DISCHARGE SUMMARY", "CONSULT NOTE",
         "HISTORY & PHYSICAL", "PROCEDURE NOTE", "ADDENDUM"];

    public NotesViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Api.GetNotesAsync(PatientId);
        Notes.Clear();
        foreach (var n in items) Notes.Add(n);
    }

    protected override void ClearData() { Notes.Clear(); HistoryNotes.Clear(); SelectedNote = null; }

    [RelayCommand]
    private void ToggleCreateForm() => ShowCreateForm = !ShowCreateForm;

    [RelayCommand]
    private async Task CreateNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewNoteText)) return;
        ErrorText = string.Empty;
        try
        {
            await Api.CreateNoteAsync(PatientId, new
            {
                Title = NewTitle,
                DocumentType = NewDocumentType,
                NoteText = NewNoteText,
                AuthorName = NewAuthor
            });
            NewTitle = string.Empty;
            NewNoteText = string.Empty;
            NewAuthor = string.Empty;
            ShowCreateForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task SignNoteAsync()
    {
        if (SelectedNote == null) return;
        ErrorText = string.Empty;
        try
        {
            await Api.SignNoteAsync(PatientId, SelectedNote.DocumentId, new
            {
                ElectronicSignature = "CPRS-ES"
            });
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    // Note History
    public ObservableCollection<NoteDto> HistoryNotes { get; } = new();
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
    private async Task LoadHistoryAsync()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        ErrorText = string.Empty;
        try
        {
            var items = await Api.GetNoteHistoryAsync(PatientId, HistoryFrom, HistoryTo);
            HistoryNotes.Clear();
            foreach (var n in items) HistoryNotes.Add(n);
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
