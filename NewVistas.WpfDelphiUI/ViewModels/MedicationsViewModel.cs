// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed class MedicationsViewModel : ChartTabViewModelBase
{
    public ObservableCollection<PrescriptionDto> Prescriptions { get; } = new();

    public MedicationsViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Api.GetPrescriptionsAsync(PatientId);
        Prescriptions.Clear();
        foreach (var p in items) Prescriptions.Add(p);
    }

    protected override void ClearData() => Prescriptions.Clear();
}
