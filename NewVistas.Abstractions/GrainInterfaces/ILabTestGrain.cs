// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab Test Grain Interface based on VistA LAB DATA file (#63) and LABORATORY TEST file (#60)
/// Represents a single lab test for a patient
/// </summary>
public interface ILabTestGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete lab test state
    /// </summary>
    Task<GrainStates.LabTestState> GetLabTestAsync();

    /// <summary>
    /// Orders a new lab test
    /// </summary>
    Task OrderLabTestAsync(
        string patientId,
        string testId,
        string testName,
        string? testCode,
        string? orderId,
        string? orderingProviderId,
        string? orderingProviderName,
        string? specimenType,
        string? category);

    /// <summary>
    /// Records specimen collection
    /// </summary>
    Task CollectSpecimenAsync(
        DateTime collectionDateTime,
        string? collectionSample,
        string? performingLab);

    /// <summary>
    /// Records test result
    /// </summary>
    Task RecordResultAsync(
        DateTime resultDateTime,
        string resultValue,
        string? resultUnit,
        string? referenceLow,
        string? referenceHigh,
        string? abnormalFlag);

    /// <summary>
    /// Verifies the test result
    /// </summary>
    Task VerifyResultAsync(
        string verifyingProviderId,
        string verifyingProviderName,
        DateTime verifiedDateTime);

    /// <summary>
    /// Updates test status
    /// </summary>
    Task UpdateStatusAsync(string status);

    /// <summary>
    /// Adds comments to the test
    /// </summary>
    Task AddCommentsAsync(string comments);

    /// <summary>
    /// Marks test as critical
    /// </summary>
    Task MarkAsCriticalAsync();

    /// <summary>
    /// Cancels the lab test
    /// </summary>
    Task CancelLabTestAsync();

    /// <summary>
    /// Gets the patient ID for this lab test
    /// </summary>
    Task<string> GetPatientIdAsync();

    /// <summary>
    /// Gets the test result value
    /// </summary>
    Task<string?> GetResultValueAsync();

    /// <summary>
    /// Checks if result is abnormal
    /// </summary>
    Task<bool> IsAbnormalAsync();

    /// <summary>
    /// Checks if result is critical
    /// </summary>
    Task<bool> IsCriticalAsync();
}
