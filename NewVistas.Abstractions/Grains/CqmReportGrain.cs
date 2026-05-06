// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text;
using System.Xml;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// CQM Evaluation Report Grain — evaluates quality measures against patient data
/// and generates QRDA Category I/III XML exports.
///
/// §170.315(c)(1) — Record and export (QRDA I per patient)
/// §170.315(c)(2) — Import and calculate (evaluate measure criteria)
/// §170.315(c)(3) — Report (QRDA III aggregate)
/// §170.315(c)(4) — Filter (by age, sex, race, ethnicity, payer)
///
/// Grain Key: "CQM-REPORT:{reportId}"
/// </summary>
public class CqmReportGrain : Grain, ICqmReportGrain
{
    private readonly IPersistentState<CqmReportState> _state;
    private readonly IGrainFactory _grainFactory;

    public CqmReportGrain(
        [PersistentState("cqmReportState", "cqmReportStore")] IPersistentState<CqmReportState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReportId))
            _state.State.ReportId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task EvaluateAsync(
        string measureId,
        List<string> patientIds,
        DateTime periodStart,
        DateTime periodEnd,
        string? evaluatedBy)
    {
        _state.State.MeasureId = measureId;
        _state.State.PeriodStart = periodStart;
        _state.State.PeriodEnd = periodEnd;
        _state.State.EvaluatedBy = evaluatedBy;
        _state.State.EvaluatedPatientIds = patientIds;
        _state.State.Status = "evaluating";
        _state.State.PatientResults = new();

        await _state.WriteStateAsync();

        try
        {
            // Load the measure definition
            ICqmMeasureGrain measureGrain = _grainFactory.GetGrain<ICqmMeasureGrain>($"CQM:{measureId}");
            CqmMeasureState measure = await measureGrain.GetMeasureAsync();

            if (string.IsNullOrEmpty(measure.Title))
                throw new InvalidOperationException($"Measure {measureId} not found.");

            // Evaluate each patient
            foreach (string patientId in patientIds)
            {
                CqmPatientResult result = await EvaluatePatientAsync(patientId, measure, periodStart, periodEnd);
                _state.State.PatientResults.Add(result);
            }

            // Compute aggregates
            ComputeAggregates(_state.State);

            _state.State.Status = "completed";
            _state.State.EvaluatedDate = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _state.State.Status = "error";
            _state.State.ErrorMessage = ex.Message;
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<CqmReportState> GetReportAsync() => Task.FromResult(_state.State);

    public Task<CqmReportState> GetFilteredReportAsync(CqmFilterCriteria filter)
    {
        List<CqmPatientResult> filtered = ApplyFilter(_state.State.PatientResults, filter);

        var report = new CqmReportState
        {
            ReportId = _state.State.ReportId,
            MeasureId = _state.State.MeasureId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            Status = _state.State.Status,
            PatientResults = filtered,
            EvaluatedDate = _state.State.EvaluatedDate,
            EvaluatedBy = _state.State.EvaluatedBy
        };

        ComputeAggregates(report);
        return Task.FromResult(report);
    }

    public Task<string> ExportQrdaCategoryIAsync(string patientId)
    {
        CqmPatientResult? result = _state.State.PatientResults
            .FirstOrDefault(r => r.PatientId == patientId);

        if (result == null)
            throw new InvalidOperationException($"No results for patient {patientId}.");

        string xml = GenerateQrdaCategoryI(result, _state.State);
        return Task.FromResult(xml);
    }

    public Task<string> ExportQrdaCategoryIIIAsync()
    {
        string xml = GenerateQrdaCategoryIII(_state.State, _state.State.PatientResults);
        return Task.FromResult(xml);
    }

    public Task<string> ExportFilteredQrdaCategoryIIIAsync(CqmFilterCriteria filter)
    {
        List<CqmPatientResult> filtered = ApplyFilter(_state.State.PatientResults, filter);
        var filteredReport = new CqmReportState
        {
            ReportId = _state.State.ReportId,
            MeasureId = _state.State.MeasureId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PatientResults = filtered
        };
        ComputeAggregates(filteredReport);

        string xml = GenerateQrdaCategoryIII(filteredReport, filtered);
        return Task.FromResult(xml);
    }

    // ─── Evaluation Engine ────────────────────────────────────────────────────

    private async Task<CqmPatientResult> EvaluatePatientAsync(
        string patientId, CqmMeasureState measure, DateTime periodStart, DateTime periodEnd)
    {
        IPatientWorkflowGrain w = _grainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        PatientState patient = await w.GetPatientAsync();

        var result = new CqmPatientResult
        {
            PatientId = patientId,
            PatientName = patient.Name,
            Sex = patient.Sex,
            Race = patient.Race.Count > 0 ? string.Join(", ", patient.Race) : null,
            Ethnicity = patient.Ethnicity.Count > 0 ? string.Join(", ", patient.Ethnicity) : null
        };

        // Calculate age at period end
        if (patient.DateOfBirth.HasValue)
        {
            int age = periodEnd.Year - patient.DateOfBirth.Value.Year;
            if (periodEnd < patient.DateOfBirth.Value.AddYears(age)) age--;
            result.Age = age;
        }

        // Get clinical data for evaluation
        List<ProblemSummary> problems = await w.GetAllProblemsAsync();
        List<LabTestSummaryEntry> labs = await w.GetLabSummaryAsync();
        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        List<MedicationSummary> meds = await w.GetActiveMedicationsAsync();

        // Evaluate Initial Population
        result.InInitialPopulation = EvaluateCriteria(
            measure.InitialPopulation, patient, problems, labs, vitals, meds,
            periodStart, periodEnd, result.Evidence);

        if (!result.InInitialPopulation)
            return result;

        // Evaluate Denominator
        result.InDenominator = measure.Denominator.Count == 0 || EvaluateCriteria(
            measure.Denominator, patient, problems, labs, vitals, meds,
            periodStart, periodEnd, result.Evidence);

        if (!result.InDenominator)
            return result;

        // Evaluate Denominator Exclusions
        if (measure.DenominatorExclusions.Count > 0)
        {
            var exclusionEvidence = new List<string>();
            result.IsDenominatorExclusion = EvaluateCriteria(
                measure.DenominatorExclusions, patient, problems, labs, vitals, meds,
                periodStart, periodEnd, exclusionEvidence);

            if (result.IsDenominatorExclusion)
            {
                result.ExclusionReason = string.Join("; ", exclusionEvidence);
                return result;
            }
        }

        // Evaluate Numerator
        result.InNumerator = EvaluateCriteria(
            measure.Numerator, patient, problems, labs, vitals, meds,
            periodStart, periodEnd, result.Evidence);

        return result;
    }

    private static bool EvaluateCriteria(
        List<CqmCriterion> criteria,
        PatientState patient,
        List<ProblemSummary> problems,
        List<LabTestSummaryEntry> labs,
        List<VitalSummary> vitals,
        List<MedicationSummary> meds,
        DateTime periodStart,
        DateTime periodEnd,
        List<string> evidence)
    {
        // All criteria must be met (AND logic)
        foreach (CqmCriterion criterion in criteria)
        {
            if (!EvaluateSingleCriterion(criterion, patient, problems, labs, vitals, meds,
                periodStart, periodEnd, evidence))
                return false;
        }
        return criteria.Count > 0;
    }

    private static bool EvaluateSingleCriterion(
        CqmCriterion criterion,
        PatientState patient,
        List<ProblemSummary> problems,
        List<LabTestSummaryEntry> labs,
        List<VitalSummary> vitals,
        List<MedicationSummary> meds,
        DateTime periodStart,
        DateTime periodEnd,
        List<string> evidence)
    {
        switch (criterion.DataSource)
        {
            case "Demographic":
                return EvaluateDemographic(criterion, patient, periodEnd, evidence);

            case "Problem":
                return EvaluateProblems(criterion, problems, evidence);

            case "Lab":
                return EvaluateLabs(criterion, labs, periodStart, periodEnd, evidence);

            case "Vital":
                return EvaluateVitals(criterion, vitals, periodStart, periodEnd, evidence);

            case "Medication":
                return EvaluateMedications(criterion, meds, evidence);

            default:
                return false;
        }
    }

    private static bool EvaluateDemographic(
        CqmCriterion criterion, PatientState patient, DateTime periodEnd, List<string> evidence)
    {
        if (criterion.ValueSetOrCode == "Age" && patient.DateOfBirth.HasValue)
        {
            int age = periodEnd.Year - patient.DateOfBirth.Value.Year;
            if (periodEnd < patient.DateOfBirth.Value.AddYears(age)) age--;

            bool result = criterion.Operator switch
            {
                "between" => int.TryParse(criterion.ComparisonValue, out int min) &&
                             int.TryParse(criterion.ComparisonValue2, out int max) &&
                             age >= min && age <= max,
                "greater-than" => int.TryParse(criterion.ComparisonValue, out int gt) && age > gt,
                "less-than" => int.TryParse(criterion.ComparisonValue, out int lt) && age < lt,
                _ => false
            };

            if (result) evidence.Add($"Age {age} meets criterion: {criterion.Description}");
            return result;
        }

        return false;
    }

    private static bool EvaluateProblems(
        CqmCriterion criterion, List<ProblemSummary> problems, List<string> evidence)
    {
        bool isNegation = criterion.Operator == "not-exists";
        string code = criterion.ValueSetOrCode;

        bool found = problems.Any(p =>
        {
            if (string.IsNullOrEmpty(p.DiagnosisCode)) return false;
            // Support wildcard matching (e.g., "E11.*" matches "E11.65", "E11.9")
            if (code.EndsWith(".*"))
            {
                string prefix = code[..^2];
                return p.DiagnosisCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            return p.DiagnosisCode.Equals(code, StringComparison.OrdinalIgnoreCase);
        });

        if (found && !isNegation)
        {
            ProblemSummary? match = problems.First(p =>
                !string.IsNullOrEmpty(p.DiagnosisCode) &&
                (code.EndsWith(".*") ? p.DiagnosisCode.StartsWith(code[..^2], StringComparison.OrdinalIgnoreCase)
                    : p.DiagnosisCode.Equals(code, StringComparison.OrdinalIgnoreCase)));
            evidence.Add($"Problem: {match.Diagnosis} ({match.DiagnosisCode})");
        }

        return isNegation ? !found : found;
    }

    private static bool EvaluateLabs(
        CqmCriterion criterion, List<LabTestSummaryEntry> labs,
        DateTime periodStart, DateTime periodEnd, List<string> evidence)
    {
        IEnumerable<LabTestSummaryEntry> matching = labs.Where(l =>
            !string.IsNullOrEmpty(l.LoincCode) &&
            l.LoincCode.Equals(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));

        if (criterion.RequireInMeasurementPeriod)
            matching = matching.Where(l => l.ResultDate >= periodStart && l.ResultDate <= periodEnd);

        List<LabTestSummaryEntry> matchList = matching.ToList();

        if (criterion.Operator == "exists")
        {
            if (matchList.Count > 0)
                evidence.Add($"Lab: {matchList[0].TestName} = {matchList[0].Value} {matchList[0].Units} on {matchList[0].ResultDate:yyyy-MM-dd}");
            return matchList.Count > 0;
        }

        if (criterion.Operator == "not-exists")
            return matchList.Count == 0;

        // Numeric comparison on lab value
        LabTestSummaryEntry? latest = matchList.OrderByDescending(l => l.ResultDate).FirstOrDefault();
        if (latest == null || !double.TryParse(latest.Value, out double labValue))
            return false;

        bool result = criterion.Operator switch
        {
            "less-than" => double.TryParse(criterion.ComparisonValue, out double lt) && labValue < lt,
            "greater-than" => double.TryParse(criterion.ComparisonValue, out double gt) && labValue > gt,
            "less-than-or-equal" => double.TryParse(criterion.ComparisonValue, out double lte) && labValue <= lte,
            "greater-than-or-equal" => double.TryParse(criterion.ComparisonValue, out double gte) && labValue >= gte,
            "between" => double.TryParse(criterion.ComparisonValue, out double min) &&
                         double.TryParse(criterion.ComparisonValue2, out double max) &&
                         labValue >= min && labValue <= max,
            _ => false
        };

        if (result)
            evidence.Add($"Lab: {latest.TestName} = {latest.Value} {latest.Units} on {latest.ResultDate:yyyy-MM-dd}");

        return result;
    }

    private static bool EvaluateVitals(
        CqmCriterion criterion, List<VitalSummary> vitals,
        DateTime periodStart, DateTime periodEnd, List<string> evidence)
    {
        IEnumerable<VitalSummary> matching = vitals.Where(v =>
            v.VitalType.Equals(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));

        if (criterion.RequireInMeasurementPeriod)
            matching = matching.Where(v => v.DateTimeTaken >= periodStart && v.DateTimeTaken <= periodEnd);

        List<VitalSummary> matchList = matching.ToList();

        if (criterion.Operator == "exists")
        {
            if (matchList.Count > 0)
                evidence.Add($"Vital: {matchList[0].VitalType} = {matchList[0].Value} on {matchList[0].DateTimeTaken:yyyy-MM-dd}");
            return matchList.Count > 0;
        }

        // Numeric comparison
        VitalSummary? latest = matchList.OrderByDescending(v => v.DateTimeTaken).FirstOrDefault();
        if (latest == null || !double.TryParse(latest.Value, out double vitalValue))
            return false;

        bool result = criterion.Operator switch
        {
            "less-than" => double.TryParse(criterion.ComparisonValue, out double lt) && vitalValue < lt,
            "greater-than" => double.TryParse(criterion.ComparisonValue, out double gt) && vitalValue > gt,
            "less-than-or-equal" => double.TryParse(criterion.ComparisonValue, out double lte) && vitalValue <= lte,
            "between" => double.TryParse(criterion.ComparisonValue, out double min) &&
                         double.TryParse(criterion.ComparisonValue2, out double max) &&
                         vitalValue >= min && vitalValue <= max,
            _ => false
        };

        if (result)
            evidence.Add($"Vital: {latest.VitalType} = {latest.Value} on {latest.DateTimeTaken:yyyy-MM-dd}");

        return result;
    }

    private static bool EvaluateMedications(
        CqmCriterion criterion, List<MedicationSummary> meds, List<string> evidence)
    {
        bool found = meds.Any(m =>
            !string.IsNullOrEmpty(m.DrugName) &&
            m.DrugName.Contains(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));

        if (found && criterion.Operator != "not-exists")
        {
            MedicationSummary match = meds.First(m =>
                m.DrugName.Contains(criterion.ValueSetOrCode, StringComparison.OrdinalIgnoreCase));
            evidence.Add($"Medication: {match.DrugName} ({match.Status})");
        }

        return criterion.Operator == "not-exists" ? !found : found;
    }

    // ─── Aggregation ──────────────────────────────────────────────────────────

    private static void ComputeAggregates(CqmReportState report)
    {
        report.InitialPopulationCount = report.PatientResults.Count(r => r.InInitialPopulation);
        report.DenominatorCount = report.PatientResults.Count(r => r.InDenominator && !r.IsDenominatorExclusion);
        report.DenominatorExclusionCount = report.PatientResults.Count(r => r.IsDenominatorExclusion);
        report.NumeratorCount = report.PatientResults.Count(r => r.InNumerator);

        int eligibleDenominator = report.DenominatorCount;
        report.PerformanceRate = eligibleDenominator > 0
            ? Math.Round((double)report.NumeratorCount / eligibleDenominator * 100, 1)
            : 0;
    }

    // ─── Filtering (§170.315(c)(4)) ───────────────────────────────────────────

    private static List<CqmPatientResult> ApplyFilter(List<CqmPatientResult> results, CqmFilterCriteria filter)
    {
        IEnumerable<CqmPatientResult> filtered = results;

        if (filter.MinAge.HasValue)
            filtered = filtered.Where(r => r.Age.HasValue && r.Age.Value >= filter.MinAge.Value);
        if (filter.MaxAge.HasValue)
            filtered = filtered.Where(r => r.Age.HasValue && r.Age.Value <= filter.MaxAge.Value);
        if (!string.IsNullOrEmpty(filter.Sex))
            filtered = filtered.Where(r => string.Equals(r.Sex, filter.Sex, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filter.Race))
            filtered = filtered.Where(r => string.Equals(r.Race, filter.Race, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filter.Ethnicity))
            filtered = filtered.Where(r => string.Equals(r.Ethnicity, filter.Ethnicity, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(filter.Payer))
            filtered = filtered.Where(r => string.Equals(r.Payer, filter.Payer, StringComparison.OrdinalIgnoreCase));

        return filtered.ToList();
    }

    // ─── QRDA Category I (Patient-Level) ──────────────────────────────────────

    private static string GenerateQrdaCategoryI(CqmPatientResult result, CqmReportState report)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });

        writer.WriteStartDocument();
        writer.WriteStartElement("ClinicalDocument", "urn:hl7-org:v3");
        writer.WriteAttributeString("xmlns", "sdtc", null, "urn:hl7-org:sdtc");

        // Template: QRDA Category I Report
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.24.1.1", "2017-08-01");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.24.1.2", "2019-12-01");

        writer.WriteElementString("typeId", string.Empty);
        WriteCode(writer, "55182-0", "2.16.840.1.113883.6.1", "Quality Measure Report", "LOINC");

        writer.WriteStartElement("title");
        writer.WriteString($"QRDA Category I — {report.MeasureId} — {result.PatientName}");
        writer.WriteEndElement();

        // Effective time (reporting period)
        writer.WriteStartElement("effectiveTime");
        writer.WriteAttributeString("value", report.PeriodEnd.ToString("yyyyMMdd"));
        writer.WriteEndElement();

        // Record target (patient)
        writer.WriteStartElement("recordTarget");
        writer.WriteStartElement("patientRole");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.572");
        writer.WriteAttributeString("extension", result.PatientId);
        writer.WriteEndElement();
        writer.WriteStartElement("patient");
        WriteName(writer, result.PatientName);
        if (!string.IsNullOrEmpty(result.Sex))
        {
            writer.WriteStartElement("administrativeGenderCode");
            writer.WriteAttributeString("code", result.Sex == "M" ? "M" : "F");
            writer.WriteAttributeString("codeSystem", "2.16.840.1.113883.5.1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // patient
        writer.WriteEndElement(); // patientRole
        writer.WriteEndElement(); // recordTarget

        // Component: measure results
        writer.WriteStartElement("component");
        writer.WriteStartElement("structuredBody");
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");

        WriteTemplateId(writer, "2.16.840.1.113883.10.20.24.2.2");
        WriteCode(writer, "55186-1", "2.16.840.1.113883.6.1", "Measure Section", "LOINC");
        writer.WriteElementString("title", "Measure Section");

        // Measure reference
        writer.WriteStartElement("entry");
        writer.WriteStartElement("organizer");
        writer.WriteAttributeString("classCode", "CLUSTER");
        writer.WriteAttributeString("moodCode", "EVN");
        writer.WriteStartElement("reference");
        writer.WriteStartElement("externalDocument");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.738");
        writer.WriteAttributeString("extension", report.MeasureId);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        // Population membership
        WritePopulationEntry(writer, "IPP", "initial-population", result.InInitialPopulation);
        WritePopulationEntry(writer, "DENOM", "denominator", result.InDenominator);
        WritePopulationEntry(writer, "DENEX", "denominator-exclusion", result.IsDenominatorExclusion);
        WritePopulationEntry(writer, "NUMER", "numerator", result.InNumerator);

        writer.WriteEndElement(); // organizer
        writer.WriteEndElement(); // entry

        // Clinical evidence
        if (result.Evidence.Count > 0)
        {
            writer.WriteStartElement("text");
            foreach (string ev in result.Evidence)
            {
                writer.WriteStartElement("paragraph");
                writer.WriteString(ev);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component
        writer.WriteEndElement(); // structuredBody
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // ClinicalDocument
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    // ─── QRDA Category III (Aggregate) ────────────────────────────────────────

    private static string GenerateQrdaCategoryIII(CqmReportState report, List<CqmPatientResult> results)
    {
        // Recompute aggregates for the given result set
        int ipp = results.Count(r => r.InInitialPopulation);
        int denom = results.Count(r => r.InDenominator && !r.IsDenominatorExclusion);
        int denex = results.Count(r => r.IsDenominatorExclusion);
        int numer = results.Count(r => r.InNumerator);
        double perfRate = denom > 0 ? Math.Round((double)numer / denom * 100, 1) : 0;

        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false });

        writer.WriteStartDocument();
        writer.WriteStartElement("ClinicalDocument", "urn:hl7-org:v3");
        writer.WriteAttributeString("xmlns", "sdtc", null, "urn:hl7-org:sdtc");

        // Template: QRDA Category III Report
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.1.1", "2017-06-01");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.1.2", "2019-05-01");

        WriteCode(writer, "55184-6", "2.16.840.1.113883.6.1", "Quality Reporting Document", "LOINC");

        writer.WriteStartElement("title");
        writer.WriteString($"QRDA Category III — {report.MeasureId}");
        writer.WriteEndElement();

        // Effective time (reporting period)
        writer.WriteStartElement("effectiveTime");
        writer.WriteStartElement("low");
        writer.WriteAttributeString("value", report.PeriodStart.ToString("yyyyMMdd"));
        writer.WriteEndElement();
        writer.WriteStartElement("high");
        writer.WriteAttributeString("value", report.PeriodEnd.ToString("yyyyMMdd"));
        writer.WriteEndElement();
        writer.WriteEndElement();

        // Component: aggregate measure data
        writer.WriteStartElement("component");
        writer.WriteStartElement("structuredBody");
        writer.WriteStartElement("component");
        writer.WriteStartElement("section");

        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.2.1");
        WriteCode(writer, "55186-1", "2.16.840.1.113883.6.1", "Measure Section", "LOINC");
        writer.WriteElementString("title", "Measure Section");

        // Measure reference entry
        writer.WriteStartElement("entry");
        writer.WriteStartElement("organizer");
        writer.WriteAttributeString("classCode", "CLUSTER");
        writer.WriteAttributeString("moodCode", "EVN");

        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.3.1", "2016-09-01");

        writer.WriteStartElement("reference");
        writer.WriteStartElement("externalDocument");
        writer.WriteStartElement("id");
        writer.WriteAttributeString("root", "2.16.840.1.113883.4.738");
        writer.WriteAttributeString("extension", report.MeasureId);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        // Aggregate counts
        WriteAggregateCount(writer, "IPP", "initial-population", ipp);
        WriteAggregateCount(writer, "DENOM", "denominator", denom);
        WriteAggregateCount(writer, "DENEX", "denominator-exclusion", denex);
        WriteAggregateCount(writer, "NUMER", "numerator", numer);

        // Performance rate
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.3.14", "2016-09-01");
        WriteCode(writer, "72510-1", "2.16.840.1.113883.6.1", "Performance Rate", "LOINC");
        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "REAL");
        writer.WriteAttributeString("value", (perfRate / 100).ToString("F4"));
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement(); // organizer
        writer.WriteEndElement(); // entry

        writer.WriteEndElement(); // section
        writer.WriteEndElement(); // component
        writer.WriteEndElement(); // structuredBody
        writer.WriteEndElement(); // component

        writer.WriteEndElement(); // ClinicalDocument
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    // ─── XML Helpers ──────────────────────────────────────────────────────────

    private static void WriteTemplateId(XmlWriter writer, string root, string? extension = null)
    {
        writer.WriteStartElement("templateId");
        writer.WriteAttributeString("root", root);
        if (extension != null)
            writer.WriteAttributeString("extension", extension);
        writer.WriteEndElement();
    }

    private static void WriteCode(XmlWriter writer, string code, string codeSystem, string displayName, string codeSystemName)
    {
        writer.WriteStartElement("code");
        writer.WriteAttributeString("code", code);
        writer.WriteAttributeString("codeSystem", codeSystem);
        writer.WriteAttributeString("displayName", displayName);
        writer.WriteAttributeString("codeSystemName", codeSystemName);
        writer.WriteEndElement();
    }

    private static void WriteName(XmlWriter writer, string fullName)
    {
        writer.WriteStartElement("name");
        if (fullName.Contains(','))
        {
            string[] parts = fullName.Split(',', 2);
            writer.WriteElementString("family", parts[0].Trim());
            writer.WriteElementString("given", parts.Length > 1 ? parts[1].Trim() : string.Empty);
        }
        else
        {
            writer.WriteString(fullName);
        }
        writer.WriteEndElement();
    }

    private static void WritePopulationEntry(XmlWriter writer, string popCode, string popName, bool isMember)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");
        WriteCode(writer, popCode, "2.16.840.1.113883.5.4", popName, "ActCode");
        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "BL");
        writer.WriteAttributeString("value", isMember ? "true" : "false");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteAggregateCount(XmlWriter writer, string popCode, string popName, int count)
    {
        writer.WriteStartElement("component");
        writer.WriteStartElement("observation");
        writer.WriteAttributeString("classCode", "OBS");
        writer.WriteAttributeString("moodCode", "EVN");
        WriteTemplateId(writer, "2.16.840.1.113883.10.20.27.3.3");
        WriteCode(writer, popCode, "2.16.840.1.113883.5.4", popName, "ActCode");
        writer.WriteStartElement("value");
        writer.WriteAttributeString("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance", "INT");
        writer.WriteAttributeString("value", count.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }
}
