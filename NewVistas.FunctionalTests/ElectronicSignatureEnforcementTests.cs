// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.Runtime;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.FunctionalTests;

/// <summary>
/// The e-signature gate lives ON THE WORKFLOW GRAIN, so every client inherits it. Before
/// this, verification existed only in the Blazor UI: the REST endpoint signed on an empty
/// body, the terminal accepted any keystroke, and both WPF apps signed with hardcoded
/// placeholder strings. These tests pin the grain-level contract.
/// </summary>
[TestFixture]
public class ElectronicSignatureEnforcementTests
{
    private const string GoodCode = "myEsig123";
    private TestCluster _cluster = null!;
    private string _userId = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        _userId = $"ESIG-DOC-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<INewPersonGrain>($"USER:{_userId}")
            .SetElectronicSignatureHashAsync(ElectronicSignature.Hash(GoodCode));
    }

    [TearDown]
    public void ClearContext() => RequestContext.Clear();

    private IPatientWorkflowGrain Wf(string pid)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(pid);

    private void ActAs(string userId) => RequestContext.Set(RequestContextKeys.UserId, userId);

    private async Task<(string pid, string docId)> UnsignedNoteAsync()
    {
        string pid = $"ESIG-{Guid.NewGuid()}";
        string docId = await Wf(pid).CreateNoteAsync(
            "PROGRESS NOTE", null, "Note body.", "Esig test",
            "PROV-1", "Dr. A", null, null, null, null, null, DateTime.UtcNow);
        return (pid, docId);
    }

    [Test]
    public async Task CorrectCode_Signs()
    {
        (string pid, string docId) = await UnsignedNoteAsync();
        ActAs(_userId);

        await Wf(pid).SignNoteAsync(docId, GoodCode);

        TiuDocumentState note = await Wf(pid).GetNoteAsync(docId);
        Assert.That(note.Status, Is.Not.EqualTo("UNSIGNED"));
    }

    [Test]
    public async Task WrongCode_IsRefused_AndTheNoteStaysUnsigned()
    {
        (string pid, string docId) = await UnsignedNoteAsync();
        ActAs(_userId);

        Assert.That(async () => await Wf(pid).SignNoteAsync(docId, "not-the-code"),
            Throws.TypeOf<UnauthorizedAccessException>());
        Assert.That(async () => await Wf(pid).SignNoteAsync(docId, ""),
            Throws.TypeOf<UnauthorizedAccessException>(),
            "an empty code used to be a valid signature via the REST endpoint");

        TiuDocumentState note = await Wf(pid).GetNoteAsync(docId);
        Assert.That(note.Status, Is.EqualTo("UNSIGNED"));
    }

    [Test]
    public async Task NoAuthenticatedCaller_IsRefused()
    {
        (string pid, string docId) = await UnsignedNoteAsync();
        RequestContext.Clear();

        Assert.That(async () => await Wf(pid).SignNoteAsync(docId, GoodCode),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public async Task UserWithNoStoredHash_CannotSign()
    {
        (string pid, string docId) = await UnsignedNoteAsync();
        ActAs($"NOBODY-{Guid.NewGuid()}");

        Assert.That(async () => await Wf(pid).SignNoteAsync(docId, GoodCode),
            Throws.TypeOf<UnauthorizedAccessException>());
    }

    [Test]
    public async Task OrderSigning_VerifiesTheCode_AndNeverStoresIt()
    {
        string pid = $"ESIG-{Guid.NewGuid()}";
        string orderId = await Wf(pid).PlaceOrderAsync(
            "Lab", "CBC", null, "PROV-1", "Dr. A", null, null, "ROUTINE", null, null);
        ActAs(_userId);

        Assert.That(async () => await Wf(pid).SignOrderAsync(orderId, "wrong"),
            Throws.TypeOf<UnauthorizedAccessException>());

        await Wf(pid).SignOrderAsync(orderId, GoodCode);

        OrderState order = await _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId).GetOrderAsync();
        Assert.That(order.ElectronicSignature, Does.StartWith($"ES/{_userId}/"),
            "the stored value is an attestation of who signed and when");
        Assert.That(order.ElectronicSignature, Does.Not.Contain(GoodCode),
            "the signing secret must never be persisted");
    }

    [Test]
    public async Task SystemSigning_StillWorksForSeeds()
    {
        // Seeds run as a SYSTEM identity holding XUPROG; SharedCluster has no auth filter,
        // so what this pins is that the system path signs without any per-user code.
        (string pid, string docId) = await UnsignedNoteAsync();

        await Wf(pid).SignNoteAsSystemAsync(docId);

        TiuDocumentState note = await Wf(pid).GetNoteAsync(docId);
        Assert.That(note.Status, Is.Not.EqualTo("UNSIGNED"));
    }
}
