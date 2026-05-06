// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages patient relations and emergency contacts (VistA File #408.12 PATIENT RELATION).
/// Key: <c>"PATIENT-RELATION:{patientId}"</c>
/// </summary>
public interface IPatientRelationGrain : IGrainWithStringKey
{
    /// <summary>Returns all patient relations.</summary>
    Task<PatientRelationState> GetAsync();

    /// <summary>
    /// Adds a new relation or replaces the existing relation with the same RelationId.
    /// Returns the relation ID (generates a new Guid if RelationId is empty).
    /// </summary>
    Task<string> AddOrUpdateRelationAsync(PatientRelation relation);

    /// <summary>Removes a relation record by its ID.</summary>
    Task RemoveRelationAsync(string relationId);

    /// <summary>Returns all relations of the specified relationship type.</summary>
    Task<List<PatientRelation>> GetByTypeAsync(RelationshipType type);
}
