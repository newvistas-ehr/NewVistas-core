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
public class PatientPortalViewModelTests : ViewModelTestBase
{
    private PatientPortalViewModel _vm = null!;
    private IPatientSubmissionIndexGrain _mockSubIndex = null!;
    private ISecureMessageIndexGrain _mockMsgIndex = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockSubIndex = Substitute.For<IPatientSubmissionIndexGrain>();
        _mockMsgIndex = Substitute.For<ISecureMessageIndexGrain>();
        MockGrainFactory.GetGrain<IPatientSubmissionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockSubIndex);
        MockGrainFactory.GetGrain<ISecureMessageIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockMsgIndex);
        _vm = new PatientPortalViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsSubmissions()
    {
        SelectPatient("PAT-001");
        _mockSubIndex.GetAllSubmissionsAsync()
            .Returns(Task.FromResult(new List<PatientSubmissionSummary> { new() }));
        _mockMsgIndex.GetAllThreadsAsync()
            .Returns(Task.FromResult(new List<SecureMessageThreadSummary>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Submissions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_LoadsMessageThreads()
    {
        SelectPatient("PAT-001");
        _mockSubIndex.GetAllSubmissionsAsync()
            .Returns(Task.FromResult(new List<PatientSubmissionSummary>()));
        _mockMsgIndex.GetAllThreadsAsync()
            .Returns(Task.FromResult(new List<SecureMessageThreadSummary> { new(), new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Threads, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task LoadQueueAsync_LoadsPendingSubmissions()
    {
        SelectPatient("PAT-001");
        var mockQueue = Substitute.For<IPatientSubmissionQueueGrain>();
        MockGrainFactory.GetGrain<IPatientSubmissionQueueGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockQueue);
        mockQueue.GetPendingSubmissionsAsync()
            .Returns(Task.FromResult(new List<PatientSubmissionSummary> { new() }));

        await _vm.LoadQueueCommand.ExecuteAsync(null);

        Assert.That(_vm.QueueItems, Has.Count.EqualTo(1));
    }
}
