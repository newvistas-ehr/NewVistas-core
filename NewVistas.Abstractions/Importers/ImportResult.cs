// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Thread-safe import result counters. Importers may run in parallel.
/// </summary>
public class ImportResult
{
    private int _patientCount;
    private int _patientErrors;
    private int _allergyCount;
    private int _allergyErrors;
    private int _problemCount;
    private int _problemErrors;
    private int _orderCount;
    private int _orderErrors;
    private int _labCount;
    private int _labErrors;
    private int _vitalCount;
    private int _vitalErrors;
    private int _tiuCount;
    private int _tiuErrors;
    private int _consultCount;
    private int _consultErrors;
    private int _surgeryCount;
    private int _surgeryErrors;
    private int _radiologyCount;
    private int _radiologyErrors;
    private int _adtCount;
    private int _adtErrors;
    private int _pharmacyCount;
    private int _pharmacyErrors;
    private int _immunizationCount;
    private int _immunizationErrors;
    private int _nursingCount;
    private int _nursingErrors;
    private int _dentalCount;
    private int _dentalErrors;
    private int _mentalHealthCount;
    private int _mentalHealthErrors;
    private int _socialWorkCount;
    private int _socialWorkErrors;
    private int _healthFactorCount;
    private int _healthFactorErrors;
    private int _dietOrderCount;
    private int _dietOrderErrors;
    private int _prostheticsCount;
    private int _prostheticsErrors;
    private int _meansTestCount;
    private int _meansTestErrors;
    private int _scConditionCount;
    private int _scConditionErrors;
    private int _reminderCount;
    private int _reminderErrors;
    private int _careTeamCount;
    private int _careTeamErrors;

    public void RecordSuccess(string domain)
    {
        switch (domain)
        {
            case "Patient": Interlocked.Increment(ref _patientCount); break;
            case "Allergy": Interlocked.Increment(ref _allergyCount); break;
            case "Problem": Interlocked.Increment(ref _problemCount); break;
            case "Order": Interlocked.Increment(ref _orderCount); break;
            case "Lab": Interlocked.Increment(ref _labCount); break;
            case "Vital": Interlocked.Increment(ref _vitalCount); break;
            case "TIU": Interlocked.Increment(ref _tiuCount); break;
            case "Consult": Interlocked.Increment(ref _consultCount); break;
            case "Surgery": Interlocked.Increment(ref _surgeryCount); break;
            case "Radiology": Interlocked.Increment(ref _radiologyCount); break;
            case "ADT": Interlocked.Increment(ref _adtCount); break;
            case "Pharmacy": Interlocked.Increment(ref _pharmacyCount); break;
            case "Immunization": Interlocked.Increment(ref _immunizationCount); break;
            case "Nursing": Interlocked.Increment(ref _nursingCount); break;
            case "Dental": Interlocked.Increment(ref _dentalCount); break;
            case "MentalHealth": Interlocked.Increment(ref _mentalHealthCount); break;
            case "SocialWork": Interlocked.Increment(ref _socialWorkCount); break;
            case "HealthFactor": Interlocked.Increment(ref _healthFactorCount); break;
            case "DietOrder": Interlocked.Increment(ref _dietOrderCount); break;
            case "Prosthetics": Interlocked.Increment(ref _prostheticsCount); break;
            case "MeansTest": Interlocked.Increment(ref _meansTestCount); break;
            case "ScCondition": Interlocked.Increment(ref _scConditionCount); break;
            case "Reminder": Interlocked.Increment(ref _reminderCount); break;
            case "CareTeam": Interlocked.Increment(ref _careTeamCount); break;
        }
    }

