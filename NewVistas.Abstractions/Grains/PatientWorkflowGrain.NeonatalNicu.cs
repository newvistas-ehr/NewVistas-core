// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// NICU depth (Phase 2) for the neonatal module — respiratory support, phototherapy, the neonatal
/// problem list, parenteral/enteral nutrition, and bedside procedures. Same open access model as the
/// rest of neonatal/OB (NICU is clinical depth within NEONATAL_CARE, not a separate edition). Writes
/// that change acuity (respiratory support, problems) refresh the nursery census so the board flags
/// babies on support and counts active problems.
/// </summary>
public partial class PatientWorkflowGrain
{
    public async Task RecordNewbornRespiratorySupportAsync(
        string newbornId, RespiratorySupportType supportType, int? fiO2Percent,
        string settings, DateTime recordedAt, string notes)
    {
        await Newborn(newbornId).RecordRespiratorySupportAsync(new RespiratorySupportEntry
        {
            RecordedAt = recordedAt,
            SupportType = supportType,
            FiO2Percent = fiO2Percent,
            Settings = settings,
            Notes = notes
        });
        await RefreshNewbornNurseryAsync(newbornId);
    }

    public async Task StartNewbornPhototherapyAsync(
        string newbornId, PhototherapyIntensity intensity, string indication,
        decimal? bilirubinAtStartMgDl, DateTime startedAt, string notes)
    {
        await Newborn(newbornId).StartPhototherapyAsync(new PhototherapyEntry
        {
            StartedAt = startedAt,
            Intensity = intensity,
            Indication = indication,
            BilirubinAtStartMgDl = bilirubinAtStartMgDl,
            Notes = notes
        });
    }

    public async Task EndNewbornPhototherapyAsync(string newbornId, DateTime endedAt, string notes)
    {
        await Newborn(newbornId).EndPhototherapyAsync(endedAt, notes);
    }

    public async Task<string> AddNewbornProblemAsync(
        string newbornId, string problem, string icd10Code, DateTime? onsetDate, string notes)
    {
        string problemId = Guid.NewGuid().ToString();
        await Newborn(newbornId).AddProblemAsync(new NeonatalProblemEntry
        {
            ProblemId = problemId,
            Problem = problem,
            Icd10Code = icd10Code,
            OnsetDate = onsetDate,
            Status = NeonatalProblemStatus.Active,
            Notes = notes
        });
        await RefreshNewbornNurseryAsync(newbornId);
        return problemId;
    }

    public async Task ResolveNewbornProblemAsync(string newbornId, string problemId)
    {
        await Newborn(newbornId).ResolveProblemAsync(problemId);
        await RefreshNewbornNurseryAsync(newbornId);
    }

    public async Task RecordNewbornNutritionAsync(
        string newbornId, DateTime recordedAt, NeonatalNutritionRoute route,
        int? totalFluidMlPerKgPerDay, string detail, string notes)
    {
        await Newborn(newbornId).AddNutritionAsync(new NeonatalNutritionEntry
        {
            RecordedAt = recordedAt,
            Route = route,
            TotalFluidMlPerKgPerDay = totalFluidMlPerKgPerDay,
            Detail = detail,
            Notes = notes
        });
    }

    public async Task<string> RecordNewbornProcedureAsync(
        string newbornId, NeonatalProcedureType procedureType, DateTime performedAt,
        string performedBy, string notes)
    {
        string procedureId = Guid.NewGuid().ToString();
        await Newborn(newbornId).AddProcedureAsync(new NeonatalProcedureEntry
        {
            ProcedureId = procedureId,
            ProcedureType = procedureType,
            PerformedAt = performedAt,
            PerformedBy = performedBy,
            Notes = notes
        });
        return procedureId;
    }
}
