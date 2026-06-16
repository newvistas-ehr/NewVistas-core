// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Imports ^SW (VistA SOCIAL WORK file #707) into SocialWorkAssessment grains
/// via the PatientWorkflowGrain.
/// </summary>
public class SocialWorkImporter
{
    private readonly IGrainFactory _grainFactory;
    private readonly IenMap _ienMap;
    private readonly ILogger _logger;

    public SocialWorkImporter(IGrainFactory grainFactory, IenMap ienMap, ILogger logger)
    {
        _grainFactory = grainFactory;
        _ienMap = ienMap;
        _logger = logger;
    }

    public async Task ImportAsync(
        Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>> records,
        ImportResult result)
    {
        var socialWorkGroups = records
            .Where(kvp => kvp.Key.Global == "SW" && kvp.Key.FileNumber == null)
            .GroupBy(kvp => kvp.Key.Ien);

        int count = 0;
        foreach (var group in socialWorkGroups)
        {
            try
            {
                long ien = group.Key;
                List<ZwrRecord> allRecords = group.SelectMany(g => g.Value).ToList();

                // Node 0: PatientDFN;DPT(^AssessType^Date(FM)^SWerDFN;VA(200,^RiskLevel
                ZwrRecord? zeroNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "0");

                if (zeroNode == null) continue;

                // Patient reference
                string? patientDfnStr = ZwrParser.Piece(zeroNode.Value, 1);
                long patientDfn = 0;
                if (patientDfnStr != null)
                    long.TryParse(patientDfnStr.Split(';')[0], out patientDfn);

                string? patientKey = patientDfn > 0
                    ? _ienMap.TryGetKey("DPT", patientDfn)
                    : null;

                if (patientKey == null) continue;

                // Assessment type
                string? assessTypeStr = ZwrParser.Piece(zeroNode.Value, 2);
                if (!Enum.TryParse<SocialWorkAssessmentType>(assessTypeStr, ignoreCase: true,
                    out SocialWorkAssessmentType assessType))
                    assessType = SocialWorkAssessmentType.Psychosocial;

                DateTime assessDate = ZwrParser.ParseFmDate(ZwrParser.Piece(zeroNode.Value, 3))
                    ?? DateTime.UtcNow;

                // Social worker reference
                string? swDfnStr = ZwrParser.Piece(zeroNode.Value, 4);
                string? socialWorkerId = null;
                string? socialWorkerName = null;
                if (swDfnStr != null)
                {
                    long.TryParse(swDfnStr.Split(';')[0], out long swDfn);
                    if (swDfn > 0)
                    {
                        socialWorkerId = _ienMap.TryGetKey("VA200", swDfn) ?? $"STAFF-{swDfn}";
                        socialWorkerName = await ResolveProviderNameAsync(socialWorkerId, swDfn);
                    }
                }

                // Risk level
                string? riskLevelStr = ZwrParser.Piece(zeroNode.Value, 5);
                if (!Enum.TryParse<SocialWorkRiskLevel>(riskLevelStr, ignoreCase: true,
                    out SocialWorkRiskLevel riskLevel))
                    riskLevel = SocialWorkRiskLevel.Unknown;

                // Node 1: Housing^Employment^SocialSupport^SubstanceUse^LegalIssues
                ZwrRecord? oneNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "1");

                string? housing = oneNode != null ? ZwrParser.Piece(oneNode.Value, 1) : null;
                string? employment = oneNode != null ? ZwrParser.Piece(oneNode.Value, 2) : null;
                string? socialSupport = oneNode != null ? ZwrParser.Piece(oneNode.Value, 3) : null;
                string? substanceUse = oneNode != null ? ZwrParser.Piece(oneNode.Value, 4) : null;

                // Node 2: DischargePlan^DischargeBarriers^Recommendations
                ZwrRecord? twoNode = allRecords.FirstOrDefault(r =>
                    r.Subscripts.Count == 1 && r.Subscripts[0] == "2");

                string? dischargePlan = twoNode != null ? ZwrParser.Piece(twoNode.Value, 1) : null;

                List<string>? dischargeBarriers = null;
                string? barriersStr = twoNode != null ? ZwrParser.Piece(twoNode.Value, 2) : null;
                if (!string.IsNullOrEmpty(barriersStr))
                    dischargeBarriers = barriersStr.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                string? recommendations = twoNode != null ? ZwrParser.Piece(twoNode.Value, 3) : null;

                IPatientWorkflowGrain workflow =
                    _grainFactory.GetGrain<IPatientWorkflowGrain>(patientKey);

                await workflow.CreateSocialWorkAssessmentAsync(
                    assessType,
                    assessDate,
                    socialWorkerId,
                    socialWorkerName,
                    riskLevel,
                    housing,
                    employment,
                    socialSupport,
                    null,               // financialStressors
                    substanceUse,
                    null,               // abuseConcernsIdentified
                    null,               // safetyPlanInPlace
                    null,               // anticipatedDischargeDate
                    null,               // dischargeDisposition
                    dischargePlan,
                    dischargeBarriers,
                    recommendations,
                    null,               // notes
                    null, null);        // locationId, locationName

                result.RecordSuccess("SocialWork");
                if (++count % 50 == 0)
                    _logger.LogInformation("Imported {Count} social work assessments so far", count);
            }
            catch (Exception ex)
            {
                result.RecordError("SocialWork");
                _logger.LogError(ex, "Failed to import social work assessment IEN {Ien}", group.Key);
            }
        }
    }

    private async Task<string> ResolveProviderNameAsync(string providerKey, long providerDfn)
    {
        try
        {
            INewPersonGrain person = _grainFactory.GetGrain<INewPersonGrain>(providerKey);
            string name = await person.GetDisplayNameAsync();
            return string.IsNullOrEmpty(name) ? $"Provider {providerDfn}" : name;
        }
        catch
        {
            return $"Provider {providerDfn}";
        }
    }
}
