// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class AdvanceDirectiveGrain : Grain, IAdvanceDirectiveGrain
{
    private readonly IPersistentState<AdvanceDirectiveState> _state;
    public AdvanceDirectiveGrain([PersistentState("advanceDirectiveState", "advanceDirectiveStore")] IPersistentState<AdvanceDirectiveState> state) { _state = state; }

    public Task<AdvanceDirectiveState> GetAsync() => Task.FromResult(_state.State);

    public async Task UpdateCodeStatusAsync(CodeStatus codeStatus, string updatedByUserId)
    {
        _state.State.PatientId = _state.State.PatientId == string.Empty ? this.GetPrimaryKeyString().Replace("ADV-DIR:", "") : _state.State.PatientId;
        _state.State.CodeStatus = codeStatus;
        _state.State.CodeStatusDate = DateTime.UtcNow;
        _state.State.CodeStatusUpdatedBy = updatedByUserId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetHealthcareProxyAsync(string proxyName, string? proxyPhone, string? proxyRelationship)
    {
        _state.State.HealthcareProxyName = proxyName;
        _state.State.HealthcareProxyPhone = proxyPhone;
        _state.State.HealthcareProxyRelationship = proxyRelationship;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddDocumentAsync(AdvanceDirectiveType directiveType, DateTime documentDate,
        string? documentSource, DateTime? expirationDate, string? notes)
    {
        _state.State.Documents.Add(new AdvanceDirectiveDocument
        {
            DocumentId = $"AD-{Guid.NewGuid():N}",
            DirectiveType = directiveType,
            DocumentDate = documentDate,
            DocumentSource = documentSource,
            IsOnFile = true,
            ExpirationDate = expirationDate,
            Notes = notes,
        });
        _state.State.HasAdvanceDirectives = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveDocumentAsync(string documentId)
    {
        _state.State.Documents.RemoveAll(d => d.DocumentId == documentId);
        _state.State.HasAdvanceDirectives = _state.State.Documents.Count > 0;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
