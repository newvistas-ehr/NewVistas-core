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

public partial class EncounterFormViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<EncounterFormInstanceIndexEntry> _instances = new();
    [ObservableProperty] private ObservableCollection<EncounterFormTemplateIndexEntry> _templates = new();

    public EncounterFormViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null;
        Instances.Clear();
        Templates.Clear();

        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("ENCOUNTER_FORM_TEMPLATES");
        if (!IsFeatureEnabled) return;

        var tplIndex = Grains.GetGrain<IEncounterFormTemplateIndexGrain>("EF-TPL-IDX");
        foreach (var t in await tplIndex.GetPublishedAsync(50)) Templates.Add(t);

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        foreach (var i in await workflow.GetEncounterFormInstancesAsync()) Instances.Add(i);
    }

    [RelayCommand]
    public async Task CreateInstanceAsync()
    {
        if (!HasPatient || Templates.Count == 0) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var tpl = Templates[0];
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateEncounterFormInstanceAsync(tpl.TemplateId, tpl.Name, null, "PROVIDER-CURRENT", "Provider, Test");
            SuccessMessage = "Form instance created.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
