// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Wpf_UI.Services;
using Orleans;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Pharmacy Hub — a landing page summarizing all pharmacy sub-modules.
/// No grain calls; just descriptive content directing users to sub-modules.
/// </summary>
public partial class PharmacyHubViewModel : ObservableObject
{
    public PharmacyHubViewModel() { }
}
