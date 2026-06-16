// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.BlazorWeb.Services;

/// <summary>
/// Scoped service that maintains the currently selected patient ID across pages
/// within a Blazor Server circuit. Analogous to the CPRS patient context bar that
/// persists the active patient until the user explicitly selects a different one.
///
/// VistA analogy: DFN (patient pointer) is set when the user selects a patient in
/// CPRS and remains active across all tabs (Cover Sheet, Orders, Notes, Vitals, etc.)
/// until a new patient is selected via Patient Lookup.
/// </summary>
public class PatientContextService
{
    /// <summary>Current patient ID (e.g. "9"), or null if no patient is selected.</summary>
    public string? PatientId { get; private set; }

    /// <summary>Patient display name, if known (set by whichever page first loads the patient).</summary>
    public string? PatientName { get; private set; }

    /// <summary>Fired when the selected patient changes so that layout/components can re-render.</summary>
    public event Action? OnChanged;

    /// <summary>Set the active patient for this circuit.</summary>
    public void SetPatient(string? patientId, string? patientName = null)
    {
        var trimmed = patientId?.Trim();
        if (PatientId == trimmed && (patientName == null || PatientName == patientName))
            return;

        PatientId = trimmed;
        if (patientName != null)
            PatientName = patientName;

        OnChanged?.Invoke();
    }

    /// <summary>Clear the patient context (e.g. on logout).</summary>
    public void Clear()
    {
        if (PatientId == null && PatientName == null) return;
        PatientId = null;
        PatientName = null;
        OnChanged?.Invoke();
    }
}
