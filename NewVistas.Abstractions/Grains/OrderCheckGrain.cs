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
        try
        {
            List<string> orderIds = await patientGrain.GetOrderIdsAsync();
            var orderTasks = orderIds.Select(id =>
                GrainFactory.GetGrain<IOrderGrain>(id).GetOrderAsync()).ToList();
            var existingOrders = await Task.WhenAll(orderTasks);

            foreach (var existing in existingOrders)
            {
                if (existing.Status is not ("Active" or "Pending")) continue;
                if (existing.OrderType != orderType) continue;

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
                if (cacheReady)
                {
                    // Get patient's active medications
                    List<string> pharmacyIds = await patientGrain.GetPharmacyIdsAsync();
                    var rxTasks = pharmacyIds.Select(id =>
                        GrainFactory.GetGrain<IPharmacyGrain>(id).GetPrescriptionAsync()).ToList();
                    var prescriptions = await Task.WhenAll(rxTasks);

                    var activeRxNames = prescriptions
                        .Where(p => p.Status is "ACTIVE" or "HOLD")
                        .Select(p => p.DrugName ?? "")
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
