// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single Accounts Receivable account representing one charge against a patient
/// (VistA File #430 ACCOUNTS RECEIVABLE).
/// Grain key: "AR-ACCOUNT:{arAccountId}"
/// Managed by RCDP*.m, RCSP*.m MUMPS routines.
/// </summary>
public interface IARAccountGrain : IGrainWithStringKey
{
    /// <summary>Returns the current AR account state.</summary>
    Task<ARAccountState> GetAsync();

    /// <summary>
    /// Establishes a new AR account with the given charge details.
    /// Sets status to Active, CurrentBalance to <paramref name="originalAmount"/>.
    /// </summary>
    Task CreateAsync(
        string patientId,
        string? billingActionId,
        ARAccountCategory arCategory,
        decimal originalAmount,
        DateTime? dueDate);

    /// <summary>
    /// Posts a payment to the account. Creates an ARTransactionGrain record, reduces
    /// CurrentBalance, increments AmountPaid, and auto-transitions to Paid if balance reaches 0.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> PostPaymentAsync(
        decimal amount,
        string paymentMethod,
        string appliedByUserId,
        string appliedByUserName,
        string? receiptNumber,
        string? checkNumber,
        string? notes);

    /// <summary>
    /// Posts a balance adjustment (credit or debit) to the account.
    /// Creates an ARTransactionGrain record, updates CurrentBalance.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> PostAdjustmentAsync(
        decimal amount,
        string adjustmentType,
        string appliedByUserId,
        string appliedByUserName,
        string? notes);

    /// <summary>
    /// Waives a portion or all of the outstanding balance.
    /// Creates an ARTransactionGrain record, updates AmountWaived, reduces CurrentBalance.
    /// Sets status to Waived if balance reaches 0.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> WaiveAsync(
        decimal waivedAmount,
        string waivedByUserId,
        string waivedByUserName,
        string reason);

    /// <summary>
    /// Writes off the outstanding balance as uncollectable.
    /// Creates an ARTransactionGrain record, sets status to WrittenOff.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> WriteOffAsync(
        decimal writeOffAmount,
        string writtenOffByUserId,
        string writtenOffByUserName,
        string reason);

    /// <summary>
    /// Accrues interest on the account.
    /// Creates an ARTransactionGrain record, increments InterestAccrued and CurrentBalance.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> AccrueInterestAsync(decimal interestAmount, string appliedByUserId);

    /// <summary>
    /// Accrues a penalty charge on the account.
    /// Creates an ARTransactionGrain record, increments PenaltyAccrued and CurrentBalance.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> AccruePenaltyAsync(decimal penaltyAmount, string appliedByUserId);

    /// <summary>
    /// Accrues an administrative cost charge on the account.
    /// Creates an ARTransactionGrain record, increments AdminCostAccrued and CurrentBalance.
    /// Returns the new transaction ID.
    /// </summary>
    Task<string> AccrueAdminCostAsync(decimal adminCostAmount, string appliedByUserId);

    /// <summary>
    /// Refers this account to the Treasury Offset Program.
    /// Sets IsTreasuryOffset = true, ARStatus = TreasuryOffset.
    /// </summary>
    Task ReferToTopAsync(string referralId, decimal referredAmount, string referredByUserId, string referredByUserName);

    /// <summary>
    /// Records an incoming Treasury offset payment against the account.
    /// Calls PostPaymentAsync internally with method "TOP".
    /// Auto-resolves status to Paid and clears IsTreasuryOffset if CurrentBalance reaches 0.
    /// </summary>
    Task RecordTopOffsetAsync(decimal offsetAmount, string processedByUserId, string processedByUserName);

    /// <summary>
    /// Withdraws the TOP referral for this account.
    /// Clears IsTreasuryOffset and resets ARStatus to Active.
    /// </summary>
    Task WithdrawTopReferralAsync();

    /// <summary>Explicitly sets the lifecycle status of the account.</summary>
    Task UpdateStatusAsync(ARAccountStatus status);
}
