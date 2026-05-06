// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class IBSiteConfigViewModel : BasePatientViewModel
{
    [ObservableProperty] private IBSiteParametersState? _siteParams;
    [ObservableProperty] private string? _actionMessage;

    // Edit fields
    [ObservableProperty] private string _editSiteName = string.Empty;
    [ObservableProperty] private string _editAddress = string.Empty;
    [ObservableProperty] private decimal _editTier1;
    [ObservableProperty] private decimal _editTier2;
    [ObservableProperty] private decimal _editTier3;
    [ObservableProperty] private string _editUserId = string.Empty;

    public IBSiteConfigViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var grain = Grains.GetGrain<IIBSiteParametersGrain>("IB-SITE-PARAMS");
        SiteParams = await grain.GetAsync();
        if (SiteParams is not null)
        {
            EditSiteName = SiteParams.SiteName;
            EditAddress = SiteParams.BillingMailingAddress;
            EditTier1 = SiteParams.PharmacyCopayTier1Amount;
            EditTier2 = SiteParams.PharmacyCopayTier2Amount;
            EditTier3 = SiteParams.PharmacyCopayTier3Amount;
        }
    }

    [RelayCommand]
    private async Task SaveParameters()
    {
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IIBSiteParametersGrain>("IB-SITE-PARAMS");
            await grain.UpdateAsync(EditSiteName, EditAddress, null, null, null, null, EditTier1, EditTier2, EditTier3, null, null, false, EditUserId);
            ActionMessage = "Site parameters saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
