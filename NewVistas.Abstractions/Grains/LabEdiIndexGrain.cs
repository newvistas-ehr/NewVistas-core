// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab EDI Index Grain — singleton index of all reference lab configurations
/// and EDI order tracking. Self-seeds with 3 demo reference labs.
///
/// Key: "LAB-EDI-INDEX"
/// Store: labEdiIndexStore
/// </summary>
public class LabEdiIndexGrain : Grain, ILabEdiIndexGrain
{
    private readonly IPersistentState<LabEdiIndexState> _state;
    private readonly IGrainFactory _grainFactory;

    public LabEdiIndexGrain(
        [PersistentState("labEdiIndex", "labEdiIndexStore")]
        IPersistentState<LabEdiIndexState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task<List<LabEdiLabSummary>> GetReferenceLabsAsync()
    {
        if (_state.State.ReferenceLabs.Count == 0)
            await SeedDemoDataAsync();

        return _state.State.ReferenceLabs;
    }

    public async Task AddOrUpdateLabAsync(LabEdiLabSummary summary)
    {
        int idx = _state.State.ReferenceLabs.FindIndex(l => l.ReferenceLabId == summary.ReferenceLabId);
        if (idx >= 0)
            _state.State.ReferenceLabs[idx] = summary;
        else
            _state.State.ReferenceLabs.Add(summary);

        await _state.WriteStateAsync();
    }

    public Task<List<LabEdiOrderSummary>> GetOrdersByPatientAsync(string patientId, int maxResults)
    {
        if (!_state.State.OrdersByPatient.TryGetValue(patientId, out List<LabEdiOrderSummary>? orders))
            return Task.FromResult(new List<LabEdiOrderSummary>());

        List<LabEdiOrderSummary> result = orders
            .OrderByDescending(o => o.CreatedDate)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(result);
    }

    public async Task AddOrUpdateOrderAsync(LabEdiOrderSummary summary)
    {
        if (!_state.State.OrdersByPatient.ContainsKey(summary.PatientId))
            _state.State.OrdersByPatient[summary.PatientId] = new List<LabEdiOrderSummary>();

        List<LabEdiOrderSummary> orders = _state.State.OrdersByPatient[summary.PatientId];
        int idx = orders.FindIndex(o => o.OrderId == summary.OrderId);
        if (idx >= 0)
            orders[idx] = summary;
        else
        {
            orders.Add(summary);
            _state.State.TotalOrders++;
        }

        await _state.WriteStateAsync();
    }

    public Task<LabEdiDashboard> GetDashboardAsync()
    {
        LabEdiDashboard dashboard = new()
        {
            ActiveReferenceLabs = _state.State.ReferenceLabs.Count(l => l.IsActive),
            TotalOrders = _state.State.TotalOrders,
            TotalResults = _state.State.TotalResults,
            PendingOrders = _state.State.OrdersByPatient.Values
                .SelectMany(o => o)
                .Count(o => o.Status == LabEdiOrderStatus.Draft),
            AwaitingResults = _state.State.OrdersByPatient.Values
                .SelectMany(o => o)
                .Count(o => o.Status == LabEdiOrderStatus.Transmitted
                         || o.Status == LabEdiOrderStatus.Acknowledged),
            LabSummaries = _state.State.ReferenceLabs.ToList()
        };

        return Task.FromResult(dashboard);
    }

    public async Task SeedDemoDataAsync()
    {
        if (_state.State.ReferenceLabs.Count > 0) return;

        // 1. Quest Diagnostics — TCP/MLLP, auto-acknowledge
        string questId = "QUEST-DX";
        ILabEdiConfigGrain questConfig = _grainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{questId}");
        await questConfig.ConfigureAsync(
            labName: "Quest Diagnostics",
            cliaNumber: "39D0617316",
            connectionType: "TCP/MLLP",
            host: "edi.questdiagnostics.com",
            port: 6661,
            ftpPath: null,
            hl7Version: "2.5.1",
            sendingFacilityId: "NEWVISTAS-VA",
            sendingFacilityName: "NewVistas VA Medical Center",
            autoAcknowledge: true,
            autoFileResults: false);
        await questConfig.ActivateAsync();

        _state.State.ReferenceLabs.Add(new LabEdiLabSummary
        {
            ReferenceLabId = questId,
            LabName = "Quest Diagnostics",
            CliaNumber = "39D0617316",
            ConnectionType = "TCP/MLLP",
            IsActive = true
        });

        // 2. LabCorp — TCP/MLLP, manual review
        string labcorpId = "LABCORP";
        ILabEdiConfigGrain labcorpConfig = _grainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{labcorpId}");
        await labcorpConfig.ConfigureAsync(
            labName: "LabCorp",
            cliaNumber: "34D0655560",
            connectionType: "TCP/MLLP",
            host: "edi.labcorp.com",
            port: 6662,
            ftpPath: null,
            hl7Version: "2.3.1",
            sendingFacilityId: "NEWVISTAS-VA",
            sendingFacilityName: "NewVistas VA Medical Center",
            autoAcknowledge: false,
            autoFileResults: false);
        await labcorpConfig.ActivateAsync();

        _state.State.ReferenceLabs.Add(new LabEdiLabSummary
        {
            ReferenceLabId = labcorpId,
            LabName = "LabCorp",
            CliaNumber = "34D0655560",
            ConnectionType = "TCP/MLLP",
            IsActive = true
        });

        // 3. ARUP Laboratories — sFTP, auto-file results
        string arupId = "ARUP-LABS";
        ILabEdiConfigGrain arupConfig = _grainFactory.GetGrain<ILabEdiConfigGrain>($"LAB-EDI-CFG:{arupId}");
        await arupConfig.ConfigureAsync(
            labName: "ARUP Laboratories",
            cliaNumber: "46D0523070",
            connectionType: "SFTP",
            host: "sftp.aruplab.com",
            port: 22,
            ftpPath: "/outbound/orders",
            hl7Version: "2.5.1",
            sendingFacilityId: "NEWVISTAS-VA",
            sendingFacilityName: "NewVistas VA Medical Center",
            autoAcknowledge: false,
            autoFileResults: true);
        await arupConfig.ActivateAsync();

        _state.State.ReferenceLabs.Add(new LabEdiLabSummary
        {
            ReferenceLabId = arupId,
            LabName = "ARUP Laboratories",
            CliaNumber = "46D0523070",
            ConnectionType = "SFTP",
            IsActive = true
        });

        await _state.WriteStateAsync();
    }
}
