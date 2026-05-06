// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lexicon Search Grain — singleton holding the unified clinical term index.
///
/// Grain Key: "LEX-INDEX"
///
/// Provides cross-vocabulary search across SNOMED CT, ICD-10-CM, CPT, and LOINC.
/// Self-seeds with ~100 common clinical terms on first activation.
///
/// Search mirrors VistA's $$LKUP^LEXSRCH:
///   - Case-insensitive substring match on Designation and FullySpecifiedName
///   - Optional coding system filter (like LEXSRC2.m source restriction)
///   - Exact code lookup by "{system}:{code}" key (like $$LOOK^LEXA)
/// </summary>
public class LexiconSearchGrain : Grain, ILexiconSearchGrain
{
    private readonly IPersistentState<LexiconIndexState> _state;

    public LexiconSearchGrain(
        [PersistentState("lexiconIndexState", "lexiconIndexStore")]
        IPersistentState<LexiconIndexState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (!_state.State.IsLoaded)
            await SeedDemoDataAsync();

        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<LexiconIndexEntry>> SearchAsync(
        string searchText, string? codingSystem, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<LexiconIndexEntry>());

        string query = searchText.Trim();

        IEnumerable<LexiconIndexEntry> results = _state.State.Terms.Values
            .Where(e => e.Designation.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (e.FullySpecifiedName != null &&
                         e.FullySpecifiedName.Contains(query, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrEmpty(codingSystem))
            results = results.Where(e => e.CodingSystem.Equals(codingSystem, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(results
            .OrderBy(e => e.Designation)
            .Take(maxResults)
            .ToList());
    }

    public Task<LexiconIndexEntry?> LookupCodeAsync(string code, string codingSystem)
    {
        string key = $"{codingSystem.ToUpperInvariant()}:{code}";
        _state.State.Terms.TryGetValue(key, out LexiconIndexEntry? entry);
        return Task.FromResult(entry);
    }

    public Task<List<LexiconIndexEntry>> GetByCodingSystemAsync(
        string codingSystem, int maxResults)
    {
        List<LexiconIndexEntry> results = _state.State.Terms.Values
            .Where(e => e.CodingSystem.Equals(codingSystem, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Designation)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public async Task LoadTermsAsync(List<LexiconIndexEntry> entries)
    {
        foreach (LexiconIndexEntry entry in entries)
        {
            string key = $"{entry.CodingSystem.ToUpperInvariant()}:{entry.Code}";
            _state.State.Terms[key] = entry;
        }

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalTerms = _state.State.Terms.Count;

        await _state.WriteStateAsync();
    }

    public Task<LexiconIndexStatus> GetStatusAsync()
    {
        return Task.FromResult(new LexiconIndexStatus
        {
            IsLoaded = _state.State.IsLoaded,
            LastLoadedDate = _state.State.LastLoadedDate,
            TotalTerms = _state.State.TotalTerms,
            SnomedCount = _state.State.Terms.Values.Count(e => e.CodingSystem == "SNOMED"),
            Icd10Count = _state.State.Terms.Values.Count(e => e.CodingSystem == "ICD10"),
            CptCount = _state.State.Terms.Values.Count(e => e.CodingSystem == "CPT"),
            LoincCount = _state.State.Terms.Values.Count(e => e.CodingSystem == "LOINC")
        });
    }

    public async Task SeedDemoDataAsync()
    {
        _state.State.Terms.Clear();

        // ──────────────────────────────────────────────────────────────
        // SNOMED CT — ~30 common clinical terms
        // ──────────────────────────────────────────────────────────────
        AddTerm("SNOMED", "73211009", "Diabetes mellitus", "Diabetes mellitus (disorder)", "disorder");
        AddTerm("SNOMED", "44054006", "Diabetes mellitus type 2", "Diabetes mellitus type 2 (disorder)", "disorder");
        AddTerm("SNOMED", "46635009", "Diabetes mellitus type 1", "Diabetes mellitus type 1 (disorder)", "disorder");
        AddTerm("SNOMED", "38341003", "Hypertension", "Hypertensive disorder, systemic arterial (disorder)", "disorder");
        AddTerm("SNOMED", "195967001", "Asthma", "Asthma (disorder)", "disorder");
        AddTerm("SNOMED", "42343007", "Congestive heart failure", "Congestive heart failure (disorder)", "disorder");
        AddTerm("SNOMED", "13645005", "Chronic obstructive lung disease", "Chronic obstructive lung disease (disorder)", "disorder");
        AddTerm("SNOMED", "35489007", "Depressive disorder", "Depressive disorder (disorder)", "disorder");
        AddTerm("SNOMED", "48694002", "Anxiety disorder", "Anxiety disorder (disorder)", "disorder");
        AddTerm("SNOMED", "22298006", "Myocardial infarction", "Myocardial infarction (disorder)", "disorder");
        AddTerm("SNOMED", "84114007", "Heart failure", "Heart failure (disorder)", "disorder");
        AddTerm("SNOMED", "49436004", "Atrial fibrillation", "Atrial fibrillation (disorder)", "disorder");
        AddTerm("SNOMED", "233604007", "Pneumonia", "Pneumonia (disorder)", "disorder");
        AddTerm("SNOMED", "68566005", "Urinary tract infection", "Urinary tract infectious disease (disorder)", "disorder");
        AddTerm("SNOMED", "66071002", "Viral hepatitis type B", "Viral hepatitis type B (disorder)", "disorder");
        AddTerm("SNOMED", "40930008", "Hypothyroidism", "Hypothyroidism (disorder)", "disorder");
        AddTerm("SNOMED", "34095006", "Dehydration", "Dehydration (disorder)", "disorder");
        AddTerm("SNOMED", "73430006", "Sleep apnea", "Sleep apnea (disorder)", "disorder");
        AddTerm("SNOMED", "267036007", "Dyspnea", "Dyspnea (finding)", "finding");
        AddTerm("SNOMED", "29857009", "Chest pain", "Chest pain (finding)", "finding");
        AddTerm("SNOMED", "25064002", "Headache", "Headache (finding)", "finding");
        AddTerm("SNOMED", "21522001", "Abdominal pain", "Abdominal pain (finding)", "finding");
        AddTerm("SNOMED", "386661006", "Fever", "Fever (finding)", "finding");
        AddTerm("SNOMED", "422587007", "Nausea", "Nausea (finding)", "finding");
        AddTerm("SNOMED", "62315008", "Diarrhea", "Diarrhea (finding)", "finding");
        AddTerm("SNOMED", "271807003", "Rash", "Eruption of skin (disorder)", "disorder");
        AddTerm("SNOMED", "82423001", "Chronic pain", "Chronic pain (finding)", "finding");
        AddTerm("SNOMED", "431855005", "Chronic kidney disease stage 3", "Chronic kidney disease stage 3 (disorder)", "disorder");
        AddTerm("SNOMED", "235595009", "Gastroesophageal reflux disease", "Gastroesophageal reflux disease (disorder)", "disorder");
        AddTerm("SNOMED", "128613002", "Seizure disorder", "Seizure disorder (disorder)", "disorder");

        // ──────────────────────────────────────────────────────────────
        // ICD-10-CM — ~25 common diagnosis codes
        // ──────────────────────────────────────────────────────────────
        AddTerm("ICD10", "E11.9", "Type 2 diabetes mellitus without complications", null, "disorder");
        AddTerm("ICD10", "E10.9", "Type 1 diabetes mellitus without complications", null, "disorder");
        AddTerm("ICD10", "I10", "Essential (primary) hypertension", null, "disorder");
        AddTerm("ICD10", "J45.909", "Unspecified asthma, uncomplicated", null, "disorder");
        AddTerm("ICD10", "I50.9", "Heart failure, unspecified", null, "disorder");
        AddTerm("ICD10", "J44.1", "Chronic obstructive pulmonary disease with acute exacerbation", null, "disorder");
        AddTerm("ICD10", "F32.9", "Major depressive disorder, single episode, unspecified", null, "disorder");
        AddTerm("ICD10", "F41.1", "Generalized anxiety disorder", null, "disorder");
        AddTerm("ICD10", "I21.9", "Acute myocardial infarction, unspecified", null, "disorder");
        AddTerm("ICD10", "I48.91", "Unspecified atrial fibrillation", null, "disorder");
        AddTerm("ICD10", "J18.9", "Pneumonia, unspecified organism", null, "disorder");
        AddTerm("ICD10", "N39.0", "Urinary tract infection, site not specified", null, "disorder");
        AddTerm("ICD10", "E03.9", "Hypothyroidism, unspecified", null, "disorder");
        AddTerm("ICD10", "E86.0", "Dehydration", null, "disorder");
        AddTerm("ICD10", "G47.33", "Obstructive sleep apnea", null, "disorder");
        AddTerm("ICD10", "R06.00", "Dyspnea, unspecified", null, "finding");
        AddTerm("ICD10", "R07.9", "Chest pain, unspecified", null, "finding");
        AddTerm("ICD10", "R51.9", "Headache, unspecified", null, "finding");
        AddTerm("ICD10", "R10.9", "Unspecified abdominal pain", null, "finding");
        AddTerm("ICD10", "R50.9", "Fever, unspecified", null, "finding");
        AddTerm("ICD10", "K21.0", "Gastro-esophageal reflux disease with esophagitis", null, "disorder");
        AddTerm("ICD10", "N18.3", "Chronic kidney disease, stage 3", null, "disorder");
        AddTerm("ICD10", "M54.5", "Low back pain", null, "finding");
        AddTerm("ICD10", "J06.9", "Acute upper respiratory infection, unspecified", null, "disorder");
        AddTerm("ICD10", "Z87.39", "Personal history of other diseases of the musculoskeletal system", null, "finding");

        // ──────────────────────────────────────────────────────────────
        // CPT — ~25 common procedure codes
        // ──────────────────────────────────────────────────────────────
        AddTerm("CPT", "99213", "Office/outpatient visit, established patient, low complexity", null, "procedure");
        AddTerm("CPT", "99214", "Office/outpatient visit, established patient, moderate complexity", null, "procedure");
        AddTerm("CPT", "99215", "Office/outpatient visit, established patient, high complexity", null, "procedure");
        AddTerm("CPT", "99203", "Office/outpatient visit, new patient, low complexity", null, "procedure");
        AddTerm("CPT", "99204", "Office/outpatient visit, new patient, moderate complexity", null, "procedure");
        AddTerm("CPT", "99205", "Office/outpatient visit, new patient, high complexity", null, "procedure");
        AddTerm("CPT", "99212", "Office/outpatient visit, established patient, straightforward", null, "procedure");
        AddTerm("CPT", "36415", "Collection of venous blood by venipuncture", null, "procedure");
        AddTerm("CPT", "71046", "Radiologic examination, chest, 2 views", null, "procedure");
        AddTerm("CPT", "93000", "Electrocardiogram, routine, 12-lead", null, "procedure");
        AddTerm("CPT", "85025", "Blood count; complete (CBC), automated", null, "procedure");
        AddTerm("CPT", "80053", "Comprehensive metabolic panel", null, "procedure");
        AddTerm("CPT", "80048", "Basic metabolic panel", null, "procedure");
        AddTerm("CPT", "81001", "Urinalysis, automated, with microscopy", null, "procedure");
        AddTerm("CPT", "87880", "Infectious agent antigen detection, Strep A", null, "procedure");
        AddTerm("CPT", "99232", "Subsequent hospital care, moderate complexity", null, "procedure");
        AddTerm("CPT", "99233", "Subsequent hospital care, high complexity", null, "procedure");
        AddTerm("CPT", "99223", "Initial hospital care, high complexity", null, "procedure");
        AddTerm("CPT", "99291", "Critical care, first 30-74 minutes", null, "procedure");
        AddTerm("CPT", "90837", "Psychotherapy, 60 minutes", null, "procedure");
        AddTerm("CPT", "90834", "Psychotherapy, 45 minutes", null, "procedure");
        AddTerm("CPT", "96372", "Therapeutic injection, subcutaneous or intramuscular", null, "procedure");
        AddTerm("CPT", "99385", "Preventive visit, new patient, 18-39 years", null, "procedure");
        AddTerm("CPT", "99395", "Preventive visit, established patient, 18-39 years", null, "procedure");
        AddTerm("CPT", "10060", "Incision and drainage of abscess, simple", null, "procedure");

        // ──────────────────────────────────────────────────────────────
        // LOINC — ~20 common laboratory observation codes
        // ──────────────────────────────────────────────────────────────
        AddTerm("LOINC", "2345-7", "Glucose [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "718-7", "Hemoglobin [Mass/volume] in Blood", null, "observable entity");
        AddTerm("LOINC", "4548-4", "Hemoglobin A1c/Hemoglobin.total in Blood", null, "observable entity");
        AddTerm("LOINC", "2160-0", "Creatinine [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "2951-2", "Sodium [Moles/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "6298-4", "Potassium [Moles/volume] in Blood", null, "observable entity");
        AddTerm("LOINC", "2823-3", "Potassium [Moles/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "789-8", "Erythrocytes [#/volume] in Blood by Automated count", null, "observable entity");
        AddTerm("LOINC", "6690-2", "Leukocytes [#/volume] in Blood by Automated count", null, "observable entity");
        AddTerm("LOINC", "777-3", "Platelets [#/volume] in Blood by Automated count", null, "observable entity");
        AddTerm("LOINC", "1742-6", "Alanine aminotransferase [Enzymatic activity/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "1920-8", "Aspartate aminotransferase [Enzymatic activity/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "1975-2", "Bilirubin.total [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "2085-9", "Cholesterol in HDL [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "2093-3", "Cholesterol [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "2571-8", "Triglyceride [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "3094-0", "Urea nitrogen [Mass/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "33914-3", "Glomerular filtration rate/1.73 sq M.predicted", null, "observable entity");
        AddTerm("LOINC", "2028-9", "Carbon dioxide, total [Moles/volume] in Serum or Plasma", null, "observable entity");
        AddTerm("LOINC", "17861-6", "Calcium [Mass/volume] in Serum or Plasma", null, "observable entity");

        _state.State.IsLoaded = true;
        _state.State.LastLoadedDate = DateTime.UtcNow;
        _state.State.TotalTerms = _state.State.Terms.Count;

        await _state.WriteStateAsync();
    }

    public async Task ClearAsync()
    {
        _state.State.Terms.Clear();
        _state.State.IsLoaded = false;
        _state.State.LastLoadedDate = null;
        _state.State.TotalTerms = 0;

        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Helper to add a term to the in-memory index.
    /// </summary>
    private void AddTerm(
        string codingSystem,
        string code,
        string designation,
        string? fullySpecifiedName,
        string? semanticType)
    {
        string key = $"{codingSystem}:{code}";
        _state.State.Terms[key] = new LexiconIndexEntry
        {
            Code = code,
            CodingSystem = codingSystem,
            Designation = designation,
            FullySpecifiedName = fullySpecifiedName,
            IsActive = true,
            SemanticType = semanticType
        };
    }
}
