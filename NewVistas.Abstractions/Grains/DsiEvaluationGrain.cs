// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// DSI Evaluation Grain — evaluates all active CDS interventions against a patient.
/// §170.315(b)(11) — returns alerts with source attribution and HTI-1 transparency.
///
/// Grain Key: "DSI-EVAL:{patientId}"
/// </summary>
public class DsiEvaluationGrain : Grain, IDsiEvaluationGrain
{
    private readonly IGrainFactory _grainFactory;

    public DsiEvaluationGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<List<DsiEvaluationResult>> EvaluatePatientAsync()
    {
        string key = this.GetPrimaryKeyString();
        int colonIdx = key.IndexOf(':');
        string patientId = colonIdx >= 0 ? key[(colonIdx + 1)..] : key;

        // Get patient's clinical data
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        PatientState patient = await w.GetPatientAsync();
        List<ProblemSummary> problems = await w.GetAllProblemsAsync();
        List<LabTestSummaryEntry> labs = await w.GetLabSummaryAsync();
        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        List<MedicationSummary> meds = await w.GetActiveMedicationsAsync();

        // Get all active interventions
        IDsiInterventionIndexGrain index = _grainFactory.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        List<DsiInterventionSummary> activeList = await index.GetActiveInterventionsAsync();

        var results = new List<DsiEvaluationResult>();

        foreach (DsiInterventionSummary summary in activeList)
        {
            IDsiInterventionGrain interventionGrain = _grainFactory.GetGrain<IDsiInterventionGrain>(
                $"DSI:{summary.InterventionId}");
            DsiInterventionState intervention = await interventionGrain.GetInterventionAsync();

            if (!intervention.IsActive) continue;

            var evidence = new List<string>();
            bool triggered = EvaluateTriggers(intervention.TriggerCriteria, patient, problems, labs, vitals, meds, evidence);

            if (triggered)
            {
                var result = new DsiEvaluationResult
                {
                    InterventionId = intervention.InterventionId,
                    Title = intervention.Title,
                    InterventionType = intervention.InterventionType,
                    Severity = intervention.Severity,
                    RecommendedAction = intervention.RecommendedAction,
                    SourceCitation = intervention.SourceCitation,
                    TriggerEvidence = evidence
                };

                // For predictive DSI, include HTI-1 transparency info
                if (intervention.InterventionType == "predictive")
                {
                    result.PredictiveTransparency = new DsiPredictiveTransparency
                    {
                        ModelPurpose = intervention.ModelPurpose ?? string.Empty,
                        Developer = intervention.Developer,
                        PerformanceMetrics = intervention.PerformanceMetrics,
                        KnownLimitations = intervention.KnownLimitations,
                        FairnessAssessment = intervention.FairnessAssessment,
                        InputDataRequirements = intervention.InputDataRequirements,
                        OutputDescription = intervention.OutputDescription
                    };
                }

                results.Add(result);
            }
        }

        return results;
    }

    private static bool EvaluateTriggers(
        List<DsiTriggerCriterion> criteria,
        PatientState patient,
        List<ProblemSummary> problems,
        List<LabTestSummaryEntry> labs,
        List<VitalSummary> vitals,
        List<MedicationSummary> meds,
        List<string> evidence)
    {
        // All criteria must be met (AND logic)
        foreach (DsiTriggerCriterion criterion in criteria)
        {
            if (!EvaluateSingleCriterion(criterion, patient, problems, labs, vitals, meds, evidence))
                return false;
        }
        return criteria.Count > 0;
    }

    private static bool EvaluateSingleCriterion(
        DsiTriggerCriterion criterion,
        PatientState patient,
        List<ProblemSummary> problems,
        List<LabTestSummaryEntry> labs,
        List<VitalSummary> vitals,
        List<MedicationSummary> meds,
        List<string> evidence)
    {
        switch (criterion.DataSource)
        {
            case "Problem":
                return EvaluateProblems(criterion, problems, evidence);
            case "Lab":
                return EvaluateLabs(criterion, labs, evidence);
            case "Vital":
                return EvaluateVitals(criterion, vitals, evidence);
            case "Medication":
                return EvaluateMedications(criterion, meds, evidence);
            case "Demographic":
                return EvaluateDemographic(criterion, patient, evidence);
            default:
                return false;
        }
    }

