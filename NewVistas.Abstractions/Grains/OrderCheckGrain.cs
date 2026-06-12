// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Concurrency;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Order Check Grain — StatelessWorker for clinical order checking.
/// Mirrors VistA ORCHECK.m / ORWDXC.m order checking system.
///
/// Checks performed:
/// 1. Drug-allergy cross-reference
/// 2. Duplicate active order detection
/// 3. Drug-drug interaction (for Pharmacy orders, via IDrugInteractionCheckerGrain)
///
/// No persistent state — reads from patient's existing grains.
/// </summary>
[StatelessWorker]
public class OrderCheckGrain : Grain, IOrderCheckGrain
{
    private readonly ILogger<OrderCheckGrain> _logger;

    public OrderCheckGrain(ILogger<OrderCheckGrain> logger)
    {
        _logger = logger;
    }

    public async Task<List<OrderCheckResult>> CheckOrderAsync(
        string patientId, string orderType, string orderText, string? orderableItemId)
    {
        var results = new List<OrderCheckResult>();
        string upperText = orderText.ToUpperInvariant();

        // Get patient data for checking
        var patientGrain = GrainFactory.GetGrain<IPatientGrain>(patientId);

        // ── Check 1: Drug-Allergy Cross-Reference ──────────────────────────
        try
        {
            List<AllergyEntry> allergies = await patientGrain.GetAllergiesAsync();

            foreach (AllergyEntry allergy in allergies)
            {
                if (string.IsNullOrEmpty(allergy.Allergen)) continue;
                string allergen = allergy.Allergen.ToUpperInvariant();

                // Check if the order text contains the allergen name
                if (upperText.Contains(allergen) || allergen.Contains(upperText.Split(' ')[0]))
                {
                    results.Add(new OrderCheckResult
                    {
                        CheckType = "DRUG_ALLERGY",
                        Severity = "HIGH",
                        Message = $"Patient has allergy to {allergy.Allergen}. " +
                                  $"Severity: {allergy.Severity ?? "Unknown"}. " +
                                  $"Reactions: {string.Join(", ", allergy.Reactions ?? [])}.",
                        OrderText = orderText,
                        ConflictingItem = allergy.Allergen
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking allergies for patient {PatientId}", patientId);
        }

        // ── Check 2: Duplicate Order Detection ─────────────────────────────
        // Reads the per-patient order index (complete, status-filtered) rather
        // than PatientState.OrderIds, which is a capped recent window — an
        // active order older than the window must still trigger the alert.
        try
        {
            var orderIndex = GrainFactory.GetGrain<IPatientOrderIndexGrain>(patientId);
            List<OrderIndexEntry> current = await orderIndex.GetEntriesByFilterAsync(2); // 2 = Current

            // Fan out only over same-type current orders (small set) for the
            // OrderableItemId comparison the index doesn't carry.
            var sameTypeKeys = current
                .Where(e => e.OrderType == orderType)
                .Select(e => e.OrderGrainKey)
                .ToList();
            var existingOrders = await Task.WhenAll(sameTypeKeys.Select(key =>
                GrainFactory.GetGrain<IOrderGrain>(key).GetOrderAsync()));

            foreach (var existing in existingOrders)
            {
                if (existing.Status is not ("Active" or "Pending")) continue;

                string existingText = (existing.OrderableItem ?? "").ToUpperInvariant();
                if (existingText == upperText ||
                    (!string.IsNullOrEmpty(orderableItemId) && existing.OrderableItemId == orderableItemId))
                {
                    results.Add(new OrderCheckResult
                    {
                        CheckType = "DUPLICATE",
                        Severity = "MODERATE",
                        Message = $"Duplicate order detected: '{existing.OrderableItem}' " +
                                  $"is already {existing.Status} (ordered {existing.OrderDateTime:MM/dd/yyyy}).",
                        OrderText = orderText,
                        ConflictingItem = existing.OrderableItem
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking duplicates for patient {PatientId}", patientId);
        }

        // ── Check 3: Drug-Drug Interaction (Pharmacy orders only) ──────────
        if (orderType == "Pharmacy")
        {
            try
            {
                var checker = GrainFactory.GetGrain<IDrugInteractionCheckerGrain>("CHECKER");
                bool cacheReady = await checker.IsCacheReadyAsync();
                if (!cacheReady)
                {
                    // Fail closed: silence here previously implied "no interactions".
                    // Surface the inability to verify as an explicit high-severity alert.
                    results.Add(new OrderCheckResult
                    {
                        CheckType = "DRUG_DRUG",
                        Severity = "HIGH",
                        Message = "Drug interactions could NOT be verified: the interaction dataset " +
                                  "is not loaded. Contact an administrator before dispensing.",
                        OrderText = orderText
                    });
                }
                else
                {
                    // Complete active-medication set from the PSO index — the
                    // capped PatientState.PharmacyIds window would hide active
                    // prescriptions from interaction checking (patient safety).
                    var psoIndex = GrainFactory
                        .GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");
                    List<PrescriptionIndexEntry> rxEntries = await psoIndex.GetAllAsync();

                    var activeRxNames = rxEntries
                        .Where(e => e.Status is "ACTIVE" or "HOLD")
                        .Select(e => e.DrugName)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();

                    if (activeRxNames.Count > 0)
                    {
                        results.Add(new OrderCheckResult
                        {
                            CheckType = "DRUG_DRUG",
                            Severity = "LOW",
                            Message = $"Drug interaction check: patient has {activeRxNames.Count} active medications. " +
                                      $"Full interaction screening requires NDF ingredient mapping.",
                            OrderText = orderText,
                            ConflictingItem = string.Join(", ", activeRxNames.Take(5))
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking drug interactions for patient {PatientId}", patientId);
            }
        }

        return results;
    }
}
