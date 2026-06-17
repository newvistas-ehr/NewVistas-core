// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Verifies the Orleans ACID guarantee on the Accounts Receivable money paths:
/// posting a financial transaction writes BOTH the <see cref="IARTransactionGrain"/>
/// record and the <see cref="IARAccountGrain"/> balance as one atomic unit. These
/// tests are the regression guard for the missing-transactions defect identified in
/// the robustness analysis (the AR account/transaction grains previously persisted
/// the two writes independently, so a crash between them could desynchronise the
/// ledger and the balance).
/// </summary>
[TestFixture]
public class AccountsReceivableTransactionAtomicityTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IARAccountGrain NewAccount()
        => _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{Guid.NewGuid()}");

    private IARTransactionGrain Transaction(string txnId)
        => _cluster.GrainFactory.GetGrain<IARTransactionGrain>($"AR-TXN:{txnId}");

    // ─── Commit consistency ──────────────────────────────────────────────

    [Test]
    public async Task PostPayment_PersistsTransactionRecordAndBalance_Together()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-ATOM-1", null, ARAccountCategory.CopayOutpatient, 100m, null);

        string txnId = await acct.PostPaymentAsync(40m, "Cash", "USR-1", "Cashier One", "RCPT-1", null, null);

        // Account side committed.
        ARAccountState account = await acct.GetAsync();
        Assert.That(account.CurrentBalance, Is.EqualTo(60m));
        Assert.That(account.AmountPaid, Is.EqualTo(40m));
        Assert.That(account.TransactionIds, Does.Contain(txnId));

        // Transaction-record side committed in the same transaction — no orphan id.
        ARTransactionState txn = await Transaction(txnId).GetAsync();
        Assert.That(txn.Amount, Is.EqualTo(40m));
        Assert.That(txn.TransactionType, Is.EqualTo(ARTransactionType.Payment));
        Assert.That(txn.ARAccountId, Is.EqualTo(account.ARAccountId));
    }

    [Test]
    public async Task MoneyPath_AccountBalanceReconcilesToTransactionLedger()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-ATOM-2", null, ARAccountCategory.CopayOutpatient, 100m, null);

        await acct.PostPaymentAsync(30m, "Cash", "USR-1", "Cashier One", null, null, null);   // -30
        await acct.PostAdjustmentAsync(10m, "CREDIT", "USR-1", "Cashier One", null);            // -10
        await acct.AccrueInterestAsync(5m, "USR-1");                                            // +5

        ARAccountState account = await acct.GetAsync();

        // Every id the account records must resolve to a real, committed transaction
        // record (no dangling ids), and reconstructing the balance from those records
        // must equal the stored balance — the invariant the ACID transaction protects.
        decimal reconstructed = 100m;
        Assert.That(account.TransactionIds, Has.Count.EqualTo(3));
        foreach (string id in account.TransactionIds)
        {
            ARTransactionState txn = await Transaction(id).GetAsync();
            Assert.That(txn.TransactionId, Is.EqualTo($"AR-TXN:{id}"), "transaction record missing for recorded id");
            reconstructed += txn.TransactionType switch
            {
                ARTransactionType.Payment or ARTransactionType.Adjustment or ARTransactionType.Waiver
                    or ARTransactionType.WriteOff => -txn.Amount,
                _ => txn.Amount, // Interest / Penalty / AdminCost increase the balance
            };
        }

        Assert.That(reconstructed, Is.EqualTo(account.CurrentBalance));
        Assert.That(account.CurrentBalance, Is.EqualTo(65m));
    }

    // ─── Atomic rollback (the core guarantee) ────────────────────────────

    [Test]
    public async Task PostPayment_WhenTransactionAborts_RollsBackBalanceAndRecord()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-ATOM-3", null, ARAccountCategory.CopayOutpatient, 100m, null);

        ITransactionClient txnClient = _cluster.ServiceProvider.GetRequiredService<ITransactionClient>();

        // Post a payment inside an ambient transaction, then force an abort. The
        // payment's two writes (transaction record + balance) must BOTH roll back.
        Assert.That(async () =>
            await txnClient.RunTransaction(TransactionOption.Create, async () =>
            {
                await acct.PostPaymentAsync(40m, "Cash", "USR-1", "Cashier One", null, null, null);
                throw new InvalidOperationException("force rollback");
            }),
            Throws.Exception);

        // Nothing from the aborted payment survived.
        ARAccountState account = await acct.GetAsync();
        Assert.That(account.CurrentBalance, Is.EqualTo(100m), "balance must roll back on abort");
        Assert.That(account.AmountPaid, Is.EqualTo(0m), "AmountPaid must roll back on abort");
        Assert.That(account.TransactionIds, Is.Empty, "no transaction id may be recorded after abort");

        // A subsequent committed payment still works (the account is not poisoned).
        string goodTxn = await acct.PostPaymentAsync(25m, "Cash", "USR-1", "Cashier One", null, null, null);
        ARAccountState after = await acct.GetAsync();
        Assert.That(after.CurrentBalance, Is.EqualTo(75m));
        Assert.That(after.TransactionIds, Does.Contain(goodTxn));
    }
}
