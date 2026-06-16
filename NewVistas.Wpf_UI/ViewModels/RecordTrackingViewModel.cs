// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Wpf_UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Record Tracking / HIM module — placeholder view.
/// Full functionality available via the web interface.
/// </summary>
public partial class RecordTrackingViewModel : ObservableObject
{
    public RecordTrackingViewModel(ApiClient api, OrleansGrainService grains) { }
}
