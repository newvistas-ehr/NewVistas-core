// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.ViewModels;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class FhirGatewayViewModelTests : ViewModelTestBase
{
    private FhirGatewayViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new FhirGatewayViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public void ResourceTypes_HasExpectedValues()
    {
        Assert.That(_vm.ResourceTypes, Has.Length.GreaterThanOrEqualTo(8));
        Assert.That(_vm.ResourceTypes, Does.Contain("Patient"));
        Assert.That(_vm.ResourceTypes, Does.Contain("Condition"));
    }

    [Test]
    public void DefaultResourceType_IsPatient()
    {
        Assert.That(_vm.ResourceType, Is.EqualTo("Patient"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
