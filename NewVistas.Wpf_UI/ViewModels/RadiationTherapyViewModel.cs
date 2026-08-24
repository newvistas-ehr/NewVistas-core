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

public partial class RadiationTherapyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<RtCourseIndexEntry> _courses = new();
    [ObservableProperty] private RtCourseIndexEntry? _selectedCourse;
    [ObservableProperty] private ObservableCollection<RtTreatmentIndexEntry> _fractions = new();

    public RadiationTherapyViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    partial void OnSelectedCourseChanged(RtCourseIndexEntry? value)
    {
        if (value is not null) _ = SelectCourse(value);
    }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var courses = await workflow.GetRtCoursesAsync();
        Courses.Clear();
        foreach (var c in courses) Courses.Add(c);
        Fractions.Clear();
    }

    [RelayCommand]
    private async Task SelectCourse(RtCourseIndexEntry entry)
    {
        SelectedCourse = entry;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var fractions = await workflow.GetRtCourseTreatmentsAsync(entry.CourseId);
            Fractions.Clear();
            foreach (var f in fractions) Fractions.Add(f);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
