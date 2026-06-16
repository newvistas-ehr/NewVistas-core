// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
