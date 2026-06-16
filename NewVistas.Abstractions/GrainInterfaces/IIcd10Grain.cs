// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// ICD-10-CM Diagnosis Code Grain — represents a single entry in VistA
/// ICD DIAGNOSIS file (#80) with coding system 10D.
///
/// Grain Key: The ICD-10-CM code (e.g., "A00.0", "M54.5").
///
/// This grain stores reference data. It is populated during ICD-10 code
/// loading (from the CMS order file) and read by clinical grains
/// (Problem List, Orders, Encounters) that need to validate or display
/// diagnosis information.
///
/// Related VistA APIs:
///   $$ICDDATA^ICDXCODE — primary data retrieval
///   $$STATCHK^ICDAPIU  — status check (active/inactive for a date)
///   $$ICDDX^ICDCODE    — diagnosis lookup by code
///   DGICD.m            — registration ICD search/select
/// </summary>
public interface IIcd10Grain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the full ICD-10 code state.
    /// </summary>
    Task<GrainStates.Icd10State> GetCodeAsync();

    /// <summary>
    /// Loads/updates the ICD-10 code data.
    /// Called during bulk import from the CMS order file.
    /// </summary>
    Task LoadCodeAsync(
        string code,
        string shortDescription,
        string longDescription,
        bool isBillable,
        int orderNumber,
        string? chapter);

    /// <summary>
    /// Checks if this code is currently active.
    /// Mirrors $$STATCHK^ICDAPIU.
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Activates this code. Mirrors status update in File #80 STATUS multiple.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates this code.
    /// </summary>
    Task DeactivateAsync();
}
