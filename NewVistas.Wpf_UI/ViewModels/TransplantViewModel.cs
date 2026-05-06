// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class TransplantViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<TransplantWaitlistEntry> _waitlist = new();
    [ObservableProperty] private ObservableCollection<TransplantDonorSummaryEntry> _donors = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public TransplantViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var waitlistIndex = _grains.GetGrain<ITransplantWaitlistIndexGrain>("TX-WAITLIST-IDX");
            List<TransplantWaitlistEntry> waitlist = await waitlistIndex.GetActiveWaitlistAsync();
            Waitlist.Clear();
            foreach (TransplantWaitlistEntry w in waitlist) Waitlist.Add(w);

            var donorIndex = _grains.GetGrain<ITransplantDonorIndexGrain>("TX-DONOR-IDX");
            List<TransplantDonorSummaryEntry> donors = await donorIndex.GetAvailableDonorsAsync();
            Donors.Clear();
            foreach (TransplantDonorSummaryEntry d in donors) Donors.Add(d);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
