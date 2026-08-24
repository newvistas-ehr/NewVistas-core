// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// PT Measurement Wizard — multi-step flow for capturing ROM and strength
/// measurements across multiple body groups in a single session.
/// Page 0 is region selection; pages 1..N are per-body-group measurement grids.
/// </summary>
public partial class PTMeasurementWizardViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<RegionSelectionItem> _regionSelections = new();
    [ObservableProperty] private List<WizardMeasurementPage> _measurementPages = new();
    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private string _therapistName = string.Empty;
    [ObservableProperty] private string _locationName = string.Empty;
    [ObservableProperty] private string _globalNotes = string.Empty;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _saveError;

    /// <summary>Fired when user cancels or save completes — navigate back to PT Hub.</summary>
    public event Action? BackToHubRequested;

    public PTMeasurementWizardViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext)
    {
        RegionSelections = new ObservableCollection<RegionSelectionItem>
        {
            new("Spine", false,
                [BodyGroup.Cervical, BodyGroup.ThoracicSpine, BodyGroup.LumbarSpine]),
            new("Upper Extremity", true,
                [BodyGroup.Shoulder, BodyGroup.Elbow, BodyGroup.Wrist, BodyGroup.Hand]),
            new("Lower Extremity", true,
                [BodyGroup.Hip, BodyGroup.Knee, BodyGroup.Ankle, BodyGroup.Foot]),
            new("TMJ", false,
                [BodyGroup.TMJ]),
        };
    }

    public bool IsOnSelectionPage => CurrentStepIndex == 0;
    public bool IsOnMeasurementPage => CurrentStepIndex > 0;

    public WizardMeasurementPage? CurrentMeasurementPage =>
        IsOnMeasurementPage && CurrentStepIndex - 1 < MeasurementPages.Count
            ? MeasurementPages[CurrentStepIndex - 1]
            : null;

    public string StepLabel =>
        IsOnMeasurementPage
            ? $"Page {CurrentStepIndex} of {MeasurementPages.Count}"
            : string.Empty;

    public bool CanGoBack => CurrentStepIndex > 0;
    public bool CanGoNext => IsOnMeasurementPage && CurrentStepIndex < MeasurementPages.Count;

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsOnSelectionPage));
        OnPropertyChanged(nameof(IsOnMeasurementPage));
        OnPropertyChanged(nameof(CurrentMeasurementPage));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
    }

    protected override Task LoadDataAsync() => Task.CompletedTask;

    [RelayCommand]
    private void ProceedToMeasurements()
    {
        Error = null;
        bool anySelected = false;
        foreach (var r in RegionSelections)
        {
            if (r.IsSelected) { anySelected = true; break; }
        }
        if (!anySelected)
        {
            Error = "Select at least one body region to measure.";
            return;
        }

        var pages = new List<WizardMeasurementPage>();
        foreach (var region in RegionSelections)
        {
            if (!region.IsSelected) continue;
            foreach (var bodyGroup in region.BodyGroups)
            {
                if (!region.HasLaterality)
                {
                    pages.Add(new WizardMeasurementPage(bodyGroup, Laterality.Bilateral));
                }
                else
                {
                    Laterality side = region.SelectedLaterality;
                    if (side == Laterality.Bilateral)
                    {
                        pages.Add(new WizardMeasurementPage(bodyGroup, Laterality.Right));
                        pages.Add(new WizardMeasurementPage(bodyGroup, Laterality.Left));
                    }
                    else
                    {
                        pages.Add(new WizardMeasurementPage(bodyGroup, side));
                    }
                }
            }
        }

        MeasurementPages = pages;
        OnPropertyChanged(nameof(CanGoNext));
        CurrentStepIndex = 1;
    }

    [RelayCommand]
    private async Task GoNext()
    {
        if (CurrentStepIndex < MeasurementPages.Count)
        {
            CurrentStepIndex++;
        }
        else
        {
            // Past last page — auto-save
            await SaveAllAsync();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStepIndex > 0)
            CurrentStepIndex--;
    }

    [RelayCommand]
    private async Task Done() => await SaveAllAsync();

    [RelayCommand]
    private void Cancel() => BackToHubRequested?.Invoke();

    private async Task SaveAllAsync()
    {
        if (!HasPatient) return;
        IsSaving = true;
        SaveError = null;
        try
        {
            // Pre-validate all MMT grades before making any grain calls
            var pageSessions = new List<(WizardMeasurementPage Page, List<RomMeasurement> Rom, List<StrengthMeasurement> Str)>();
            foreach (var page in MeasurementPages)
            {
                var rom = new List<RomMeasurement>();
                var str = new List<StrengthMeasurement>();

                foreach (var row in page.MovementRows)
                {
                    decimal? active = ParseDecimal(row.ActiveRom);
                    decimal? passive = ParseDecimal(row.PassiveRom);

                    if (active.HasValue || passive.HasValue || !string.IsNullOrWhiteSpace(row.Pain))
                    {
                        rom.Add(new RomMeasurement
                        {
                            Movement = row.Movement,
                            ActiveRom = active,
                            PassiveRom = passive,
                            PainOnMotion = row.Pain
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(row.StrengthGrade))
                    {
                        var parsed = BodyGroupDefinitions.ParseMmtGrade(row.StrengthGrade);
                        if (parsed == null)
                        {
                            SaveError = $"Invalid strength grade '{row.StrengthGrade}' for {row.DisplayName} on {page.PageTitle}.";
                            return;
                        }
                        str.Add(new StrengthMeasurement
                        {
                            Movement = row.Movement,
                            Grade = parsed.Value.grade,
                            GradeDisplay = parsed.Value.display,
                            Comments = row.StrengthComments
                        });
                    }
                }

                if (rom.Count > 0 || str.Count > 0)
                    pageSessions.Add((page, rom, str));
            }

            if (pageSessions.Count == 0)
            {
                // Nothing entered — just go back
                BackToHubRequested?.Invoke();
                return;
            }

            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            DateTime sessionDate = DateTime.UtcNow;

            foreach (var (page, rom, str) in pageSessions)
            {
                string? notes = !string.IsNullOrWhiteSpace(page.SessionNotes)
                    ? page.SessionNotes
                    : (!string.IsNullOrWhiteSpace(GlobalNotes) ? GlobalNotes : null);

                await grain.RecordBodyGroupSessionAsync(
                    page.BodyGroup, sessionDate,
                    null, TherapistName,
                    null, LocationName,
                    page.Side,
                    rom, str,
                    notes);
            }

            BackToHubRequested?.Invoke();
        }
        catch (Exception ex)
        {
            SaveError = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static decimal? ParseDecimal(string? s) =>
        !string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out var d) ? d : null;
}

/// <summary>
/// A region group on the wizard selection page (e.g., "Upper Extremity").
/// </summary>
public partial class RegionSelectionItem : ObservableObject
{
    public string DisplayName { get; }
    public bool HasLaterality { get; }
    public List<BodyGroup> BodyGroups { get; }
    public string BodyGroupList { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isRight;
    [ObservableProperty] private bool _isLeft;
    [ObservableProperty] private bool _isBoth = true;

    public RegionSelectionItem(string displayName, bool hasLaterality, List<BodyGroup> bodyGroups)
    {
        DisplayName = displayName;
        HasLaterality = hasLaterality;
        BodyGroups = bodyGroups;
        BodyGroupList = string.Join(", ", bodyGroups.Select(FormatBodyGroup));
    }

    public bool ShowLaterality => HasLaterality && IsSelected;

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(ShowLaterality));

    public Laterality SelectedLaterality =>
        IsRight ? Laterality.Right :
        IsLeft ? Laterality.Left :
        Laterality.Bilateral;

    private static string FormatBodyGroup(BodyGroup bg) => bg switch
    {
        BodyGroup.Cervical => "Cervical",
        BodyGroup.ThoracicSpine => "Thoracic Spine",
        BodyGroup.LumbarSpine => "Lumbar Spine",
        BodyGroup.TMJ => "TMJ",
        _ => bg.ToString()
    };
}

/// <summary>
/// One measurement page in the wizard — represents a body group + side combination.
/// </summary>
public partial class WizardMeasurementPage : ObservableObject
{
    public BodyGroup BodyGroup { get; }
    public Laterality Side { get; }
    public string PageTitle { get; }
    public ObservableCollection<MovementRow> MovementRows { get; } = new();

    [ObservableProperty] private string _sessionNotes = string.Empty;

    public WizardMeasurementPage(BodyGroup bodyGroup, Laterality side)
    {
        BodyGroup = bodyGroup;
        Side = side;
        PageTitle = FormatPageTitle(bodyGroup, side);

        var movements = BodyGroupDefinitions.GetMovements(bodyGroup);
        foreach (var m in movements)
        {
            var normalRange = BodyGroupDefinitions.GetNormalRomRange(bodyGroup, m);
            MovementRows.Add(new MovementRow(m, FormatMovement(m), normalRange));
        }
    }

    private static string FormatPageTitle(BodyGroup bg, Laterality side)
    {
        string name = bg switch
        {
            BodyGroup.Cervical => "Cervical (Neck)",
            BodyGroup.ThoracicSpine => "Thoracic Spine",
            BodyGroup.LumbarSpine => "Lumbar Spine",
            BodyGroup.TMJ => "TMJ",
            _ => bg.ToString()
        };
        return side == Laterality.Bilateral ? name : $"{side} {name}";
    }

    private static string FormatMovement(Movement m) =>
        System.Text.RegularExpressions.Regex.Replace(m.ToString(), "(\\B[A-Z])", " $1");
}
