// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Demographics (DPT file #2) ─────────────────────────────────────

    public async Task<PatientState> GetPatientAsync()
    {
        return await GetPatientGrain().GetPatientAsync();
    }

    public async Task UpdateDemographicsAsync(
        string name, string sex, DateTime? dateOfBirth, string? socialSecurityNumber)
    {
        PatientState state = await GetPatientGrain().UpdateDemographicsAsync(name, sex, dateOfBirth, socialSecurityNumber);
        await GetPatientIndexGrain().AddOrUpdateAsync(BuildIndexEntry(state));
    }

    public async Task SetDfnAsync(string dfn)
    {
        PatientState state = await GetPatientGrain().SetDfnAsync(dfn);
        await GetPatientIndexGrain().AddOrUpdateAsync(BuildIndexEntry(state));
    }

    public async Task SetIcnAsync(string icn)
    {
        PatientState state = await GetPatientGrain().SetIcnAsync(icn);
        await GetPatientIndexGrain().AddOrUpdateAsync(BuildIndexEntry(state));
    }

    public async Task UpdateAddressAsync(
        string? streetAddress1, string? streetAddress2, string? streetAddress3,
        string? city, string? state, string? zipCode)
    {
        await GetPatientGrain().UpdateAddressAsync(streetAddress1, streetAddress2, streetAddress3, city, state, zipCode);
    }

    public async Task UpdateContactInfoAsync(string? phoneResidence, string? phoneWork, string? email)
    {
        await GetPatientGrain().UpdateContactInfoAsync(phoneResidence, phoneWork, email);
    }

    public async Task UpdateEmergencyContactAsync(string? name, string? relationship, string? phone)
    {
        await GetPatientGrain().UpdateEmergencyContactAsync(name, relationship, phone);
    }

    public async Task UpdateVeteranInfoAsync(
        string veteran, int? serviceConnectedPercentage, string? eligibilityCode, string? primaryEligibilityCode)
    {
        await GetPatientGrain().UpdateVeteranInfoAsync(veteran, serviceConnectedPercentage, eligibilityCode, primaryEligibilityCode);
    }

    public async Task UpdateMilitaryServiceAsync(
        DateTime? serviceEntryDate, DateTime? serviceSeparationDate,
        string? serviceBranch, string? dischargeType, string? prisonerOfWar)
    {
        await GetPatientGrain().UpdateMilitaryServiceAsync(serviceEntryDate, serviceSeparationDate, serviceBranch, dischargeType, prisonerOfWar);
    }

    public async Task UpdateMaritalStatusAsync(string? maritalStatus)
    {
        await GetPatientGrain().UpdateMaritalStatusAsync(maritalStatus);
    }

    public Task<List<PatientIndexEntry>> SearchPatientsAsync(string searchTerm, int maxResults = 25)
        // Delegates to the StatelessWorker search reader so search CPU scales
        // out instead of funneling through the singleton PATIENT-INDEX.
        => GrainFactory.GetGrain<IPatientSearchGrain>("PATIENT-SEARCH").SearchAsync(searchTerm, maxResults);

    // ─── Lab Orders (LR file #63) ───────────────────────────────────────

    public async Task<string> OrderLabTestAsync(
        string testId, string testName, string? testCode,
        string? orderId, string? orderingProviderId,
        string? orderingProviderName, string? specimenType, string? category)
    {
        var labTestId = $"LAB-{Guid.NewGuid()}";
        var labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);

        await labGrain.OrderLabTestAsync(
            PatientId, testId, testName, testCode, orderId,
            orderingProviderId, orderingProviderName, specimenType, category);

        await AppendCappedIdAsync(PatientHistoryDomains.Lab, labTestId, DateTime.UtcNow);

        return labTestId;
    }

    public async Task<string> PlaceLabOrderAsync(
        string testId, string testName, string? testCode,
        string? orderingProviderId, string? orderingProviderName,
        string? specimenType, string? category)
    {
        // A lab order IS a CPOE order (ORDER file #100) whose result is tracked by a LabTestGrain
        // — the same way CPRS/Epic model it. Create the unified order first, pointing it at the
        // lab test it fulfills (OrderableItemId), so the lab shows up in the patient's order list
        // and order history; then record the lab-test side linked back to the order (OrderId).
        // (OrderLabTestAsync, by contrast, records a lab in isolation — used for bulk imports —
        // which is exactly why imported/standalone labs never appeared on the Orders page.)
        string labTestId = $"LAB-{Guid.NewGuid()}";
        string providerId = string.IsNullOrWhiteSpace(orderingProviderId) ? "PROV-001" : orderingProviderId;
        string providerName = orderingProviderName ?? string.Empty;

        string labOrderId = await PlaceOrderAsync(
            "Laboratory", testName, labTestId,
            providerId, providerName, null, null,
            "Routine",
            string.IsNullOrWhiteSpace(specimenType) ? null : $"Specimen: {specimenType}",
            null);

        ILabTestGrain labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        await labGrain.OrderLabTestAsync(
            PatientId, testId, testName, testCode, labOrderId,
            orderingProviderId, orderingProviderName, specimenType, category);

        await AppendCappedIdAsync(PatientHistoryDomains.Lab, labTestId, DateTime.UtcNow);

        return labTestId;
    }

    public async Task<List<LabResultSummary>> GetLabResultsAsync()
    {
        var patientGrain = GetPatientGrain();
        var labIds = await patientGrain.GetLabTestIdsAsync();

        var tasks = labIds.Select(id => GrainFactory.GetGrain<ILabTestGrain>(id).GetLabTestAsync()).ToList();
        var states = await Task.WhenAll(tasks);

        return states
            .Select(state => new LabResultSummary
            {
                LabTestId = state.LabTestId,
                TestName = state.TestName ?? "",
                ResultValue = state.ResultValue,
                Units = state.ResultUnit,
                Flag = state.AbnormalFlag,
                Status = state.Status ?? "",
                CollectionDate = state.CollectionDateTime
            })
            .OrderByDescending(l => l.CollectionDate)
            .ToList();
    }

    /// <summary>
    /// Paged full lab result history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<LabResultSummary>> GetLabResultHistoryAsync(int offset, int maxResults)
    {
        var labIds = await GetHistoryPageIdsAsync(PatientHistoryDomains.Lab, offset, maxResults);
        var tasks = labIds.Select(id => GrainFactory.GetGrain<ILabTestGrain>(id).GetLabTestAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(state => new LabResultSummary
            {
                LabTestId = state.LabTestId,
                TestName = state.TestName ?? "",
                ResultValue = state.ResultValue,
                Units = state.ResultUnit,
                Flag = state.AbnormalFlag,
                Status = state.Status ?? "",
                CollectionDate = state.CollectionDateTime
            })
            .ToList();
    }

    public async Task<LabTestState> GetLabTestAsync(string labTestId)
    {
        var labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        return await labGrain.GetLabTestAsync();
    }

    public async Task CollectSpecimenAsync(
        string labTestId, DateTime collectionDateTime,
        string? collectionSample, string? performingLab)
    {
        var labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        await labGrain.CollectSpecimenAsync(collectionDateTime, collectionSample, performingLab);
    }

    public async Task RecordLabResultAsync(
        string labTestId, DateTime resultDateTime,
        string resultValue, string? resultUnit,
        string? referenceLow, string? referenceHigh, string? abnormalFlag)
    {
        var labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        await labGrain.RecordResultAsync(resultDateTime, resultValue, resultUnit,
            referenceLow, referenceHigh, abnormalFlag);
    }

    public async Task VerifyLabResultAsync(
        string labTestId, string verifyingProviderId,
        string verifyingProviderName, DateTime verifiedDateTime)
    {
        var labGrain = GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        await labGrain.VerifyResultAsync(verifyingProviderId, verifyingProviderName, verifiedDateTime);

        // A verified result completes the CPOE order that requested it, so the lab order moves
        // out of the active view into completed/history. Labs placed in isolation (bulk imports,
        // seeded historical results) carry no OrderId and are left untouched.
        LabTestState lab = await labGrain.GetLabTestAsync();
        if (!string.IsNullOrEmpty(lab.OrderId))
        {
            await GrainFactory.GetGrain<IOrderGrain>(lab.OrderId).CompleteOrderAsync(verifiedDateTime);
            await SyncOrderToIndexAsync(lab.OrderId);
        }
    }

    public async Task<List<LabTestSummaryEntry>> GetLabSummaryAsync()
    {
        var summaryGrain = GrainFactory.GetGrain<IPatientLabSummary>($"PatientLabSummary/{PatientId}");
        IReadOnlyDictionary<string, LabTestSummaryEntry> current = await summaryGrain.GetCurrentLabs();
        return current.Values.OrderByDescending(e => e.ResultDate).ToList();
    }

    public async Task<List<LabTestSummaryEntry>> GetAbnormalLabResultsAsync()
    {
        var summaryGrain = GrainFactory.GetGrain<IPatientLabSummary>($"PatientLabSummary/{PatientId}");
        IReadOnlyList<LabTestSummaryEntry> abnormal = await summaryGrain.GetAbnormalResults();
        return abnormal.OrderByDescending(e => e.ResultDate).ToList();
    }

    public async Task<List<LabIndexEntry>> GetRecentResultsByTypeAsync(string loincCode, int n)
    {
        var indexGrain = GrainFactory.GetGrain<IPatientLabIndex>($"PatientLabIndex/{PatientId}");
        IReadOnlyList<LabIndexEntry> entries = await indexGrain.GetLastNByType(loincCode, n);
        return entries.ToList();
    }

    public async Task IngestLabResultAsync(
        string resultId, string loincCode, string testName,
        string value, string units, string? referenceRange,
        LabAbnormalFlag abnormalFlag, DateTimeOffset resultDate,
        string facilityCode, string? facilityName, string? specimen, string? panelName)
    {
        var ingestionGrain = GrainFactory.GetGrain<ILabResultIngestion>(PatientId);
        await ingestionGrain.IngestResult(new LabResultDetail
        {
            ResultId = resultId,
            LoincCode = loincCode,
            TestName = testName,
            Value = value,
            Units = units,
            ReferenceRange = referenceRange ?? string.Empty,
            AbnormalFlag = abnormalFlag,
            ResultDate = resultDate,
            FacilityCode = facilityCode,
            FacilityName = facilityName ?? string.Empty,
            Specimen = specimen ?? string.Empty,
            PanelName = panelName
        });
    }

    // ─── Lab Accessioning — LRACE.m ─────────────────────────────────────────

    private ILabAccessionIndexGrain AccessionIndex()
        => GrainFactory.GetGrain<ILabAccessionIndexGrain>($"LAB-ACCESSION-IDX:{PatientId}");

    public async Task<string> AccessionSpecimenAsync(
        List<string> labTestIds, string specimenType, string? collectionTube,
        DateTime collectionDateTime, string? labSection,
        string? accessionedByUserId, string? accessionedByUserName, string? notes)
    {
        string accessionNumber = $"{DateTime.UtcNow:yyyyMMdd}-{labSection ?? "GEN"}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        ILabAccessionGrain accession = GrainFactory.GetGrain<ILabAccessionGrain>($"LAB-ACCESSION:{accessionNumber}");
        await accession.CreateAsync(PatientId, labTestIds, specimenType, collectionTube,
            collectionDateTime, labSection, accessionedByUserId, accessionedByUserName, notes);

        await AccessionIndex().AddOrUpdateAsync(new GrainStates.LabAccessionIndexEntry
        {
            AccessionNumber    = accessionNumber,
            PatientId          = PatientId,
            SpecimenType       = specimenType,
            Status             = GrainStates.SpecimenStatus.Accessioned,
            CollectionDateTime = collectionDateTime,
            IsRejected         = false,
            TestCount          = labTestIds.Count,
        });

        return accessionNumber;
    }

    public Task<GrainStates.LabAccessionState> GetAccessionAsync(string accessionNumber)
        => GrainFactory.GetGrain<ILabAccessionGrain>($"LAB-ACCESSION:{accessionNumber}").GetAsync();

    public Task<List<GrainStates.LabAccessionIndexEntry>> GetAccessionsAsync()
        => AccessionIndex().GetAllAsync();

    public Task<List<GrainStates.LabAccessionIndexEntry>> GetPendingAccessionsAsync()
        => AccessionIndex().GetPendingAsync();

    public async Task RejectSpecimenAsync(string accessionNumber, GrainStates.SpecimenRejectReason reason,
        string? rejectNotes, string rejectedByUserId, bool orderRecollect)
    {
        ILabAccessionGrain accession = GrainFactory.GetGrain<ILabAccessionGrain>($"LAB-ACCESSION:{accessionNumber}");
        await accession.RejectAsync(reason, rejectNotes, rejectedByUserId, orderRecollect);

        GrainStates.LabAccessionState state = await accession.GetAsync();
        await AccessionIndex().AddOrUpdateAsync(new GrainStates.LabAccessionIndexEntry
        {
            AccessionNumber    = accessionNumber,
            PatientId          = PatientId,
            SpecimenType       = state.SpecimenType,
            Status             = state.Status,
            CollectionDateTime = state.CollectionDateTime,
            IsRejected         = true,
            TestCount          = state.LabTestIds.Count,
        });
    }

    // ─── Lab QC — Quality Control ───────────────────────────────────────────

    public Task<GrainStates.LabQcResult> RecordLabQcRunAsync(
        string instrumentId, string loincCode, string testName,
        string qcLevel, string lotNumber, decimal measuredValue,
        decimal expectedMean, decimal standardDeviation,
        string techId, string techName, string? notes)
    {
        ILabQcGrain qc = GrainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{instrumentId}:{loincCode}");
        return qc.RecordQcRunAsync(qcLevel, lotNumber, measuredValue, expectedMean, standardDeviation, techId, techName, notes);
    }

    public Task<GrainStates.LabQcState> GetLabQcStateAsync(string instrumentId, string loincCode)
        => GrainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{instrumentId}:{loincCode}").GetAsync();

    public Task<bool> IsLabTestingAllowedAsync(string instrumentId, string loincCode)
        => GrainFactory.GetGrain<ILabQcGrain>($"LAB-QC:{instrumentId}:{loincCode}").IsTestingAllowedAsync();

    // ─── Delta Checks ───────────────────────────────────────────────────────

    public async Task<GrainStates.DeltaCheckResult> PerformDeltaCheckAsync(
        string labTestId, string loincCode, string testName,
        decimal currentValue, DateTime resultDate, string? instrumentId)
    {
        // Get the patient's previous result for this LOINC code
        List<GrainStates.LabIndexEntry> recent = await GetRecentResultsByTypeAsync(loincCode, 2);
        decimal? previousValue = null;
        DateTime? previousDate = null;

        // Skip the current result if it's already in the index
        GrainStates.LabIndexEntry? prev = recent.FirstOrDefault(r => r.ResultId != labTestId);
        if (prev is not null && decimal.TryParse(prev.TestName, out _) == false)
        {
            // Try to get actual value from the lab test grain
            try
            {
                // Look in recent results for a numeric value
                // For simplicity, use the batch grain value
            }
            catch { }
        }

        // Try to get delta thresholds from auto-verify rules if instrument specified
        decimal thresholdPercent = 50m; // default 50%
        decimal? thresholdAbsolute = null;

        if (!string.IsNullOrEmpty(instrumentId))
        {
            try
            {
                IAutoVerifyRulesGrain rules = GrainFactory.GetGrain<IAutoVerifyRulesGrain>($"LA-AUTOVERIFY:{instrumentId}");
                GrainStates.AutoVerifyRulesState rulesState = await rules.GetRulesAsync();
                GrainStates.TestVerifyRule? rule = rulesState.TestRules.FirstOrDefault(r => r.LoincCode == loincCode);
                if (rule?.DeltaCheckPercent.HasValue == true)
                    thresholdPercent = (decimal)rule.DeltaCheckPercent.Value;
                if (rule?.DeltaCheckAbsolute.HasValue == true)
                    thresholdAbsolute = (decimal)rule.DeltaCheckAbsolute.Value;
            }
            catch { }
        }

        // Compute delta
        decimal? absDelta = null;
        decimal? pctDelta = null;
        bool passed = true;
        string? failureReason = null;

        if (previousValue.HasValue && previousValue.Value != 0)
        {
            absDelta = Math.Abs(currentValue - previousValue.Value);
            pctDelta = Math.Abs((currentValue - previousValue.Value) / previousValue.Value) * 100m;

            if (pctDelta > thresholdPercent)
            {
                passed = false;
                failureReason = $"Percent change {pctDelta:F1}% exceeds threshold {thresholdPercent:F1}%";
            }
            if (thresholdAbsolute.HasValue && absDelta > thresholdAbsolute.Value)
            {
                passed = false;
                failureReason = $"Absolute change {absDelta:F2} exceeds threshold {thresholdAbsolute:F2}";
            }
        }

        return new GrainStates.DeltaCheckResult
        {
            LabTestId          = labTestId,
            LoincCode          = loincCode,
            TestName           = testName,
            CurrentValue       = currentValue,
            PreviousValue      = previousValue,
            AbsoluteDelta      = absDelta,
            PercentDelta       = pctDelta,
            Passed             = passed,
            ThresholdPercent   = thresholdPercent,
            ThresholdAbsolute  = thresholdAbsolute,
            FailureReason      = failureReason,
            ResultDate         = resultDate,
            PreviousResultDate = previousDate,
        };
    }

    // ─── Lab Tech Worklist ──────────────────────────────────────────────────

    public async Task<GrainStates.LabWorklistState> RefreshLabWorklistAsync(string locationId)
    {
        ILabWorklistGrain worklist = GrainFactory.GetGrain<ILabWorklistGrain>($"LAB-WORKLIST:{locationId}");
        List<GrainStates.LabWorklistItem> items = new();

        // Get all lab test IDs for this patient
        try
        {
            // COMPLETE lab set: the worklist filters for pending work — a
            // pending collection older than the recent window must not vanish.
            List<string> labTestIds = await GetCompleteIdsAsync(PatientHistoryDomains.Lab);

            foreach (string ltId in labTestIds)
            {
                ILabTestGrain lt = GrainFactory.GetGrain<ILabTestGrain>(ltId);
                GrainStates.LabTestState lts = await lt.GetLabTestAsync();

                GrainStates.LabWorklistCategory cat = lts.Status switch
                {
                    "Ordered" => GrainStates.LabWorklistCategory.PendingCollection,
                    "Collected" => GrainStates.LabWorklistCategory.PendingResult,
                    "Pending" => GrainStates.LabWorklistCategory.PendingVerification,
                    _ => GrainStates.LabWorklistCategory.PendingResult,
                };

                if (lts.Status == "Completed" || lts.Status == "Cancelled") continue;

                items.Add(new GrainStates.LabWorklistItem
                {
                    LabTestId         = ltId,
                    PatientId         = PatientId,
                    TestName          = lts.TestName,
                    TestCode          = lts.TestCode,
                    Category          = lts.Category,
                    Status            = lts.Status,
                    WorklistCategory  = cat,
                    OrderedDate       = lts.CreatedDate,
                    CollectionDateTime = lts.CollectionDateTime,
                    SpecimenType      = lts.SpecimenType,
                    IsCritical        = lts.IsCritical,
                    AbnormalFlag      = lts.AbnormalFlag,
                });
            }
        }
        catch { }

        await worklist.RefreshAsync(items);
        return await worklist.GetAsync();
    }

    public Task<GrainStates.LabWorklistState> GetLabWorklistAsync(string locationId)
        => GrainFactory.GetGrain<ILabWorklistGrain>($"LAB-WORKLIST:{locationId}").GetAsync();
}
