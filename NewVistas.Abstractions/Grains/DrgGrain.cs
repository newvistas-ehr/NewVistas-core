// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DRG Assignment Grain — simplified DRG grouper logic.
/// In production, this would call an external MS-DRG grouper engine.
/// This implementation uses a lookup table for common DRGs.
/// </summary>
public class DrgAssignmentGrain : Grain, IDrgAssignmentGrain
{
    private readonly IPersistentState<DrgAssignmentState> _state;

    public DrgAssignmentGrain(
        [PersistentState("drgAssignment", "drgStore")] IPersistentState<DrgAssignmentState> state)
    {
        _state = state;
    }

    public Task<DrgAssignmentState> GetAssignmentAsync() => Task.FromResult(_state.State);

    public async Task AssignDrgAsync(
        string patientId, string patientName,
        string principalDiagnosis, string principalDiagnosisDescription,
        List<string> secondaryDiagnoses, List<string> procedures,
        int patientAge, string patientSex,
        string dischargeStatus, int actualLOS)
    {
        _state.State.AdmissionId = this.GetPrimaryKeyString().Replace("DRG:", "");
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.PrincipalDiagnosis = principalDiagnosis;
        _state.State.PrincipalDiagnosisDescription = principalDiagnosisDescription;
        _state.State.SecondaryDiagnoses = secondaryDiagnoses;
        _state.State.Procedures = procedures;
        _state.State.PatientAge = patientAge;
        _state.State.PatientSex = patientSex;
        _state.State.DischargeStatus = dischargeStatus;
        _state.State.ActualLOS = actualLOS;

        // Determine CC/MCC level based on secondary diagnoses count (simplified)
        _state.State.CcMccLevel = secondaryDiagnoses.Count >= 3 ? "MCC" :
            secondaryDiagnoses.Count >= 1 ? "CC" : "NONE";

        // Simplified DRG assignment based on principal diagnosis prefix
        var (drg, desc, mdc, mdcDesc, type, weight, glos, alos) = GroupDrg(principalDiagnosis, procedures.Count > 0, _state.State.CcMccLevel);
        _state.State.DrgCode = drg;
        _state.State.DrgDescription = desc;
        _state.State.MdcCode = mdc;
        _state.State.MdcDescription = mdcDesc;
        _state.State.DrgType = type;
        _state.State.RelativeWeight = weight;
        _state.State.GeometricMeanLOS = glos;
        _state.State.ArithmeticMeanLOS = alos;

        // Outlier check: actual LOS > 2x geometric mean
        _state.State.IsOutlier = actualLOS > (double)glos * 2;

        _state.State.Status = "ASSIGNED";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReviewAsync(string reviewerId)
    {
        _state.State.Status = "REVIEWED";
        _state.State.ReviewedBy = reviewerId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task FinalizeAsync()
    {
        _state.State.Status = "FINAL";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReassignDrgAsync(string newDrgCode, string newDrgDescription, decimal newRelativeWeight, string reason)
    {
        _state.State.DrgCode = newDrgCode;
        _state.State.DrgDescription = newDrgDescription;
        _state.State.RelativeWeight = newRelativeWeight;
        _state.State.Status = "ASSIGNED";
        _state.State.ReviewedBy = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Simplified DRG grouper — maps ICD-10 diagnosis prefixes to common DRGs.
    /// A production system would use CMS MS-DRG grouper v41+.
    /// </summary>
    private static (string Drg, string Desc, string Mdc, string MdcDesc, string Type, decimal Weight, decimal Glos, decimal Alos)
        GroupDrg(string principalDx, bool hasProcedures, string ccMcc)
    {
        // Heart failure
        if (principalDx.StartsWith("I50"))
            return ccMcc == "MCC" ? ("291", "Heart Failure & Shock w MCC", "05", "Circulatory System", "MEDICAL", 1.4906m, 4.6m, 5.5m) :
                   ccMcc == "CC" ? ("292", "Heart Failure & Shock w CC", "05", "Circulatory System", "MEDICAL", 0.9901m, 3.5m, 4.2m) :
                   ("293", "Heart Failure & Shock w/o CC/MCC", "05", "Circulatory System", "MEDICAL", 0.6751m, 2.6m, 3.1m);

        // Pneumonia
        if (principalDx.StartsWith("J18") || principalDx.StartsWith("J15"))
            return ccMcc == "MCC" ? ("177", "Respiratory Infections w MCC", "04", "Respiratory System", "MEDICAL", 1.6570m, 5.5m, 6.8m) :
                   ("178", "Respiratory Infections w CC", "04", "Respiratory System", "MEDICAL", 1.0340m, 4.0m, 4.8m);

        // Hip/knee replacement
        if (principalDx.StartsWith("M16") || principalDx.StartsWith("M17"))
            return hasProcedures ? ("470", "Major Hip/Knee Joint Replacement", "08", "Musculoskeletal", "SURGICAL", 1.7404m, 1.9m, 2.4m) :
                   ("562", "Hip/Knee Replacement w/o MCC", "08", "Musculoskeletal", "MEDICAL", 0.8510m, 3.0m, 3.5m);

        // COPD
        if (principalDx.StartsWith("J44"))
            return ccMcc == "MCC" ? ("190", "COPD w MCC", "04", "Respiratory System", "MEDICAL", 1.1515m, 3.8m, 4.5m) :
                   ("191", "COPD w CC", "04", "Respiratory System", "MEDICAL", 0.8496m, 3.0m, 3.6m);

        // Sepsis
        if (principalDx.StartsWith("A41") || principalDx.StartsWith("R65"))
            return ("871", "Septicemia or Severe Sepsis w/o MV >96 hrs w MCC", "18", "Infectious & Parasitic", "MEDICAL", 1.8571m, 5.6m, 6.7m);

        // Default
        return hasProcedures
            ? ("999", "Ungroupable Surgical", "25", "Other", "SURGICAL", 1.0000m, 3.0m, 4.0m)
            : ("998", "Ungroupable Medical", "25", "Other", "MEDICAL", 0.7500m, 3.0m, 3.5m);
    }
}

/// <summary>
/// DRG Index Grain — facility-level coding worklist.
/// </summary>
public class DrgIndexGrain : Grain, IDrgIndexGrain
{
    private readonly IPersistentState<DrgIndexState> _state;

    public DrgIndexGrain(
        [PersistentState("drgIndex", "drgIndexStore")] IPersistentState<DrgIndexState> state)
    {
        _state = state;
    }

    public Task<List<DrgAssignmentEntry>> GetAllAssignmentsAsync()
        => Task.FromResult(_state.State.Assignments);

    public Task<List<DrgAssignmentEntry>> GetAssignmentsByStatusAsync(string status)
        => Task.FromResult(_state.State.Assignments.Where(a => a.Status == status).ToList());

    public async Task AddOrUpdateAsync(DrgAssignmentEntry entry)
    {
        int idx = _state.State.Assignments.FindIndex(a => a.AdmissionId == entry.AdmissionId);
        if (idx >= 0) _state.State.Assignments[idx] = entry;
        else _state.State.Assignments.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string admissionId)
    {
        _state.State.Assignments.RemoveAll(a => a.AdmissionId == admissionId);
        await _state.WriteStateAsync();
    }
}
