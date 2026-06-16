// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class PharmacyBenefitsViewModel : BasePatientViewModel
{
    [ObservableProperty] private PatientBenefitPlanState? _benefitPlan;
    [ObservableProperty] private ObservableCollection<PriorAuthIndexEntry> _priorAuths = new();

    // PA request form
    [ObservableProperty] private bool _showPaForm;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _indication = string.Empty;
    [ObservableProperty] private string _requestedByName = "Provider, Test";

    public PharmacyBenefitsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var plan = Grains.GetGrain<IPatientBenefitPlanGrain>($"PBM-PATIENT:{PatientId}");
        BenefitPlan = await plan.GetPlanAsync();

        var paIndex = Grains.GetGrain<IPriorAuthIndexGrain>($"PA-INDEX:{PatientId}");
        var paList = await paIndex.GetAllAsync();
        PriorAuths.Clear();
        foreach (var pa in paList) PriorAuths.Add(pa);
    }

    [RelayCommand]
    private void TogglePaForm() => ShowPaForm = !ShowPaForm;

    [RelayCommand]
    private async Task RequestPriorAuth()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(DrugName)) return;
        IsLoading = true; Error = null;
        try
        {
            string paId = $"PA:{Guid.NewGuid()}";
            var pa = Grains.GetGrain<IPriorAuthorizationGrain>(paId);
            await pa.SubmitRequestAsync(
                PatientId, null, DrugName, null, null, RequestedByName,
                new List<string>(),
                Indication.Length > 0 ? Indication : null);

            ShowPaForm = false;
            DrugName = string.Empty;
            Indication = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
