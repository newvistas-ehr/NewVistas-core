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

public partial class ImmunizationForecastViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<ForecastRecommendation> _recommendations = new();
    [ObservableProperty] private int _totalDue;
    [ObservableProperty] private int _totalOverdue;
    [ObservableProperty] private int _totalComplete;
    [ObservableProperty] private DateTime? _forecastDate;

    public ImmunizationForecastViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null;
        Recommendations.Clear();
        TotalDue = 0;
        TotalOverdue = 0;
        TotalComplete = 0;
        ForecastDate = null;

        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("IMMUNIZATION_FORECAST");
    }

    [RelayCommand]
    public async Task GenerateForecastAsync()
    {
        if (!HasPatient) return;
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var result = await workflow.GenerateImmunizationForecastAsync();

            if (!result.Success)
            {
                Error = result.ErrorMessage ?? "Forecast generation failed.";
                return;
            }

            Recommendations.Clear();
            foreach (var r in result.Recommendations)
                Recommendations.Add(r);

            TotalDue = result.TotalDue;
            TotalOverdue = result.TotalOverdue;
            TotalComplete = result.TotalComplete;
            ForecastDate = result.ForecastDate;
            SuccessMessage = "Forecast generated successfully.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
