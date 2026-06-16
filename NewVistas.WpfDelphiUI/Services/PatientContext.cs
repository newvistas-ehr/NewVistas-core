// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;

namespace NewVistas.WpfDelphiUI.Services;

/// <summary>
/// Shared singleton that tracks which patient is currently selected.
/// All ViewModels observe PatientId changes to load their data.
/// Equivalent to CPRS's global patient context (CPRS fFrame patient state).
/// </summary>
public partial class PatientContext : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPatient))]
    private string _patientId = string.Empty;

    [ObservableProperty]
    private string _patientName = string.Empty;

    [ObservableProperty]
    private string _patientDob = string.Empty;

    [ObservableProperty]
    private string _patientSsn = string.Empty;

    [ObservableProperty]
    private string _patientSex = string.Empty;

    [ObservableProperty]
    private string _patientAge = string.Empty;

    [ObservableProperty]
    private bool _isVeteran;

    [ObservableProperty]
    private string _serviceConnectedPercent = string.Empty;

    [ObservableProperty]
    private string _ward = string.Empty;

    [ObservableProperty]
    private string _roomBed = string.Empty;

    [ObservableProperty]
    private string _primaryProvider = string.Empty;

    [ObservableProperty]
    private string _attending = string.Empty;

    [ObservableProperty]
    private string _lastVisit = string.Empty;

    public bool HasPatient => !string.IsNullOrWhiteSpace(PatientId);

    public void Clear()
    {
        PatientId              = string.Empty;
        PatientName            = string.Empty;
        PatientDob             = string.Empty;
        PatientSsn             = string.Empty;
        PatientSex             = string.Empty;
        PatientAge             = string.Empty;
        IsVeteran              = false;
        ServiceConnectedPercent = string.Empty;
        Ward                   = string.Empty;
        RoomBed                = string.Empty;
        PrimaryProvider        = string.Empty;
        Attending              = string.Empty;
        LastVisit              = string.Empty;
        OnPropertyChanged(nameof(HasPatient));
    }
}
