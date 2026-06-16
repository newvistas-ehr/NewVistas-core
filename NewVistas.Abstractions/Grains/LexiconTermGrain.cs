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
/// Lexicon Term Grain — stores a single clinical terminology entry.
///
/// Grain Key: "LEX:{CodingSystem}:{Code}"
///   e.g., "LEX:SNOMED:73211009", "LEX:ICD10:E11.9"
///
/// Simple load/get pattern mirroring ICD-10 individual code grains.
/// </summary>
public class LexiconTermGrain : Grain, ILexiconTermGrain
{
    private readonly IPersistentState<LexiconTermState> _state;

    public LexiconTermGrain(
        [PersistentState("lexiconTermState", "lexiconTermStore")]
        IPersistentState<LexiconTermState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Code))
        {
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<LexiconTermState> GetTermAsync() =>
        Task.FromResult(_state.State);

    public async Task LoadTermAsync(
        string code,
        string codingSystem,
        string designation,
        string? fullySpecifiedName,
        bool isActive,
        string? semanticType,
        string? conceptId,
        List<string>? synonyms)
    {
        _state.State.Code = code;
        _state.State.CodingSystem = codingSystem;
        _state.State.Designation = designation;
        _state.State.FullySpecifiedName = fullySpecifiedName;
        _state.State.IsActive = isActive;
        _state.State.SemanticType = semanticType;
        _state.State.ConceptId = conceptId;
        _state.State.Synonyms = synonyms ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }
}
