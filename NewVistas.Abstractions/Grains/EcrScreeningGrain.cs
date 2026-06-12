// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Concurrency;
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
///
/// [StatelessWorker]: pure compute — reads patient data and trigger
/// definitions, evaluates matches, holds nothing between calls.
/// </summary>
[StatelessWorker]
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

        // Patient reads and the trigger-index read are independent; the
        // workflow grain is [Reentrant], so issue them all together.
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        IEcrTriggerIndexGrain index = _grainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX");

        Task<List<ProblemSummary>> problemsTask = w.GetAllProblemsAsync();
        Task<List<LabTestSummaryEntry>> labsTask = w.GetLabSummaryAsync();
        Task<List<EcrTriggerSummary>> activeTriggersTask = index.GetActiveTriggersAsync();
        await Task.WhenAll(problemsTask, labsTask, activeTriggersTask);

        List<ProblemSummary> problems = problemsTask.Result;
        List<LabTestSummaryEntry> labs = labsTask.Result;
        List<EcrTriggerSummary> activeTriggers = activeTriggersTask.Result;

        // Fan out the per-trigger detail reads instead of awaiting one by one.
        EcrTriggerState[] triggers = await Task.WhenAll(activeTriggers.Select(t =>
            _grainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{t.TriggerId}").GetTriggerAsync()));

        var matches = new List<EcrScreeningMatch>();

        foreach (EcrTriggerState trigger in triggers)
        {
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
