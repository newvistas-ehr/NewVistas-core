// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http;
using System.Windows.Threading;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Wpf_UI.Services;
using NSubstitute;
using Orleans;

namespace NewVistas.Wpf_UI_Tests;

/// <summary>
/// Base class for WPF ViewModel unit tests. Sets up mock OrleansGrainService,
/// ApiClient, PatientContext, and a mock IPatientWorkflowGrain.
/// </summary>
public abstract class ViewModelTestBase
{
    protected IGrainFactory MockGrainFactory { get; private set; } = null!;
    protected IPatientWorkflowGrain MockWorkflowGrain { get; private set; } = null!;
    protected OrleansGrainService GrainService { get; private set; } = null!;
    protected ApiClient ApiClient { get; private set; } = null!;
    protected PatientContext PatientContext { get; private set; } = null!;
    protected AuthService AuthService { get; private set; } = null!;

    [SetUp]
    public virtual void Setup()
    {
        MockGrainFactory = Substitute.For<IGrainFactory>();
        MockWorkflowGrain = Substitute.For<IPatientWorkflowGrain>();

        // Default: any patient ID returns the mock workflow grain
        MockGrainFactory
            .GetGrain<IPatientWorkflowGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(MockWorkflowGrain);

        // Default: IClinicIndexGrain returns empty list
        var mockClinicIndex = Substitute.For<IClinicIndexGrain>();
        mockClinicIndex.GetAllClinicsAsync().Returns(new List<NewVistas.Abstractions.GrainStates.ClinicEntry>());
        MockGrainFactory
            .GetGrain<IClinicIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockClinicIndex);

        // Create test HttpClient (used only by ApiClient for auth)
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:7127") };
        ApiClient = new ApiClient(httpClient);

        // Ensure a WPF Dispatcher exists on this thread so AuthService's
        // DispatcherTimer constructor succeeds. AuthService is sealed so
        // we cannot mock it — create a real instance instead.
        // Token is null by default so SetRequestContext() returns early.
        _ = Dispatcher.CurrentDispatcher;
        var authService = new AuthService(ApiClient);
        AuthService = authService;
        GrainService = new OrleansGrainService(MockGrainFactory, authService);

        PatientContext = new PatientContext();
    }

    /// <summary>
    /// Set the patient context to simulate a patient being selected.
    /// </summary>
    protected void SelectPatient(string patientId)
    {
        PatientContext.PatientId = patientId;
    }
}
