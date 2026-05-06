// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain representing the system-wide donor organ index.
/// Grain key: "TX-DONOR-IDX"
/// </summary>
public interface ITransplantDonorIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all donors in the index, newest first by recovery date.</summary>
    Task<List<TransplantDonorSummaryEntry>> GetAllDonorsAsync();

    /// <summary>Returns donors filtered by organ type.</summary>
    Task<List<TransplantDonorSummaryEntry>> GetDonorsByOrganAsync(TransplantOrganType organType);

    /// <summary>Returns donors filtered by status.</summary>
    Task<List<TransplantDonorSummaryEntry>> GetDonorsByStatusAsync(DonorStatus status);

    /// <summary>Returns only available organs (Status = Available).</summary>
    Task<List<TransplantDonorSummaryEntry>> GetAvailableDonorsAsync();

    /// <summary>Adds or updates a donor summary entry.</summary>
    Task UpsertDonorAsync(TransplantDonorSummaryEntry entry);

    /// <summary>Removes a donor from the index.</summary>
    Task RemoveDonorAsync(string donorId);
}
