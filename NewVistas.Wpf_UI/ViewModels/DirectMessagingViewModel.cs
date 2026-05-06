// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class DirectMessagingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<DirectAddressSummary> _addresses = new();
    [ObservableProperty] private ObservableCollection<DirectMessageSummary> _messages = new();
    [ObservableProperty] private string? _actionMessage;

    // Register fields
    [ObservableProperty] private string _newAddress = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private string _newOwnerType = "provider";

    public DirectMessagingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var addrIndex = Grains.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
        List<DirectAddressSummary> addrs = await addrIndex.GetAllAddressesAsync();
        Addresses.Clear();
        foreach (var a in addrs) Addresses.Add(a);
    }

    [RelayCommand]
    private async Task LoadMessages()
    {
        IsLoading = true; Error = null;
        try
        {
            var msgIndex = Grains.GetGrain<IDirectMessageIndexGrain>("DIRECT-MSG-INDEX");
            List<DirectMessageSummary> msgs = await msgIndex.GetAllMessagesAsync();
            Messages.Clear();
            foreach (var m in msgs) Messages.Add(m);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RegisterAddress()
    {
        if (string.IsNullOrWhiteSpace(NewAddress) || string.IsNullOrWhiteSpace(NewDisplayName)) return;
        IsLoading = true; Error = null;
        try
        {
            var addrIndex = Grains.GetGrain<IDirectAddressIndexGrain>("DIRECT-ADDR-INDEX");
            await addrIndex.AddAddressAsync(new DirectAddressSummary
            {
                DirectAddress = NewAddress.Trim(),
                DisplayName = NewDisplayName.Trim(),
                OwnerType = NewOwnerType,
                IsActive = true
            });
            ActionMessage = $"Address {NewAddress} registered.";
            NewAddress = string.Empty; NewDisplayName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
