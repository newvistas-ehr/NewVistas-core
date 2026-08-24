// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class ExternalReferralViewModelTests : ViewModelTestBase
{
    private ExternalReferralViewModel _vm = null!;
    private ISiteParametersGrain _mockSiteParams = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockSiteParams = Substitute.For<ISiteParametersGrain>();
        MockGrainFactory.GetGrain<ISiteParametersGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockSiteParams);
        _vm = new ExternalReferralViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_ChecksFeatureFlag()
    {
        SelectPatient("PAT-001");
        _mockSiteParams.IsFeatureEnabledAsync("EXTERNAL_REFERRAL_TRACKING").Returns(Task.FromResult(false));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.IsFeatureEnabled, Is.False);
        Assert.That(_vm.Referrals, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task LoadDataAsync_LoadsReferrals_WhenEnabled()
    {
        SelectPatient("PAT-001");
        _mockSiteParams.IsFeatureEnabledAsync("EXTERNAL_REFERRAL_TRACKING").Returns(Task.FromResult(true));
        MockWorkflowGrain.GetExternalReferralsAsync()
            .Returns(Task.FromResult(new List<ExternalReferralIndexEntry> { new() { ReferralId = "R1" } }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.IsFeatureEnabled, Is.True);
        Assert.That(_vm.Referrals, Has.Count.EqualTo(1));
    }

    [Test]
    public void ToggleNewForm_TogglesShowNewForm()
    {
        Assert.That(_vm.ShowNewForm, Is.False);
        _vm.ToggleNewFormCommand.Execute(null);
        Assert.That(_vm.ShowNewForm, Is.True);
        _vm.ToggleNewFormCommand.Execute(null);
        Assert.That(_vm.ShowNewForm, Is.False);
    }
}
