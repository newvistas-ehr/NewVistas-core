// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Security;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

/// <summary>
/// Shell ViewModel — owns the CPRS-style main frame:
/// patient toolbar, chart tabs, menu, and status bar.
/// Mirrors fFrame.pas in the original Delphi CPRS.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    // ── Patient toolbar (pnlToolbar in fFrame.dfm) ────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatient))]
    private string _patientDisplayName = string.Empty;

    [ObservableProperty]
    private string _patientInfoLine = string.Empty;   // DOB  SSN  Age  Sex

    [ObservableProperty]
    private string _visitLocation = string.Empty;

    [ObservableProperty]
    private string _visitProvider = string.Empty;

    [ObservableProperty]
    private string _primaryProvider = string.Empty;

    [ObservableProperty]
    private string _attendingProvider = string.Empty;

    [ObservableProperty]
    private string _remindersCount = "0";

    [ObservableProperty]
    private string _remindersColor = "#888888";

    // ── Tab control ───────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedChartTab;

    // ── Status bar (stsArea in fFrame.dfm) ────────────────────────────────

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _currentUser = "Not logged in";

    [ObservableProperty]
    private string _windowTitle = "CPRS Chart — NewVistas";

    // ── Chart tab ViewModels (lazy, one per session) ───────────────────────

    private CoverSheetViewModel?  _coverSheetVm;
    private ProblemsViewModel?    _problemsVm;
    private MedicationsViewModel? _medicationsVm;
    private OrdersViewModel?      _ordersVm;
    private NotesViewModel?       _notesVm;
    private ConsultsViewModel?    _consultsVm;
    private LabsViewModel?        _labsVm;
    private VitalsViewModel?      _vitalsVm;
    private SurgeryViewModel?     _surgeryVm;
    private ReportsViewModel?     _reportsVm;

    public CoverSheetViewModel  CoverSheetVm  => _coverSheetVm  ??= _services.GetRequiredService<CoverSheetViewModel>();
    public ProblemsViewModel    ProblemsVm    => _problemsVm    ??= _services.GetRequiredService<ProblemsViewModel>();
    public MedicationsViewModel MedicationsVm => _medicationsVm ??= _services.GetRequiredService<MedicationsViewModel>();
    public OrdersViewModel      OrdersVm      => _ordersVm      ??= _services.GetRequiredService<OrdersViewModel>();
    public NotesViewModel       NotesVm       => _notesVm       ??= _services.GetRequiredService<NotesViewModel>();
    public ConsultsViewModel    ConsultsVm    => _consultsVm    ??= _services.GetRequiredService<ConsultsViewModel>();
    public LabsViewModel        LabsVm        => _labsVm        ??= _services.GetRequiredService<LabsViewModel>();
    public VitalsViewModel      VitalsVm      => _vitalsVm      ??= _services.GetRequiredService<VitalsViewModel>();
    public SurgeryViewModel     SurgeryVm     => _surgeryVm     ??= _services.GetRequiredService<SurgeryViewModel>();
    public ReportsViewModel     ReportsVm     => _reportsVm     ??= _services.GetRequiredService<ReportsViewModel>();

    public bool HasPatient => !string.IsNullOrWhiteSpace(PatientDisplayName);

    // ── Role-based tab visibility ─────────────────────────────────────────
    // Precomputed at login from security keys. O(1) checks.

    public bool ShowClinicalTabs => _auth.HasMenuAccess(MenuArea.Clinical);
    public bool ShowSurgeryTab => _auth.HasMenuAccess(MenuArea.Surgery) || _auth.HasMenuAccess(MenuArea.Clinical);
    public bool ShowLabsTab => _auth.HasMenuAccess(MenuArea.Laboratory) || _auth.HasMenuAccess(MenuArea.Clinical);

    public PatientContext PatientContext { get; }
    private readonly AuthService _auth;

    public MainViewModel(IServiceProvider services, PatientContext patientContext, AuthService authService)
    {
        _services      = services;
        _auth          = authService;
        PatientContext = patientContext;

        PatientContext.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PatientContext.PatientId))
                OnPatientChanged();
        };
    }

    private void OnPatientChanged()
    {
        string pid  = PatientContext.PatientId.Trim();
        string name = PatientContext.PatientName;

        if (string.IsNullOrWhiteSpace(pid))
        {
            PatientDisplayName = string.Empty;
            PatientInfoLine    = string.Empty;
            VisitLocation      = string.Empty;
            VisitProvider      = string.Empty;
            PrimaryProvider    = string.Empty;
            AttendingProvider  = string.Empty;
            RemindersCount     = "0";
            RemindersColor     = "#888888";
            WindowTitle        = "CPRS Chart — NewVistas";
            StatusText         = "No patient selected";
        }
        else
        {
            string dob = PatientContext.PatientDob;
            string ssn = PatientContext.PatientSsn;
            string age = PatientContext.PatientAge;
            string sex = PatientContext.PatientSex;

            PatientDisplayName = string.IsNullOrWhiteSpace(name) ? pid : name;
            PatientInfoLine    = $"DOB: {dob}   SSN: {ssn}   Age: {age}   Sex: {sex}";
            PrimaryProvider    = PatientContext.PrimaryProvider;
            AttendingProvider  = PatientContext.Attending;
            WindowTitle        = $"CPRS Chart — {PatientDisplayName}";
            StatusText         = $"Patient: {PatientDisplayName}";
            SelectedChartTab   = 0;   // jump to Cover Sheet on new patient
        }

        OnPropertyChanged(nameof(HasPatient));
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [RelayCommand]
    public void SelectPatient()
    {
        var vm  = _services.GetRequiredService<PatientSelectionViewModel>();
        var dlg = new Windows.PatientSelectionWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dlg.ShowDialog();
        // PatientContext is updated inside the dialog VM on OK
    }

    [RelayCommand]
    public void RefreshPatient()
    {
        if (!HasPatient) return;
        StatusText = "Refreshing...";
        // Tab views reload themselves when IsLoaded is reset
        _coverSheetVm?.Refresh();
        StatusText = $"Patient: {PatientDisplayName}";
    }

    [RelayCommand]
    public void NavigateToTab(string tabIndexStr)
    {
        if (int.TryParse(tabIndexStr, out int idx))
            SelectedChartTab = idx;
    }

    [RelayCommand]
    public void SetFontSize(string sizeStr)
    {
        if (double.TryParse(sizeStr, out double size))
            System.Windows.Application.Current.MainWindow.FontSize = size;
    }

    [RelayCommand]
    public void ExitApplication()
        => System.Windows.Application.Current.Shutdown();

    [RelayCommand]
    public void ShowAbout()
    {
        System.Windows.MessageBox.Show(
            "NewVistas — Clinical Information System\n\nCPRS-compatible WPF client\nCopyright 2026 Merrimack Valley Software Works, LLC",
            "About NewVistas",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
