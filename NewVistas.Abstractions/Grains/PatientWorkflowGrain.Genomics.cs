// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Hereditary genetics &amp; family history orchestration (HEREDITARY_GENETICS). Stores interpreted
/// genetic test reports with coded reportable variants (HGVS / ClinVar) on a per-patient
/// <see cref="IGenomicsGrain"/>, and structured family history on <see cref="IFamilyHistoryGrain"/>.
/// The curated <see cref="HereditaryRisk"/> knowledge base derives hereditary-syndrome findings from
/// germline variants and red-flag patterns from the family history (referral decision support).
/// Read-only decision support — never auto-orders. Access is open (flag-gated), matching the
/// genetics blueprint's "results-back / referral-out" model.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IGenomicsGrain Genomics() => GrainFactory.GetGrain<IGenomicsGrain>(PatientId);
    private IFamilyHistoryGrain FamilyHx() => GrainFactory.GetGrain<IFamilyHistoryGrain>(PatientId);

    // ─── Genetic test reports / variants ────────────────────────────────────

    public async Task<string> RecordGeneticTestReportAsync(
        string testName, string lab, GeneticTestMethod method, string indication,
        DateTime? collectionDate, DateTime? reportDate, GeneticReportResult overallResult,
        string orderingProvider, string notes, string recordedBy)
    {
        return await Genomics().RecordReportAsync(new GeneticTestReport
        {
            TestName = testName,
            Lab = lab,
            Method = method,
            Indication = indication,
            CollectionDate = collectionDate,
            ReportDate = reportDate,
            OverallResult = overallResult,
            OrderingProvider = orderingProvider,
            Notes = notes,
            RecordedBy = recordedBy
        });
    }

    public Task AddGeneticVariantAsync(
        string reportId, string gene, string hgvsCoding, string hgvsProtein, string transcript,
        VariantClassification classification, VariantZygosity zygosity, VariantOrigin origin,
        string clinVarId, string dbSnpId, string notes)
        => Genomics().AddVariantAsync(reportId, new GeneticVariant
        {
            Gene = gene,
            HgvsCoding = hgvsCoding,
            HgvsProtein = hgvsProtein,
            Transcript = transcript,
            Classification = classification,
            Zygosity = zygosity,
            Origin = origin,
            ClinVarId = clinVarId,
            DbSnpId = dbSnpId,
            Notes = notes
        });

    public Task RemoveGeneticReportAsync(string reportId) => Genomics().RemoveReportAsync(reportId);

    public Task<GenomicsState> GetGenomicsProfileAsync() => Genomics().GetAsync();

    /// <summary>Hereditary-syndrome findings from the patient's germline variants (curated KB).</summary>
    public async Task<List<HereditaryFinding>> GetHereditaryFindingsAsync()
    {
        GenomicsState g = await Genomics().GetAsync();
        return HereditaryRisk.AssessVariants(g.Reports.SelectMany(r => r.Variants));
    }

    // ─── Family history ─────────────────────────────────────────────────────

    public async Task<string> AddFamilyMemberAsync(
        FamilyRelationship relationship, string name, string sex, FamilyVitalStatus vitalStatus,
        int? ageYears, int? ageAtDeath, string causeOfDeath, string notes)
    {
        return await FamilyHx().AddMemberAsync(new FamilyMemberHistoryEntry
        {
            Relationship = relationship,
            Name = name,
            Sex = sex,
            VitalStatus = vitalStatus,
            AgeYears = ageYears,
            AgeAtDeath = ageAtDeath,
            CauseOfDeath = causeOfDeath,
            Notes = notes
        });
    }

    public Task AddFamilyConditionAsync(string memberId, string condition, string code, int? ageAtDiagnosis, string notes)
        => FamilyHx().AddConditionAsync(memberId, new FamilyConditionEntry
        {
            Condition = condition,
            Code = code,
            AgeAtDiagnosis = ageAtDiagnosis,
            Notes = notes
        });

    public Task RemoveFamilyMemberAsync(string memberId) => FamilyHx().RemoveMemberAsync(memberId);

    public Task<FamilyHistoryState> GetFamilyHistoryAsync() => FamilyHx().GetAsync();

    /// <summary>Hereditary-cancer red-flag patterns in the family history (referral decision support).</summary>
    public async Task<List<FamilyRiskFlag>> GetFamilyRiskFlagsAsync()
    {
        FamilyHistoryState f = await FamilyHx().GetAsync();
        return HereditaryRisk.AssessFamilyHistory(f.Members);
    }
}
