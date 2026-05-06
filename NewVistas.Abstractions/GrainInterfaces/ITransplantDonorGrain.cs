// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single donor organ record.
/// Grain key: "TX-DONOR:{guid}"
/// </summary>
public interface ITransplantDonorGrain : IGrainWithStringKey
{
    /// <summary>Returns the full donor state.</summary>
    Task<TransplantDonorState> GetDonorAsync();

    /// <summary>Creates a new donor organ record.</summary>
    Task CreateDonorAsync(
        DonorType donorType,
        TransplantOrganType organType,
        string donorName,
        DateTime? dateOfBirth,
        BloodType bloodType,
        decimal? weightKg,
        decimal? heightCm,
        string? causeOfDeath,
        DateTime? crossClampDateTime,
        DateTime recoveryDateTime,
        DateTime? expirationDateTime,
        string? hlaTyping,
        decimal? coldIschemiaTimeHours,
        string locationId,
        string locationName,
        string recoveredById,
        string recoveredByName,
        string? notes);

    /// <summary>Allocates this organ to a specific recipient patient.</summary>
    Task AllocateToPatientAsync(string patientId, string patientName, DateTime allocationDateTime);

    /// <summary>Records that the transplant has been completed.</summary>
    Task RecordTransplantAsync(DateTime transplantDateTime);

    /// <summary>Marks the organ as discarded with a reason.</summary>
    Task DiscardOrganAsync(string reason);
}
