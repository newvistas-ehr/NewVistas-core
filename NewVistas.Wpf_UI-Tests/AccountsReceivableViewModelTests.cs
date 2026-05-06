// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class AccountsReceivableViewModelTests : ViewModelTestBase
{
    private IARDebtorGrain _mockDebtor = null!;
    private IARAccountIndexGrain _mockAccountIndex = null!;
    private IARAccountGrain _mockAccount = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockDebtor = Substitute.For<IARDebtorGrain>();
        _mockAccountIndex = Substitute.For<IARAccountIndexGrain>();
        _mockAccount = Substitute.For<IARAccountGrain>();

        MockGrainFactory.GetGrain<IARDebtorGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockDebtor);
        MockGrainFactory.GetGrain<IARAccountIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockAccountIndex);
        MockGrainFactory.GetGrain<IARAccountGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockAccount);
    }

    [Test]
    public async Task Load_PopulatesDebtorAndAccounts()
    {
        var debtor = new ARDebtorState { PatientId = "P1" };
        _mockDebtor.GetAsync().Returns(debtor);
        _mockAccountIndex.GetAllAsync().Returns(new List<ARAccountIndexEntry>
        {
            new() { ARAccountId = "AR1" }
        });

        var vm = new AccountsReceivableViewModel(ApiClient, GrainService) { PatientId = "P1" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Debtor, Is.Not.Null);
        Assert.That(vm.Accounts, Has.Count.EqualTo(1));
        Assert.That(vm.Loaded, Is.True);
    }

    [Test]
    public async Task Load_EmptyPatientId_DoesNothing()
    {
        var vm = new AccountsReceivableViewModel(ApiClient, GrainService) { PatientId = "" };
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.That(vm.Loaded, Is.False);
        await _mockDebtor.DidNotReceive().GetAsync();
    }

    [Test]
    public async Task PostPayment_CallsGrain()
    {
        _mockAccount.PostPaymentAsync(Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns("TX-1");
        _mockDebtor.GetAsync().Returns(new ARDebtorState());
        _mockAccountIndex.GetAllAsync().Returns(new List<ARAccountIndexEntry>());

        var vm = new AccountsReceivableViewModel(ApiClient, GrainService) { PatientId = "P1" };
        vm.SelectedAccount = new ARAccountIndexEntry { ARAccountId = "AR1" };
        vm.PaymentAmount = 50m;
        vm.PaymentMethod = "Cash";
        await vm.PostPaymentCommand.ExecuteAsync(null);

        await _mockAccount.Received().PostPaymentAsync(50m, "Cash", "USER", "System User", null, null, null);
    }
}
