// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Pharmacy Point of Sale — RPMS ABSP (File #9002313).
/// Tests individual grains directly via TestCluster (not via the workflow grain).
/// </summary>
[TestFixture]
public class PharmacyPosTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPharmacyPosClaimGrain NewClaim()
        => _cluster.GrainFactory.GetGrain<IPharmacyPosClaimGrain>($"POS-CLAIM:{Guid.NewGuid()}");

    private IPharmacyPosClaimIndexGrain ClaimIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IPharmacyPosClaimIndexGrain>($"POS-CLAIM-IDX:{patientId}");

    private IPharmacyPosInsurerGrain NewInsurer()
        => _cluster.GrainFactory.GetGrain<IPharmacyPosInsurerGrain>($"POS-INSURER:{Guid.NewGuid()}");

    private IPharmacyPosInsurerIndexGrain InsurerIndex()
        => _cluster.GrainFactory.GetGrain<IPharmacyPosInsurerIndexGrain>("POS-INSURER-IDX");

    private static Task CreateB1ClaimAsync(IPharmacyPosClaimGrain grain, string patientId,
        string bin = "999999", string pcn = "PROC01", string ndc = "12345678901",
        decimal quantity = 30m, int daysSupply = 30,
        decimal ingredientCost = 150.00m, decimal dispensingFee = 2.50m, decimal uc = 175.00m)
        => grain.CreateAsync(
            patientId, "RX-001", NcpdpTransactionType.B1,
            bin, pcn, "D0",
            "GRP-100", "CARD-5678", "1",
            "INS-001", "TRICARE",
            ndc, "Lisinopril 10mg", quantity, daysSupply,
            DateTime.UtcNow.Date,
            ingredientCost, dispensingFee, uc,
            "NCPDP-PHARM-001", "Smith, RPh",
            "1234567890", "Jones, MD",
            null);

    // ─── ClaimGrain tests ────────────────────────────────────────────────────

    [Test]
    public async Task ClaimGrain_Create_PersistsAllFields()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();

        // Act
        await CreateB1ClaimAsync(grain, patientId);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.TransactionType, Is.EqualTo(NcpdpTransactionType.B1));
        Assert.That(state.Status, Is.EqualTo(PosClaimStatus.Pending));
        Assert.That(state.Bin, Is.EqualTo("999999"));
        Assert.That(state.Pcn, Is.EqualTo("PROC01"));
        Assert.That(state.Ndc, Is.EqualTo("12345678901"));
        Assert.That(state.QuantityDispensed, Is.EqualTo(30m));
        Assert.That(state.DaysSupply, Is.EqualTo(30));
        Assert.That(state.IngredientCostSubmitted, Is.EqualTo(150.00m));
        Assert.That(state.DispensingFeeSubmitted, Is.EqualTo(2.50m));
        Assert.That(state.UsualAndCustomary, Is.EqualTo(175.00m));
        Assert.That(state.DrugName, Is.EqualTo("Lisinopril 10mg"));
        Assert.That(state.PrescriptionId, Is.EqualTo("RX-001"));
        Assert.That(state.NcpdpVersion, Is.EqualTo("D0"));
        Assert.That(state.GroupNumber, Is.EqualTo("GRP-100"));
        Assert.That(state.CardholderId, Is.EqualTo("CARD-5678"));
        Assert.That(state.InsurerName, Is.EqualTo("TRICARE"));
    }

    [Test]
    public async Task ClaimGrain_Create_CalculatesGrossAmountDue()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();

        // Act
        await CreateB1ClaimAsync(grain, patientId, ingredientCost: 150.00m, dispensingFee: 2.50m);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert — grossAmountDue = ingredientCost + dispensingFee
        Assert.That(state.GrossAmountDue, Is.EqualTo(152.50m));
    }

    [Test]
    public async Task ClaimGrain_Adjudicate_Paid_SetsResponseFields()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();
        await CreateB1ClaimAsync(grain, patientId);

        // Act
        await grain.AdjudicateAsync(
            PosClaimStatus.Paid,
            insurancePaidAmount: 145.00m,
            patientResponsibility: 10.00m,
            copayAmount: 10.00m,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: "AUTH123",
            rejections: null,
            durMessages: null);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(PosClaimStatus.Paid));
        Assert.That(state.InsurancePaidAmount, Is.EqualTo(145.00m));
        Assert.That(state.PatientResponsibility, Is.EqualTo(10.00m));
        Assert.That(state.CopayAmount, Is.EqualTo(10.00m));
        Assert.That(state.AuthorizationNumber, Is.EqualTo("AUTH123"));
    }

    [Test]
    public async Task ClaimGrain_Adjudicate_Rejected_StoresRejections()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();
        await CreateB1ClaimAsync(grain, patientId);

        List<PosRejection> rejections = new()
        {
            new PosRejection { Code = "79", Description = "Refill Too Soon", Category = "B" },
            new PosRejection { Code = "76", Description = "Plan Limitations", Category = "B" }
        };

        // Act
        await grain.AdjudicateAsync(
            PosClaimStatus.Rejected,
            insurancePaidAmount: null,
            patientResponsibility: null,
            copayAmount: null,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: null,
            rejections: rejections,
            durMessages: null);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(PosClaimStatus.Rejected));
        Assert.That(state.Rejections, Has.Count.EqualTo(2));
        Assert.That(state.Rejections[0].Code, Is.EqualTo("79"));
        Assert.That(state.Rejections[0].Description, Is.EqualTo("Refill Too Soon"));
        Assert.That(state.Rejections[1].Code, Is.EqualTo("76"));
        Assert.That(state.Rejections[1].Description, Is.EqualTo("Plan Limitations"));
    }

    [Test]
    public async Task ClaimGrain_Adjudicate_WithDurMessages()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();
        await CreateB1ClaimAsync(grain, patientId);

        List<DurMessage> durMessages = new()
        {
            new DurMessage
            {
                ReasonCode = "DD",
                Level = DurConflictLevel.Critical,
                ConflictingDrugNdc = "98765432100",
                Message = "Drug-Drug interaction detected"
            }
        };

        // Act
        await grain.AdjudicateAsync(
            PosClaimStatus.Paid,
            insurancePaidAmount: 140.00m,
            patientResponsibility: 12.50m,
            copayAmount: 12.50m,
            coinsuranceAmount: null,
            deductibleAmount: null,
            authorizationNumber: "AUTH456",
            rejections: null,
            durMessages: durMessages);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.DurMessages, Has.Count.EqualTo(1));
        Assert.That(state.DurMessages[0].ReasonCode, Is.EqualTo("DD"));
        Assert.That(state.DurMessages[0].Level, Is.EqualTo(DurConflictLevel.Critical));
        Assert.That(state.DurMessages[0].ConflictingDrugNdc, Is.EqualTo("98765432100"));
    }

    [Test]
    public async Task ClaimGrain_Reverse_SetsStatusReversed()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();
        await CreateB1ClaimAsync(grain, patientId);
        await grain.AdjudicateAsync(PosClaimStatus.Paid, 145.00m, 10.00m, 10.00m, null, null, "AUTH789", null, null);

        // Act
        await grain.ReverseAsync();
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(PosClaimStatus.Reversed));
    }

    [Test]
    public async Task ClaimGrain_Cancel_SetsStatusCancelled()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();
        await CreateB1ClaimAsync(grain, patientId);

        // Act
        await grain.CancelAsync();
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.Status, Is.EqualTo(PosClaimStatus.Cancelled));
    }

    [Test]
    public async Task ClaimGrain_EligibilityCheck_E1Type()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimGrain grain = NewClaim();

        // Act — E1 eligibility check has no prescription
        await grain.CreateAsync(
            patientId, null, NcpdpTransactionType.E1,
            "610014", "PROC", "D0",
            "GRP-200", "CARD-9999", "1",
            "INS-002", "Medicaid",
            null, null, null, null,
            DateTime.UtcNow.Date,
            null, null, null,
            null, null, null, null,
            null);
        PharmacyPosClaimState state = await grain.GetAsync();

        // Assert
        Assert.That(state.TransactionType, Is.EqualTo(NcpdpTransactionType.E1));
        Assert.That(state.PrescriptionId, Is.Null);
        Assert.That(state.Ndc, Is.Null);
        Assert.That(state.DrugName, Is.Null);
    }

    // ─── ClaimIndex tests ────────────────────────────────────────────────────

    [Test]
    public async Task ClaimIndexGrain_AddAndGetAll_ReturnsNewestFirst()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimIndexGrain index = ClaimIndex(patientId);

        PosClaimIndexEntry entry1 = new()
        {
            ClaimId = $"POS-CLAIM:{Guid.NewGuid()}",
            PatientId = patientId,
            TransactionType = NcpdpTransactionType.B1,
            Status = PosClaimStatus.Pending,
            DrugName = "Metformin 500mg",
            DateOfService = DateTime.UtcNow.Date.AddDays(-2)
        };
        PosClaimIndexEntry entry2 = new()
        {
            ClaimId = $"POS-CLAIM:{Guid.NewGuid()}",
            PatientId = patientId,
            TransactionType = NcpdpTransactionType.B1,
            Status = PosClaimStatus.Paid,
            DrugName = "Lisinopril 10mg",
            DateOfService = DateTime.UtcNow.Date
        };

        // Act
        await index.AddEntryAsync(entry1);
        await index.AddEntryAsync(entry2);
        List<PosClaimIndexEntry> all = await index.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ClaimIndexGrain_GetByStatus_FiltersCorrectly()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimIndexGrain index = ClaimIndex(patientId);

        await index.AddEntryAsync(new PosClaimIndexEntry
        {
            ClaimId = $"POS-CLAIM:{Guid.NewGuid()}",
            PatientId = patientId,
            TransactionType = NcpdpTransactionType.B1,
            Status = PosClaimStatus.Paid,
            DrugName = "Atorvastatin 20mg"
        });
        await index.AddEntryAsync(new PosClaimIndexEntry
        {
            ClaimId = $"POS-CLAIM:{Guid.NewGuid()}",
            PatientId = patientId,
            TransactionType = NcpdpTransactionType.B1,
            Status = PosClaimStatus.Rejected,
            DrugName = "Amoxicillin 500mg"
        });

        // Act
        List<PosClaimIndexEntry> paidOnly = await index.GetByStatusAsync(PosClaimStatus.Paid);

        // Assert
        Assert.That(paidOnly, Has.Count.EqualTo(1));
        Assert.That(paidOnly[0].DrugName, Is.EqualTo("Atorvastatin 20mg"));
    }

    [Test]
    public async Task ClaimIndexGrain_UpdateEntryStatus_ChangesStatusAndAmounts()
    {
        // Arrange
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPharmacyPosClaimIndexGrain index = ClaimIndex(patientId);
        string claimId = $"POS-CLAIM:{Guid.NewGuid()}";

        await index.AddEntryAsync(new PosClaimIndexEntry
        {
            ClaimId = claimId,
            PatientId = patientId,
            TransactionType = NcpdpTransactionType.B1,
            Status = PosClaimStatus.Pending,
            DrugName = "Omeprazole 20mg"
        });

        // Act
        await index.UpdateEntryStatusAsync(claimId, PosClaimStatus.Paid, 130.00m, 15.00m);
        List<PosClaimIndexEntry> all = await index.GetAllAsync();

        // Assert
        PosClaimIndexEntry updated = all.First(e => e.ClaimId == claimId);
        Assert.That(updated.Status, Is.EqualTo(PosClaimStatus.Paid));
        Assert.That(updated.InsurancePaidAmount, Is.EqualTo(130.00m));
        Assert.That(updated.PatientResponsibility, Is.EqualTo(15.00m));
    }

    // ─── InsurerGrain tests ──────────────────────────────────────────────────

    [Test]
    public async Task InsurerGrain_Save_PersistsAllFields()
    {
        // Arrange
        IPharmacyPosInsurerGrain grain = NewInsurer();

        // Act
        await grain.SaveAsync(
            "Medicaid", "610014", "PROC", "D0",
            "NCPDP-001", "01", "Medicaid", "1-800-555-0001", true);
        PharmacyPosInsurerState state = await grain.GetAsync();

        // Assert
        Assert.That(state.InsurerName, Is.EqualTo("Medicaid"));
        Assert.That(state.Bin, Is.EqualTo("610014"));
        Assert.That(state.Pcn, Is.EqualTo("PROC"));
        Assert.That(state.NcpdpVersion, Is.EqualTo("D0"));
        Assert.That(state.PlanName, Is.EqualTo("Medicaid"));
        Assert.That(state.IsActive, Is.True);
        Assert.That(state.PharmacyNcpdpId, Is.EqualTo("NCPDP-001"));
        Assert.That(state.HelpDeskPhone, Is.EqualTo("1-800-555-0001"));
    }

    [Test]
    public async Task InsurerGrain_Deactivate_SetsInactive()
    {
        // Arrange
        IPharmacyPosInsurerGrain grain = NewInsurer();
        await grain.SaveAsync("Test Insurer", "999999", "TEST", "D0", null, null, null, null, true);

        // Act
        await grain.DeactivateAsync();
        PharmacyPosInsurerState state = await grain.GetAsync();

        // Assert
        Assert.That(state.IsActive, Is.False);
    }

    // ─── InsurerIndex tests ──────────────────────────────────────────────────

    [Test]
    public async Task InsurerIndexGrain_Upsert_AddsNew()
    {
        // Arrange — use a unique index grain key to isolate this test
        IPharmacyPosInsurerIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPharmacyPosInsurerIndexGrain>($"POS-INSURER-IDX-{Guid.NewGuid()}");
        string insurerId = $"INS-{Guid.NewGuid()}";

        // Act
        await index.UpsertAsync(new PosInsurerIndexEntry
        {
            InsurerId = insurerId,
            InsurerName = "BlueCross",
            Bin = "610014",
            Pcn = "PROC",
            IsActive = true
        });
        List<PosInsurerIndexEntry> all = await index.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InsurerName, Is.EqualTo("BlueCross"));
    }

    [Test]
    public async Task InsurerIndexGrain_Upsert_UpdatesExisting()
    {
        // Arrange
        IPharmacyPosInsurerIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPharmacyPosInsurerIndexGrain>($"POS-INSURER-IDX-{Guid.NewGuid()}");
        string insurerId = $"INS-{Guid.NewGuid()}";

        await index.UpsertAsync(new PosInsurerIndexEntry
        {
            InsurerId = insurerId,
            InsurerName = "OldName",
            Bin = "610014",
            Pcn = "PROC",
            IsActive = true
        });

        // Act — upsert same ID with new name
        await index.UpsertAsync(new PosInsurerIndexEntry
        {
            InsurerId = insurerId,
            InsurerName = "NewName",
            Bin = "610014",
            Pcn = "PROC",
            IsActive = true
        });
        List<PosInsurerIndexEntry> all = await index.GetAllAsync();

        // Assert
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].InsurerName, Is.EqualTo("NewName"));
    }

    [Test]
    public async Task InsurerIndexGrain_GetActive_FiltersCorrectly()
    {
        // Arrange
        IPharmacyPosInsurerIndexGrain index = _cluster.GrainFactory
            .GetGrain<IPharmacyPosInsurerIndexGrain>($"POS-INSURER-IDX-{Guid.NewGuid()}");

        await index.UpsertAsync(new PosInsurerIndexEntry
        {
            InsurerId = $"INS-{Guid.NewGuid()}",
            InsurerName = "Active Insurer",
            Bin = "610014",
            Pcn = "PROC",
            IsActive = true
        });
        await index.UpsertAsync(new PosInsurerIndexEntry
        {
            InsurerId = $"INS-{Guid.NewGuid()}",
            InsurerName = "Inactive Insurer",
            Bin = "999999",
            Pcn = "TEST",
            IsActive = false
        });

        // Act
        List<PosInsurerIndexEntry> active = await index.GetActiveAsync();

        // Assert
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].InsurerName, Is.EqualTo("Active Insurer"));
    }
}