    private static bool EvaluateProblems(DsiTriggerCriterion criterion, List<ProblemSummary> problems, List<string> evidence)
    {
        string code = criterion.ValueSetOrCode;
        bool isNegation = criterion.Operator == "not-exists";

        bool found = problems.Any(p =>
        {
            if (string.IsNullOrEmpty(p.DiagnosisCode)) return false;
            if (code.EndsWith(".*"))
            {
                string prefix = code[..^2];
                return p.DiagnosisCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            return p.DiagnosisCode.Equals(code, StringComparison.OrdinalIgnoreCase);
        });

        if (found && !isNegation)
        {
            ProblemSummary match = problems.First(p =>
                !string.IsNullOrEmpty(p.DiagnosisCode) &&
                (code.EndsWith(".*") ? p.DiagnosisCode.StartsWith(code[..^2], StringComparison.OrdinalIgnoreCase)
                    : p.DiagnosisCode.Equals(code, StringComparison.OrdinalIgnoreCase)));
            evidence.Add($"Problem: {match.Diagnosis} ({match.DiagnosisCode})");
        }

        return isNegation ? !found : found;
    }

    private static bool EvaluateLabs(DsiTriggerCriterion criterion, List<LabTestSummaryEntry> labs, List<string> evidence)
    {
        List<LabTestSummaryEntry> matching = labs.Where(l =>
            !string.IsNullOrEmpty(l.LoincCode) &&
            l.LoincCode.Equals(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase)).ToList();

        if (criterion.Operator == "exists")
        {
            if (matching.Count > 0)
                evidence.Add($"Lab: {matching[0].TestName} = {matching[0].Value} {matching[0].Units}");
            return matching.Count > 0;
        }

        if (criterion.Operator == "not-exists")
            return matching.Count == 0;

        LabTestSummaryEntry? latest = matching.OrderByDescending(l => l.ResultDate).FirstOrDefault();
        if (latest == null || !double.TryParse(latest.Value, out double labValue))
            return false;

        bool result = criterion.Operator switch
        {
            "greater-than" => double.TryParse(criterion.ComparisonValue, out double gt) && labValue > gt,
            "less-than" => double.TryParse(criterion.ComparisonValue, out double lt) && labValue < lt,
            "between" => double.TryParse(criterion.ComparisonValue, out double min) &&
                         double.TryParse(criterion.ComparisonValue2, out double max) &&
                         labValue >= min && labValue <= max,
            _ => false
        };

        if (result)
            evidence.Add($"Lab: {latest.TestName} = {latest.Value} {latest.Units}");
        return result;
    }

    private static bool EvaluateVitals(DsiTriggerCriterion criterion, List<VitalSummary> vitals, List<string> evidence)
    {
        List<VitalSummary> matching = vitals.Where(v =>
            v.VitalType.Equals(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase)).ToList();

        if (criterion.Operator == "exists")
        {
            if (matching.Count > 0)
                evidence.Add($"Vital: {matching[0].VitalType} = {matching[0].Value}");
            return matching.Count > 0;
        }

        VitalSummary? latest = matching.OrderByDescending(v => v.DateTimeTaken).FirstOrDefault();
        if (latest == null || !double.TryParse(latest.Value, out double vitalValue))
            return false;

        bool result = criterion.Operator switch
        {
            "greater-than" => double.TryParse(criterion.ComparisonValue, out double gt) && vitalValue > gt,
            "less-than" => double.TryParse(criterion.ComparisonValue, out double lt) && vitalValue < lt,
            "between" => double.TryParse(criterion.ComparisonValue, out double min) &&
                         double.TryParse(criterion.ComparisonValue2, out double max) &&
                         vitalValue >= min && vitalValue <= max,
            _ => false
        };

        if (result)
            evidence.Add($"Vital: {latest.VitalType} = {latest.Value}");
        return result;
    }

    private static bool EvaluateMedications(DsiTriggerCriterion criterion, List<MedicationSummary> meds, List<string> evidence)
    {
        bool found = meds.Any(m =>
            !string.IsNullOrEmpty(m.DrugName) &&
            m.DrugName.Contains(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));

        if (found && criterion.Operator != "not-exists")
        {
            MedicationSummary match = meds.First(m =>
                m.DrugName.Contains(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));
            evidence.Add($"Medication: {match.DrugName}");
        }

        return criterion.Operator == "not-exists" ? !found : found;
    }

    private static bool EvaluateDemographic(DsiTriggerCriterion criterion, PatientState patient, List<string> evidence)
    {
        if (criterion.ValueSetOrCode == "Age" && patient.DateOfBirth.HasValue)
        {
            int age = DateTime.UtcNow.Year - patient.DateOfBirth.Value.Year;
            if (DateTime.UtcNow < patient.DateOfBirth.Value.AddYears(age)) age--;

            bool result = criterion.Operator switch
            {
                "greater-than" => int.TryParse(criterion.ComparisonValue, out int gt) && age > gt,
                "less-than" => int.TryParse(criterion.ComparisonValue, out int lt) && age < lt,
                "between" => int.TryParse(criterion.ComparisonValue, out int min) &&
                             int.TryParse(criterion.ComparisonValue2, out int max) &&
                             age >= min && age <= max,
                _ => false
            };

            if (result) evidence.Add($"Age: {age}");
            return result;
        }
        return false;
    }
}
