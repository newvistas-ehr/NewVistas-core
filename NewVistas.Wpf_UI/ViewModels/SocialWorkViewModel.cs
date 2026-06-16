// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class SocialWorkViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<SocialWorkAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private ObservableCollection<SocialWorkReferralIndexEntry> _referrals = new();

    public SocialWorkViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var assessments = await workflow.GetSocialWorkAssessmentsAsync();
        Assessments.Clear();
        foreach (var a in assessments) Assessments.Add(a);

        var referrals = await workflow.GetSocialWorkReferralsAsync();
        Referrals.Clear();
        foreach (var r in referrals) Referrals.Add(r);
    }
}
