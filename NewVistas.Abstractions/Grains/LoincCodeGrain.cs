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
/// LOINC Code Grain — stores a single LOINC observation code.
///
/// Grain Key: "LOINC:{code}" (e.g., "LOINC:2345-7").
/// </summary>
public class LoincCodeGrain : Grain, ILoincCodeGrain
{
    private readonly IPersistentState<LoincCodeState> _state;

    public LoincCodeGrain(
        [PersistentState("loincCodeState", "loincCodeStore")]
        IPersistentState<LoincCodeState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Code))
        {
            // Extract code from grain key "LOINC:{code}"
            string key = this.GetPrimaryKeyString();
            string code = key.StartsWith("LOINC:") ? key[6..] : key;
            _state.State.Code = code;
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LoincCodeState> GetCodeAsync() =>
        Task.FromResult(_state.State);

    public async Task LoadCodeAsync(
        string code,
        string component,
        string property,
        string timeAspect,
        string system,
        string scale,
        string method,
        string shortName,
        string longCommonName,
        string status,
        string classType)
    {
        _state.State.Code = code;
        _state.State.Component = component;
        _state.State.Property = property;
        _state.State.TimeAspect = timeAspect;
        _state.State.System = system;
        _state.State.Scale = scale;
        _state.State.Method = method;
        _state.State.ShortName = shortName;
        _state.State.LongCommonName = longCommonName;
        _state.State.Status = status;
        _state.State.ClassType = classType;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsActiveAsync() =>
        Task.FromResult(_state.State.Status == "ACTIVE");

    public async Task ActivateAsync()
    {
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.Status = "DEPRECATED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
