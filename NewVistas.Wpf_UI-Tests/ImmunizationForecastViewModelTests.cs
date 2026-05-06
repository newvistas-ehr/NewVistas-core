// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class ImmunizationForecastViewModelTests : ViewModelTestBase
{
    private ImmunizationForecastViewModel _vm = null!;
    private ISiteParametersGrain _mockSiteParams = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockSiteParams = Substitute.For<ISiteParametersGrain>();
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockSiteParams);
        _vm = new ImmunizationForecastViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_ChecksFeatureFlag()
    {
        SelectPatient("PAT-001");
        _mockSiteParams.IsFeatureEnabledAsync("IMMUNIZATION_FORECAST").Returns(Task.FromResult(true));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.IsFeatureEnabled, Is.True);
    }

    [Test]
    public async Task GenerateForecastAsync_PopulatesResults()
    {
        SelectPatient("PAT-001");
        var result = new ImmunizationForecastResult
        {
            Success = true,
            TotalDue = 2,
            TotalOverdue = 1,
            TotalComplete = 5,
            ForecastDate = DateTime.UtcNow,
            Recommendations = new List<ForecastRecommendation>
            {
                new() { VaccineGroup = "Influenza", Status = "DUE" }
            }
        };
        MockWorkflowGrain.GenerateImmunizationForecastAsync().Returns(Task.FromResult(result));

        await _vm.GenerateForecastCommand.ExecuteAsync(null);

        Assert.That(_vm.Recommendations, Has.Count.EqualTo(1));
        Assert.That(_vm.TotalDue, Is.EqualTo(2));
        Assert.That(_vm.SuccessMessage, Is.EqualTo("Forecast generated successfully."));
    }

    [Test]
    public async Task GenerateForecastAsync_SetsError_WhenFails()
    {
        SelectPatient("PAT-001");
        var result = new ImmunizationForecastResult { Success = false, ErrorMessage = "No data" };
        MockWorkflowGrain.GenerateImmunizationForecastAsync().Returns(Task.FromResult(result));

        await _vm.GenerateForecastCommand.ExecuteAsync(null);

        Assert.That(_vm.Error, Is.EqualTo("No data"));
    }
}
