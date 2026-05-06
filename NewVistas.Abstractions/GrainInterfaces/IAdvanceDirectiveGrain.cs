// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient advance directive recording.
/// Grain key: "ADV-DIR:{patientId}"
/// </summary>
public interface IAdvanceDirectiveGrain : IGrainWithStringKey
{
    Task<AdvanceDirectiveState> GetAsync();
    Task UpdateCodeStatusAsync(CodeStatus codeStatus, string updatedByUserId);
    Task SetHealthcareProxyAsync(string proxyName, string? proxyPhone, string? proxyRelationship);
    Task AddDocumentAsync(AdvanceDirectiveType directiveType, DateTime documentDate,
        string? documentSource, DateTime? expirationDate, string? notes);
    Task RemoveDocumentAsync(string documentId);
}
