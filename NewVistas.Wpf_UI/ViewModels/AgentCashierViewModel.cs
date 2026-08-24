// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class AgentCashierViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Cashier Window tab — patient receipts
    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private ObservableCollection<CashierReceiptIndexEntry> _patientReceipts = new();
    [ObservableProperty] private bool _patientLoaded;

    // Issue receipt form
    [ObservableProperty] private bool _showReceiptForm;
    [ObservableProperty] private string _receiptNumber = string.Empty;
    [ObservableProperty] private string _receiptPatientId = string.Empty;
    [ObservableProperty] private string _receiptPatientName = string.Empty;
    [ObservableProperty] private string _receiptArAccountId = string.Empty;
    [ObservableProperty] private decimal _receiptAmount;
    [ObservableProperty] private CashierPaymentMethod _receiptPaymentMethod = CashierPaymentMethod.Cash;
    [ObservableProperty] private string _receiptCashierId = string.Empty;
    [ObservableProperty] private string _receiptCashierName = string.Empty;
    [ObservableProperty] private string _receiptSessionId = string.Empty;

    // Sessions tab
    [ObservableProperty] private ObservableCollection<CashierSessionIndexEntry> _openSessions = new();
    [ObservableProperty] private ObservableCollection<CashierSessionIndexEntry> _allSessions = new();

    // New session form
    [ObservableProperty] private bool _showSessionForm;
    [ObservableProperty] private string _stationId = string.Empty;
    [ObservableProperty] private string _stationName = string.Empty;
    [ObservableProperty] private string _cashierId = string.Empty;
    [ObservableProperty] private string _cashierName = string.Empty;
    [ObservableProperty] private decimal _openingBalance;

    public CashierPaymentMethod[] PaymentMethods { get; } =
    [
        CashierPaymentMethod.Cash, CashierPaymentMethod.Check,
        CashierPaymentMethod.MoneyOrder, CashierPaymentMethod.CreditCard,
        CashierPaymentMethod.WireTransfer
    ];

    public AgentCashierViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    partial void OnSelectedTabChanged(int value)
    {
        if (value == 1)
            _ = LoadSessionsAsync();
    }

    [RelayCommand]
    private async Task LoadPatientReceipts()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var indexGrain = _grains.GetGrain<ICashierReceiptIndexGrain>($"CASHIER-RECEIPT-IDX:{pid}");
            List<CashierReceiptIndexEntry> receipts = await indexGrain.GetAllAsync();
            PatientReceipts.Clear();
            foreach (CashierReceiptIndexEntry r in receipts) PatientReceipts.Add(r);
            PatientLoaded = true;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleReceiptForm() => ShowReceiptForm = !ShowReceiptForm;

    [RelayCommand]
    private async Task IssueReceipt()
    {
        if (string.IsNullOrWhiteSpace(ReceiptNumber) || string.IsNullOrWhiteSpace(ReceiptSessionId)) return;
        IsLoading = true; Error = null;
        try
        {
            string receiptId = $"CASHIER-RECEIPT:{Guid.NewGuid()}";
            var receiptGrain = _grains.GetGrain<ICashierReceiptGrain>(receiptId);
            await receiptGrain.IssueAsync(
                ReceiptNumber, ReceiptPatientId, ReceiptPatientName, ReceiptArAccountId,
                ReceiptAmount, ReceiptPaymentMethod,
                ReceiptCashierId, ReceiptCashierName, ReceiptSessionId, null, null);
            ShowReceiptForm = false;
            ReceiptNumber = ReceiptPatientId = ReceiptPatientName = ReceiptArAccountId =
                ReceiptCashierId = ReceiptCashierName = ReceiptSessionId = string.Empty;
            ReceiptAmount = 0;
            if (PatientLoaded) await LoadPatientReceipts();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    private async Task LoadSessionsAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var sessionIndex = _grains.GetGrain<ICashierSessionIndexGrain>("CASHIER-SESSION-IDX");
            List<CashierSessionIndexEntry> open = await sessionIndex.GetOpenSessionsAsync();
            List<CashierSessionIndexEntry> all = await sessionIndex.GetAllAsync();
            OpenSessions.Clear();
            foreach (CashierSessionIndexEntry s in open) OpenSessions.Add(s);
            AllSessions.Clear();
            foreach (CashierSessionIndexEntry s in all) AllSessions.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleSessionForm() => ShowSessionForm = !ShowSessionForm;

    [RelayCommand]
    private async Task OpenSession()
    {
        if (string.IsNullOrWhiteSpace(StationId) || string.IsNullOrWhiteSpace(CashierId)) return;
        IsLoading = true; Error = null;
        try
        {
            string sessionId = $"CASHIER-SESSION:{Guid.NewGuid()}";
            var sessionGrain = _grains.GetGrain<ICashierSessionGrain>(sessionId);
            await sessionGrain.OpenAsync(StationId, StationName, CashierId, CashierName, DateTime.Today, OpeningBalance);
            ShowSessionForm = false;
            StationId = StationName = CashierId = CashierName = string.Empty;
            OpeningBalance = 0;
            await LoadSessionsAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
