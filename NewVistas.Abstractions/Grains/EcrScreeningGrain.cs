// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// eCR Screening Grain — evaluates a patient's clinical data against all active
/// reportable condition triggers to detect conditions requiring case reporting.
///
/// §170.315(f)(5) — Electronic Case Reporting trigger detection.
///
/// Grain Key: "ECR-SCREEN:{patientId}"
/// </summary>
public class EcrScreeningGrain : Grain, IEcrScreeningGrain
{
    private readonly IGrainFactory _grainFactory;

    public EcrScreeningGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<List<EcrScreeningMatch>> ScreenPatientAsync()
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string patientId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        // Get patient's clinical data
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        List<ProblemSummary> problems = await w.GetAllProblemsAsync();
        List<LabTestSummaryEntry> labs = await w.GetLabSummaryAsync();

        // Get all active triggers
        IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");
        List<EcrTriggerSummary> activeTriggers = await index.GetActiveTriggersAsync();

        var matches = new List<EcrScreeningMatch>();

        foreach (EcrTriggerSummary triggerSummary in activeTriggers)
        {
            IEcrTriggerGrain triggerGrain = _grainFactory.GetGrain<IEcrTriggerGrain>(
                $"ECR-TRIGGER:{triggerSummary.TriggerId}");
            EcrTriggerState trigger = await triggerGrain.GetTriggerAsync();

            if (!trigger.IsActive) continue;

            EcrScreeningMatch? match = EvaluateTrigger(trigger, problems, labs);
            if (match != null)
                matches.Add(match);
        }

        return matches;
    }

    private static EcrScreeningMatch? EvaluateTrigger(
        EcrTriggerState trigger,
        List<ProblemSummary> problems,
        List<LabTestSummaryEntry> labs)
    {
        foreach (EcrTriggerCode triggerCode in trigger.TriggerCodes)
        {
            switch (triggerCode.TriggerType)
            {
                case "diagnosis":
                    ProblemSummary? matchedProblem = MatchDiagnosis(triggerCode, problems);
                    if (matchedProblem != null)
                    {
                        return new EcrScreeningMatch
                        {
                            TriggerId = trigger.TriggerId,
                            ConditionName = trigger.ConditionName,
                            MatchedCode = matchedProblem.DiagnosisCode ?? triggerCode.Code,
                            MatchedCodeSystem = triggerCode.CodeSystem,
                            MatchedDescription = $"{matchedProblem.Diagnosis} ({matchedProblem.DiagnosisCode})",
                            Jurisdictions = trigger.Jurisdictions,
                            ReportingTimeframe = trigger.ReportingTimeframe,
                            ClinicalEvidence = new List<string>
                            {
                                $"Diagnosis: {matchedProblem.Diagnosis} ({matchedProblem.DiagnosisCode})",
                                $"Status: {matchedProblem.Status}"
                            }
                        };
                    }
                    break;

                case "lab-result":
                    LabTestSummaryEntry? matchedLab = MatchLabResult(triggerCode, labs);
                    if (matchedLab != null)
                    {
                        return new EcrScreeningMatch
                        {
                            TriggerId = trigger.TriggerId,
                            ConditionName = trigger.ConditionName,
                            MatchedCode = matchedLab.LoincCode,
                            MatchedCodeSystem = "LOINC",
                            MatchedDescription = $"{matchedLab.TestName}: {matchedLab.Value} {matchedLab.Units}",
                            Jurisdictions = trigger.Jurisdictions,
                            ReportingTimeframe = trigger.ReportingTimeframe,
                            ClinicalEvidence = new List<string>
                            {
                                $"Lab: {matchedLab.TestName} = {matchedLab.Value} {matchedLab.Units} on {matchedLab.ResultDate:yyyy-MM-dd}",
                                $"LOINC: {matchedLab.LoincCode}"
                            }
                        };
                    }
                    break;
            }
        }

        return null;
    }

    private static ProblemSummary? MatchDiagnosis(EcrTriggerCode triggerCode, List<ProblemSummary> problems)
    {
        string code = triggerCode.Code;

        return problems.FirstOrDefault(p =>
        {
            if (string.IsNullOrEmpty(p.DiagnosisCode)) return false;

            // Wildcard matching (e.g., "B05.*" matches "B05.0", "B05.9")
            if (code.EndsWith(".*"))
            {
                string prefix = code[..^2];
                return p.DiagnosisCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return p.DiagnosisCode.Equals(code, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static LabTestSummaryEntry? MatchLabResult(EcrTriggerCode triggerCode, List<LabTestSummaryEntry> labs)
    {
        return labs.FirstOrDefault(l =>
            !string.IsNullOrEmpty(l.LoincCode) &&
            l.LoincCode.Equals(triggerCode.Code, StringComparison.OrdinalIgnoreCase));
    }
}
