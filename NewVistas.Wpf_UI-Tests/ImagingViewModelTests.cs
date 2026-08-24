// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class ImagingViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesImages()
    {
        var testData = new List<ImagingSummary>
        {
            new() { ImageId = "I1", ObjectType = "PHOTOGRAPH" },
            new() { ImageId = "I2", ObjectType = "RADIOLOGY" }
        };
        MockWorkflowGrain.GetImagesAsync(50).Returns(testData);
        SelectPatient("PATIENT-001");
        var vm = new ImagingViewModel(GrainService, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Images, Has.Count.EqualTo(2));
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetImagesAsync(50).Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new ImagingViewModel(GrainService, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new ImagingViewModel(GrainService, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