    public void RecordError(string domain)
    {
        switch (domain)
        {
            case "Patient": Interlocked.Increment(ref _patientErrors); break;
            case "Allergy": Interlocked.Increment(ref _allergyErrors); break;
            case "Problem": Interlocked.Increment(ref _problemErrors); break;
            case "Order": Interlocked.Increment(ref _orderErrors); break;
            case "Lab": Interlocked.Increment(ref _labErrors); break;
            case "Vital": Interlocked.Increment(ref _vitalErrors); break;
            case "TIU": Interlocked.Increment(ref _tiuErrors); break;
            case "Consult": Interlocked.Increment(ref _consultErrors); break;
            case "Surgery": Interlocked.Increment(ref _surgeryErrors); break;
            case "Radiology": Interlocked.Increment(ref _radiologyErrors); break;
            case "ADT": Interlocked.Increment(ref _adtErrors); break;
            case "Pharmacy": Interlocked.Increment(ref _pharmacyErrors); break;
            case "Immunization": Interlocked.Increment(ref _immunizationErrors); break;
            case "Nursing": Interlocked.Increment(ref _nursingErrors); break;
            case "Dental": Interlocked.Increment(ref _dentalErrors); break;
            case "MentalHealth": Interlocked.Increment(ref _mentalHealthErrors); break;
            case "SocialWork": Interlocked.Increment(ref _socialWorkErrors); break;
            case "HealthFactor": Interlocked.Increment(ref _healthFactorErrors); break;
            case "DietOrder": Interlocked.Increment(ref _dietOrderErrors); break;
            case "Prosthetics": Interlocked.Increment(ref _prostheticsErrors); break;
            case "MeansTest": Interlocked.Increment(ref _meansTestErrors); break;
            case "ScCondition": Interlocked.Increment(ref _scConditionErrors); break;
            case "Reminder": Interlocked.Increment(ref _reminderErrors); break;
            case "CareTeam": Interlocked.Increment(ref _careTeamErrors); break;
        }
    }

    public int PatientCount => _patientCount;
    public int PatientErrors => _patientErrors;
    public int AllergyCount => _allergyCount;
    public int AllergyErrors => _allergyErrors;
    public int ProblemCount => _problemCount;
    public int ProblemErrors => _problemErrors;
    public int OrderCount => _orderCount;
    public int OrderErrors => _orderErrors;
    public int LabCount => _labCount;
    public int LabErrors => _labErrors;
    public int VitalCount => _vitalCount;
    public int VitalErrors => _vitalErrors;
    public int TiuCount => _tiuCount;
    public int TiuErrors => _tiuErrors;
    public int ConsultCount => _consultCount;
    public int ConsultErrors => _consultErrors;
    public int SurgeryCount => _surgeryCount;
    public int SurgeryErrors => _surgeryErrors;
    public int RadiologyCount => _radiologyCount;
    public int RadiologyErrors => _radiologyErrors;
    public int AdtCount => _adtCount;
    public int AdtErrors => _adtErrors;
    public int PharmacyCount => _pharmacyCount;
    public int PharmacyErrors => _pharmacyErrors;
    public int ImmunizationCount => _immunizationCount;
    public int ImmunizationErrors => _immunizationErrors;
    public int NursingCount => _nursingCount;
    public int NursingErrors => _nursingErrors;
    public int DentalCount => _dentalCount;
    public int DentalErrors => _dentalErrors;
    public int MentalHealthCount => _mentalHealthCount;
    public int MentalHealthErrors => _mentalHealthErrors;
    public int SocialWorkCount => _socialWorkCount;
    public int SocialWorkErrors => _socialWorkErrors;
    public int HealthFactorCount => _healthFactorCount;
    public int HealthFactorErrors => _healthFactorErrors;
    public int DietOrderCount => _dietOrderCount;
    public int DietOrderErrors => _dietOrderErrors;
    public int ProstheticsCount => _prostheticsCount;
    public int ProstheticsErrors => _prostheticsErrors;
    public int MeansTestCount => _meansTestCount;
    public int MeansTestErrors => _meansTestErrors;
    public int ScConditionCount => _scConditionCount;
    public int ScConditionErrors => _scConditionErrors;
    public int ReminderCount => _reminderCount;
    public int ReminderErrors => _reminderErrors;
    public int CareTeamCount => _careTeamCount;
    public int CareTeamErrors => _careTeamErrors;

