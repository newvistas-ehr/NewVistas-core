// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Patient Advocate module — placeholder view.
/// Full functionality available via the web interface.
/// </summary>
public partial class PatientAdvocateViewModel : ObservableObject
{
    public PatientAdvocateViewModel(ApiClient api, OrleansGrainService grains) { }
}
