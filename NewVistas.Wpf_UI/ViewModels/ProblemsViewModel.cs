// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ProblemsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ProblemSummary> _problems = new();
    [ObservableProperty] private bool _showActiveOnly = true;
    [ObservableProperty] private ProblemSummary? _selectedProblem;

    // Add form
    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _diagnosisCode = string.Empty;
    [ObservableProperty] private string _priority = "CHRONIC";
    [ObservableProperty] private DateTime? _dateOfOnset;
    [ObservableProperty] private bool _isServiceConnected;

    public string[] PriorityOptions { get; } = ["ACUTE", "CHRONIC"];

    // ── Diagnostic stewardship (ADR-006) ────────────────────────────────────
    // The advisory is PULL, NOT PUSH: a collapsed line the clinician chooses to open. It never
    // blocks saving and never steals focus. The self-suppressing thresholds are the
    // alert-fatigue control — only diagnoses that clear them show anything at all.

    [ObservableProperty] private DiagnosisRevisionAdvisory? _advisory;
    [ObservableProperty] private bool _advisoryExpanded;

    /// <summary>Alternatives this diagnosis turns out to be; empty when nothing to say.</summary>
    [ObservableProperty] private ObservableCollection<DiagnosisAlternative> _advisoryAlternatives = new();

    /// <summary>Discriminating tests worth considering.</summary>
    [ObservableProperty] private ObservableCollection<DiagnosticTestSuggestion> _advisoryTests = new();

    /// <summary>Problem ids with an unadjudicated episode, so only those offer the prompt.</summary>
    private readonly HashSet<string> _openEpisodeProblemIds = new(StringComparer.Ordinal);

    // Adjudication prompt
    [ObservableProperty] private bool _showAdjudicate;
    [ObservableProperty] private ProblemSummary? _adjudicating;
    [ObservableProperty] private string _adjudicateOutcome = OutcomeConfirmed;
    [ObservableProperty] private string _adjudicateOutcomeCode = string.Empty;
    [ObservableProperty] private string _adjudicateOutcomeDisplay = string.Empty;
    [ObservableProperty] private string _adjudicateNote = string.Empty;
    [ObservableProperty] private string? _adjudicateProposal;

    private const string OutcomeConfirmed = "It was right";
    private const string OutcomeRefined = "Same condition, more specific";
    private const string OutcomeBroadened = "Same condition, less specific";
    private const string OutcomeRevised = "It was something else";
    private const string OutcomeResolved = "Resolved, never established";
    private const string OutcomeUnknown = "Don't know / lost to follow-up";

    public string[] OutcomeOptions { get; } =
    [
        OutcomeConfirmed, OutcomeRefined, OutcomeBroadened,
        OutcomeRevised, OutcomeResolved, OutcomeUnknown
    ];

    public bool AdvisoryVisible
        => Advisory is not null
           && (Advisory.RevisionRate is not null
               || AdvisoryAlternatives.Count > 0
               || AdvisoryTests.Count > 0);

    /// <summary>True when the outcome needs a code — the "what was it actually" picker.</summary>
    public bool AdjudicateNeedsCode
        => AdjudicateOutcome is OutcomeRevised or OutcomeRefined or OutcomeBroadened;

    public ProblemsViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = ShowActiveOnly
            ? await workflow.GetActiveProblemsAsync()
            : await workflow.GetAllProblemsAsync();

        Problems.Clear();
        foreach (var p in list) Problems.Add(p);

        await LoadOpenEpisodesAsync(workflow);
    }

    /// <summary>
    /// Which problems still have an unadjudicated episode. Degrades to "none" when diagnostic
    /// stewardship is off — the chart must not depend on an optional module.
    /// </summary>
    private async Task LoadOpenEpisodesAsync(IPatientWorkflowGrain workflow)
    {
        _openEpisodeProblemIds.Clear();
        try
        {
            List<DiagnosticEpisode> episodes = await workflow.GetDiagnosticEpisodesAsync();
            foreach (DiagnosticEpisode e in episodes)
                if (e.Outcome == DiagnosticEpisodeOutcome.Open)
                    _openEpisodeProblemIds.Add(e.ProblemId);
        }
        catch { /* stewardship unavailable — no prompts, chart still works */ }
        OnPropertyChanged(nameof(CanAdjudicateSelected));
    }

    public bool CanAdjudicateSelected
        => SelectedProblem is not null && _openEpisodeProblemIds.Contains(SelectedProblem.ProblemId);

    partial void OnSelectedProblemChanged(ProblemSummary? value)
        => OnPropertyChanged(nameof(CanAdjudicateSelected));

    partial void OnAdjudicateOutcomeChanged(string value)
        => OnPropertyChanged(nameof(AdjudicateNeedsCode));

    /// <summary>
    /// Refresh the advisory as the clinician settles on a code. Fires at assertion time — the
    /// only moment the advice can still change the decision. Adjudication-time display would
    /// arrive to congratulate a correction already made.
    /// </summary>
    partial void OnDiagnosisCodeChanged(string value) => _ = RefreshAdvisoryAsync(value);

    private async Task RefreshAdvisoryAsync(string code)
    {
        code = (code ?? string.Empty).Trim();
        if (code.Length < 3 || !HasPatient)
        {
            Advisory = null;
            AdvisoryAlternatives.Clear();
            AdvisoryTests.Clear();
            OnPropertyChanged(nameof(AdvisoryVisible));
            return;
        }

        try
        {
            // ProblemId is deliberately null: the problem does not exist yet, and marking
            // exposure while someone browses codes would poison the unexposed comparison arm
            // that the reported rate is computed from.
            DiagnosisRevisionAdvisory a = await Grains.GetGrain<IPatientWorkflowGrain>(PatientId)
                .GetDiagnosisRevisionAdvisoryAsync(code, Diagnosis ?? code, null);

            Advisory = a;
            AdvisoryAlternatives.Clear();
            foreach (DiagnosisAlternative alt in a.Alternatives) AdvisoryAlternatives.Add(alt);
            AdvisoryTests.Clear();
            foreach (DiagnosticTestSuggestion t in a.SuggestedTests) AdvisoryTests.Add(t);
        }
        catch
        {
            // Decoration on a clinical screen. Silence is the correct degraded state.
            Advisory = null;
            AdvisoryAlternatives.Clear();
            AdvisoryTests.Clear();
        }
        OnPropertyChanged(nameof(AdvisoryVisible));
    }

    [RelayCommand]
    private void ToggleAdvisory() => AdvisoryExpanded = !AdvisoryExpanded;

    [RelayCommand]
    private void ToggleAddForm() => ShowAddForm = !ShowAddForm;

    [RelayCommand]
    private async Task AddProblem()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(Diagnosis)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AddProblemAsync(
                Diagnosis,
                DiagnosisCode.Length > 0 ? DiagnosisCode : null,
                null, // condition
                Priority,
                DateOfOnset,
                null, null, // provider
                null, null, // clinic
                IsServiceConnected,
                null); // comments
            ShowAddForm = false;
            Diagnosis = string.Empty;
            DiagnosisCode = string.Empty;
            Advisory = null;
            AdvisoryAlternatives.Clear();
            AdvisoryTests.Clear();
            OnPropertyChanged(nameof(AdvisoryVisible));
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    // ── Adjudication ────────────────────────────────────────────────────────

    [RelayCommand]
    private void BeginAdjudicate()
    {
        if (SelectedProblem is null) return;
        Adjudicating = SelectedProblem;
        AdjudicateOutcome = OutcomeConfirmed;
        AdjudicateOutcomeCode = string.Empty;
        AdjudicateOutcomeDisplay = string.Empty;
        AdjudicateNote = string.Empty;
        AdjudicateProposal = null;
        ShowAdjudicate = true;
    }

    [RelayCommand]
    private void CancelAdjudicate()
    {
        // Skipping must always be free. A required field yields default-clicked answers, which
        // are worse than no answer because they cannot be told apart from real ones.
        ShowAdjudicate = false;
        Adjudicating = null;
    }

    /// <summary>
    /// Offer the system's reading of the code change as a default the clinician can override.
    /// Uses the same shared rule the server uses, so client and server cannot drift apart.
    /// </summary>
    partial void OnAdjudicateOutcomeCodeChanged(string value)
    {
        if (Adjudicating is null || string.IsNullOrWhiteSpace(value)) { AdjudicateProposal = null; return; }

        DiagnosticEpisodeOutcome suggested =
            DiagnosisCodeRelation.Propose(Adjudicating.DiagnosisCode, value);

        AdjudicateOutcome = suggested switch
        {
            DiagnosticEpisodeOutcome.Refined => OutcomeRefined,
            DiagnosticEpisodeOutcome.Broadened => OutcomeBroadened,
            DiagnosticEpisodeOutcome.Revised => OutcomeRevised,
            DiagnosticEpisodeOutcome.Confirmed => OutcomeConfirmed,
            _ => AdjudicateOutcome
        };
        AdjudicateProposal = suggested == DiagnosticEpisodeOutcome.Open
            ? null
            : $"Suggested: {AdjudicateOutcome.ToLowerInvariant()}. Change it if that is wrong.";
    }

    [RelayCommand]
    private async Task SubmitAdjudication()
    {
        if (Adjudicating is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            DiagnosticEpisodeOutcome outcome = AdjudicateOutcome switch
            {
                OutcomeRefined => DiagnosticEpisodeOutcome.Refined,
                OutcomeBroadened => DiagnosticEpisodeOutcome.Broadened,
                OutcomeRevised => DiagnosticEpisodeOutcome.Revised,
                OutcomeResolved => DiagnosticEpisodeOutcome.ResolvedWithoutAlternate,
                OutcomeUnknown => DiagnosticEpisodeOutcome.ClosedUnadjudicated,
                _ => DiagnosticEpisodeOutcome.Confirmed
            };

            RevisionReason? reason = outcome switch
            {
                DiagnosticEpisodeOutcome.Revised => RevisionReason.Correction,
                DiagnosticEpisodeOutcome.Refined or DiagnosticEpisodeOutcome.Broadened
                    => RevisionReason.Refinement,
                DiagnosticEpisodeOutcome.ResolvedWithoutAlternate => RevisionReason.Resolution,
                _ => null
            };

            await Grains.GetGrain<IPatientWorkflowGrain>(PatientId)
                .AdjudicateDiagnosticEpisodeAsync(
                    Adjudicating.ProblemId, outcome,
                    AdjudicateOutcomeCode.Length > 0 ? AdjudicateOutcomeCode : null,
                    AdjudicateOutcomeDisplay.Length > 0 ? AdjudicateOutcomeDisplay : null,
                    reason,
                    AdjudicateNote.Length > 0 ? AdjudicateNote : null);

            ShowAdjudicate = false;
            Adjudicating = null;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