    public int TotalImported =>
        _patientCount + _allergyCount + _problemCount + _orderCount +
        _labCount + _vitalCount + _tiuCount + _consultCount +
        _surgeryCount + _radiologyCount + _adtCount + _pharmacyCount +
        _immunizationCount + _nursingCount + _dentalCount + _mentalHealthCount +
        _socialWorkCount + _healthFactorCount + _dietOrderCount + _prostheticsCount +
        _meansTestCount + _scConditionCount + _reminderCount + _careTeamCount;

    public int TotalErrors =>
        _patientErrors + _allergyErrors + _problemErrors + _orderErrors +
        _labErrors + _vitalErrors + _tiuErrors + _consultErrors +
        _surgeryErrors + _radiologyErrors + _adtErrors + _pharmacyErrors +
        _immunizationErrors + _nursingErrors + _dentalErrors + _mentalHealthErrors +
        _socialWorkErrors + _healthFactorErrors + _dietOrderErrors + _prostheticsErrors +
        _meansTestErrors + _scConditionErrors + _reminderErrors + _careTeamErrors;

    public string GetSummaryText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=== ZWR Import Complete ===");
        sb.AppendLine($"  {"Domain",-15} {"Imported",10} {"Errors",10}");
        sb.AppendLine($"  {new string('-', 37)}");
        AppendRow(sb, "Patients", _patientCount, _patientErrors);
        AppendRow(sb, "Allergies", _allergyCount, _allergyErrors);
        AppendRow(sb, "Problems", _problemCount, _problemErrors);
        AppendRow(sb, "Orders", _orderCount, _orderErrors);
        AppendRow(sb, "Lab Tests", _labCount, _labErrors);
        AppendRow(sb, "Vitals", _vitalCount, _vitalErrors);
        AppendRow(sb, "TIU Documents", _tiuCount, _tiuErrors);
        AppendRow(sb, "Consults", _consultCount, _consultErrors);
        AppendRow(sb, "Surgeries", _surgeryCount, _surgeryErrors);
        AppendRow(sb, "Radiology", _radiologyCount, _radiologyErrors);
        AppendRow(sb, "ADT", _adtCount, _adtErrors);
        AppendRow(sb, "Pharmacy", _pharmacyCount, _pharmacyErrors);
        AppendRow(sb, "Immunizations", _immunizationCount, _immunizationErrors);
        AppendRow(sb, "Nursing", _nursingCount, _nursingErrors);
        AppendRow(sb, "Dental", _dentalCount, _dentalErrors);
        AppendRow(sb, "Mental Health", _mentalHealthCount, _mentalHealthErrors);
        AppendRow(sb, "Social Work", _socialWorkCount, _socialWorkErrors);
        AppendRow(sb, "Health Factors", _healthFactorCount, _healthFactorErrors);
        AppendRow(sb, "Diet Orders", _dietOrderCount, _dietOrderErrors);
        AppendRow(sb, "Prosthetics", _prostheticsCount, _prostheticsErrors);
        AppendRow(sb, "Means Tests", _meansTestCount, _meansTestErrors);
        AppendRow(sb, "SC Conditions", _scConditionCount, _scConditionErrors);
        AppendRow(sb, "Reminders", _reminderCount, _reminderErrors);
        AppendRow(sb, "Care Teams", _careTeamCount, _careTeamErrors);
        sb.AppendLine($"  {new string('-', 37)}");
        AppendRow(sb, "TOTAL", TotalImported, TotalErrors);
        return sb.ToString();
    }

    private static void AppendRow(System.Text.StringBuilder sb, string label, int count, int errors)
    {
        sb.AppendLine($"  {label,-15} {count,10} {errors,10}");
    }
}
