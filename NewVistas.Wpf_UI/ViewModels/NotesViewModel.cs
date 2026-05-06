// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
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

    public NotesViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

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

    [RelayCommand]
    private async Task SignNote()
    {
        if (SelectedNote is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.SignNoteAsync(SelectedNote.DocumentId);
            await LoadDataAsync();
        }
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
