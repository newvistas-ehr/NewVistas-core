// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class SurgeryViewModel : ChartTabViewModelBase
{
    public ObservableCollection<SurgeryDto> Surgeries { get; } = new();

    [ObservableProperty] private bool _showScheduleForm;
    [ObservableProperty] private string _newProcedure = string.Empty;
    [ObservableProperty] private string _newSurgeon = string.Empty;
    [ObservableProperty] private string _newAnesthesiaType = "GENERAL";

    public string[] AnesthesiaTypes { get; } = ["GENERAL", "SPINAL", "LOCAL", "MAC", "REGIONAL"];

    public SurgeryViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Api.GetSurgeriesAsync(PatientId);
        Surgeries.Clear();
        foreach (var s in items) Surgeries.Add(s);
    }

    protected override void ClearData() => Surgeries.Clear();

    [RelayCommand]
    private void ToggleScheduleForm() => ShowScheduleForm = !ShowScheduleForm;

    [RelayCommand]
    private async Task ScheduleSurgeryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProcedure)) return;
        ErrorText = string.Empty;
        try
        {
            await Api.ScheduleSurgeryAsync(PatientId, new
            {
                PrincipalProcedure = NewProcedure,
                PrincipalSurgeon = NewSurgeon,
                AnesthesiaType = NewAnesthesiaType
            });
            NewProcedure = string.Empty;
            NewSurgeon = string.Empty;
            ShowScheduleForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
