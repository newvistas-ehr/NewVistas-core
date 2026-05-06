// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ImagingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ImagingSummary> _images = new();

    // Capture form
    [ObservableProperty] private bool _showCaptureForm;
    [ObservableProperty] private string _objectType = "PHOTOGRAPH";
    [ObservableProperty] private string _procedureDescription = string.Empty;
    [ObservableProperty] private string _specialtyIndex = "GENERAL";
    [ObservableProperty] private string _imageUrl = string.Empty;
    [ObservableProperty] private string _capturedByName = "Technician, Test";

    public string[] ObjectTypes { get; } = [
        "PHOTOGRAPH", "RADIOLOGY", "CARDIOLOGY", "DERMATOLOGY",
        "WOUND CARE", "OPHTHALMOLOGY", "PATHOLOGY", "OTHER"
    ];

    public ImagingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetImagesAsync(50);
        Images.Clear();
        foreach (var i in list) Images.Add(i);
    }

    [RelayCommand]
    private void ToggleCaptureForm() => ShowCaptureForm = !ShowCaptureForm;

    [RelayCommand]
    private async Task CaptureImage()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CaptureImageAsync(
                ObjectType,
                ProcedureDescription.Length > 0 ? ProcedureDescription : null,
                SpecialtyIndex,
                ImageUrl.Length > 0 ? ImageUrl : null,
                null, // thumbnailUrl
                null, null, // dicom
                DateTime.UtcNow, DateTime.UtcNow, 1, // procedureDate, captureDate, imageCount
                null, null, // radiologyId, tiuDocumentId
                null, CapturedByName, // capturedBy
                null, null, // location
                null); // comments
            ShowCaptureForm = false;
            ImageUrl = string.Empty;
            ProcedureDescription = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
