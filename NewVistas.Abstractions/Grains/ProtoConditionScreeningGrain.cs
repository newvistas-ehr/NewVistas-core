// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient proto-condition screening worker (grain key <c>PROTO-SCREEN:{patientId}</c>).
///
/// [StatelessWorker]: pure compute — reads the patient's read models and a proto definition,
/// runs the deterministic <see cref="ProtoConditionMatcher"/>, holds nothing between calls.
/// Snapshot assembly is defensive: a failure reading any one dimension degrades that dimension to
/// empty rather than failing the whole evaluation (the matcher reports it as "not assessed").
/// </summary>
[StatelessWorker]
public class ProtoConditionScreeningGrain : Grain, IProtoConditionScreeningGrain
{
    private readonly IGrainFactory _grainFactory;

    public ProtoConditionScreeningGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    private string PatientId()
    {
        string key = this.GetPrimaryKeyString();
        int colon = key.IndexOf(':');
        return colon >= 0 ? key[(colon + 1)..] : key;
    }

    public async Task<PatientFeatureSnapshot> AssembleSnapshotAsync()
    {
        string patientId = PatientId();
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        IPatientSymptomGrain sym = _grainFactory.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{patientId}");

        PatientState patient = await Safe(() => w.GetPatientAsync(), new PatientState());
        List<ProblemSummary> problems = await Safe(() => w.GetActiveProblemsAsync(), new());
        List<LabTestSummaryEntry> labs = await Safe(() => w.GetLabSummaryAsync(), new());
        List<VitalSummary> vitals = await Safe(() => w.GetLatestVitalsAsync(), new());
        TreatingFacilityListState facilities = await Safe(() => w.GetTreatingFacilitiesAsync(), new());
        List<SymptomObservation> symptoms = await Safe(() => sym.GetLatestAsync(), new());

        var snapshot = new PatientFeatureSnapshot
        {
            PatientId = patientId,
            AssembledAt = DateTime.UtcNow,
            Problems = problems
                .Where(p => !string.IsNullOrWhiteSpace(p.DiagnosisCode))
                .Select(p => p.DiagnosisCode!.Trim().ToUpperInvariant())
                .Distinct()
                .ToList(),
            Labs = labs.Select(l => new SnapshotLab
            {
                Loinc = l.LoincCode,
                Value = l.Value,
                ResultedDate = l.ResultDate.UtcDateTime,
                AbnormalFlag = l.AbnormalFlag
            }).ToList(),
            Symptoms = symptoms
                .GroupBy(o => o.Code)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.RecordedDate).First().Presence),
            Vitals = BuildVitals(vitals),
            Age = ComputeAge(patient.DateOfBirth),
            Sex = string.IsNullOrWhiteSpace(patient.Sex) ? null : patient.Sex,
            City = patient.City,
            Race = patient.Race.FirstOrDefault(),
            Facilities = facilities.Facilities
                .Where(f => !string.IsNullOrWhiteSpace(f.FacilityId))
                .Select(f => f.FacilityId)
                .Distinct()
                .ToList()
        };
        return snapshot;
    }

    public async Task<ProtoMatchResult> EvaluateAsync(string protoConditionId)
    {
        PatientFeatureSnapshot snapshot = await AssembleSnapshotAsync();
        ProtoConditionState proto = await _grainFactory
            .GetGrain<IProtoConditionGrain>($"PROTO:{protoConditionId}").GetAsync();
        return ProtoConditionMatcher.Evaluate(proto, snapshot);
    }

    public async Task<ProtoMatchResult> EvaluateAndRecordAsync(string protoConditionId)
    {
        ProtoMatchResult result = await EvaluateAsync(protoConditionId);
        await _grainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{protoConditionId}")
            .UpsertEvaluationAsync(result);
        return result;
    }

    // ── Snapshot helpers ─────────────────────────────────────────────────

    private static async Task<T> Safe<T>(Func<Task<T>> read, T fallback)
    {
        try { return await read(); }
        catch { return fallback; }
    }

    private static int? ComputeAge(DateTime? dob)
    {
        if (dob is null) return null;
        DateTime now = DateTime.UtcNow;
        int age = now.Year - dob.Value.Year;
        if (dob.Value.Date > now.AddYears(-age)) age--;
        return age < 0 ? null : age;
    }

    private static List<SnapshotVital> BuildVitals(List<VitalSummary> vitals)
    {
        var result = new List<SnapshotVital>();
        foreach (VitalSummary v in vitals)
        {
            string key = CanonicalVitalKey(v.VitalType);
            if (key == "BP" && v.Value.Contains('/'))
            {
                string[] parts = v.Value.Split('/', 2);
                result.Add(NumericVital("BP_SYS", parts[0], v.Value, v.DateTimeTaken));
                result.Add(NumericVital("BP_DIA", parts.Length > 1 ? parts[1] : "", v.Value, v.DateTimeTaken));
            }
            else
            {
                result.Add(NumericVital(key, v.Value, v.Value, v.DateTimeTaken));
            }
        }
        return result;
    }

    private static SnapshotVital NumericVital(string type, string valuePart, string raw, DateTime measured) => new()
    {
        Type = type,
        Numeric = TryLeadingNumber(valuePart),
        Raw = raw,
        Measured = measured
    };

    private static double? TryLeadingNumber(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var chars = new List<char>();
        bool started = false;
        foreach (char c in raw.Trim())
        {
            if (char.IsDigit(c) || c == '.' || ((c == '-' || c == '+') && !started)) { chars.Add(c); started = true; }
            else if (started) break;
        }
        return double.TryParse(new string(chars.ToArray()), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : null;
    }

    private static string CanonicalVitalKey(string vitalType)
    {
        string t = (vitalType ?? string.Empty).Trim().ToUpperInvariant();
        return t switch
        {
            "BP" or "B/P" or "BLOOD PRESSURE" => "BP",
            "SPO2" or "PO2" or "POX" or "PULSE OX" or "PULSE OXIMETRY" or "O2 SAT" or "OXYGEN SATURATION" => "SPO2",
            "TEMP" or "T" or "TEMPERATURE" => "TEMP",
            "P" or "HR" or "PULSE" or "HEART RATE" => "HR",
            "R" or "RR" or "RESP" or "RESPIRATION" or "RESPIRATORY RATE" => "RR",
            "WT" or "WEIGHT" => "WT",
            "HT" or "HEIGHT" => "HT",
            _ => t.Replace(' ', '_')
        };
    }
}
