// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// CPT Code Grain — represents a single entry in the AMA Current Procedural
/// Terminology coding system, used for procedure and service billing.
///
/// Grain Key: "CPT:{code}" (e.g., "CPT:99213", "CPT:36415").
///
/// CPT codes are the primary procedure coding system used in VA billing
/// and Patient Care Encounter (PCE) documentation. They map to VistA's
/// CPT file (#81) and are used in:
///   - PCE V CPT procedures (File #9000010.18)
///   - Integrated Billing claim lines
///   - Fee Basis authorizations
///   - Event Capture workload credit
///
/// Related VistA APIs:
///   $$CPT^ICPTCOD  — CPT code lookup
///   CPTSRCH^ICPT   — CPT search by text
/// </summary>
public interface ICptCodeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the full CPT code state.
    /// </summary>
    Task<GrainStates.CptCodeState> GetCodeAsync();

    /// <summary>
    /// Loads/updates the CPT code data.
    /// Called during bulk import from CPT reference files.
    /// </summary>
    Task LoadCodeAsync(
        string code,
        string shortName,
        string longDescription,
        string category,
        decimal workRvu,
        decimal totalRvu,
        string status,
        DateTime? effectiveDate);

    /// <summary>
    /// Checks if this code is currently active.
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Activates this code.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates this code.
    /// </summary>
    Task DeactivateAsync();
}
