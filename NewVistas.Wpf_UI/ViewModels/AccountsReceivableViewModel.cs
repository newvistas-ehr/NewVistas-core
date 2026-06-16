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

public partial class AccountsReceivableViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private ARDebtorState? _debtor;
    [ObservableProperty] private ObservableCollection<ARAccountIndexEntry> _accounts = new();
    [ObservableProperty] private ARAccountIndexEntry? _selectedAccount;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _loaded;

    // Payment form
    [ObservableProperty] private bool _showPaymentForm;
    [ObservableProperty] private decimal _paymentAmount;
    [ObservableProperty] private string _paymentMethod = "Cash";
    [ObservableProperty] private string _appliedByUserId = string.Empty;
    [ObservableProperty] private string _appliedByUserName = string.Empty;

    public string[] PaymentMethods { get; } = ["Cash", "Check", "MoneyOrder", "CreditCard", "WireTransfer", "TOP"];

    public AccountsReceivableViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task Load()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null; Loaded = false;
        try
        {
            string pid = PatientId.Trim();
            var debtorGrain = _grains.GetGrain<IARDebtorGrain>($"AR-DEBTOR:{pid}");
            Debtor = await debtorGrain.GetAsync();

            var indexGrain = _grains.GetGrain<IARAccountIndexGrain>($"AR-ACCT-IDX:{pid}");
            List<ARAccountIndexEntry> accounts = await indexGrain.GetAllAsync();
            Accounts.Clear();
            foreach (ARAccountIndexEntry a in accounts) Accounts.Add(a);
            Loaded = true;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void TogglePaymentForm() => ShowPaymentForm = !ShowPaymentForm;

    [RelayCommand]
    private async Task PostPayment()
    {
        if (SelectedAccount is null || PaymentAmount <= 0) return;
        IsLoading = true; Error = null;
        try
        {
            var accountGrain = _grains.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{SelectedAccount.ARAccountId}");
            await accountGrain.PostPaymentAsync(
                PaymentAmount,
                PaymentMethod,
                AppliedByUserId.Length > 0 ? AppliedByUserId : "USER",
                AppliedByUserName.Length > 0 ? AppliedByUserName : "System User",
                null, null, null);
            ShowPaymentForm = false;
            PaymentAmount = 0;
            await Load();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
