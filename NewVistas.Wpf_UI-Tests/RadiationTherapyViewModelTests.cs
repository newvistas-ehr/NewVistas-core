// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class RadiationTherapyViewModelTests : ViewModelTestBase
{
    private RadiationTherapyViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new RadiationTherapyViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsCourses()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetRtCoursesAsync()
            .Returns(Task.FromResult(new List<RtCourseIndexEntry> { new() { CourseId = "C1" } }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Courses, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectCourse_LoadsFractions()
    {
        SelectPatient("PAT-001");
        var entry = new RtCourseIndexEntry { CourseId = "C1" };
        MockWorkflowGrain.GetRtCourseTreatmentsAsync("C1")
            .Returns(Task.FromResult(new List<RtTreatmentIndexEntry> { new(), new() }));

        await _vm.SelectCourseCommand.ExecuteAsync(entry);

        Assert.That(_vm.Fractions, Has.Count.EqualTo(2));
        Assert.That(_vm.SelectedCourse!.CourseId, Is.EqualTo("C1"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
