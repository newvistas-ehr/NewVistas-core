// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class LabEdiViewModelTests : ViewModelTestBase
{
    private LabEdiViewModel _vm = null!;
    private ILabEdiIndexGrain _mockIndex = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<ILabEdiIndexGrain>();
        MockGrainFactory.GetGrain<ILabEdiIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIndex);
        _vm = new LabEdiViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsReferenceLabs()
    {
        SelectPatient("PAT-001");
        var labs = new List<LabEdiLabSummary> { new() { ReferenceLabId = "L1" } };
        _mockIndex.GetReferenceLabsAsync().Returns(Task.FromResult(labs));
        MockWorkflowGrain.GetLabEdiOrdersAsync(50).Returns(Task.FromResult(new List<LabEdiOrderSummary>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.ReferenceLabs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_LoadsOrders()
    {
        SelectPatient("PAT-001");
        _mockIndex.GetReferenceLabsAsync().Returns(Task.FromResult(new List<LabEdiLabSummary>()));
        var orders = new List<LabEdiOrderSummary> { new() { OrderId = "O1" } };
        MockWorkflowGrain.GetLabEdiOrdersAsync(50).Returns(Task.FromResult(orders));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Orders, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadOrderDetail_LoadsFromGrain()
    {
        SelectPatient("PAT-001");
        var mockOrder = Substitute.For<ILabEdiOrderGrain>();
        var orderState = new LabEdiOrderState { OrderId = "O1" };
        mockOrder.GetOrderAsync().Returns(Task.FromResult(orderState));
        MockGrainFactory.GetGrain<ILabEdiOrderGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockOrder);

        _vm.SelectedOrder = new LabEdiOrderSummary { OrderId = "O1" };
        await _vm.LoadOrderDetailCommand.ExecuteAsync(null);

        Assert.That(_vm.OrderDetail, Is.Not.Null);
    }
}
