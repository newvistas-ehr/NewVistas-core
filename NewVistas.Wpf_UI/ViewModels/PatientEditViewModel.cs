// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class PatientEditViewModel : BasePatientViewModel
{
    [ObservableProperty] private PatientState? _patient;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private int _selectedTabIndex;

    // Demographics
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editSex = "M";
    [ObservableProperty] private DateTime? _editDob;
    [ObservableProperty] private string _editSsn = string.Empty;
    [ObservableProperty] private string _editMaritalStatus = string.Empty;

    // Address
    [ObservableProperty] private string _editAddress1 = string.Empty;
    [ObservableProperty] private string _editAddress2 = string.Empty;
    [ObservableProperty] private string _editAddress3 = string.Empty;
    [ObservableProperty] private string _editCity = string.Empty;
    [ObservableProperty] private string _editState = string.Empty;
    [ObservableProperty] private string _editZip = string.Empty;

    // Contact
    [ObservableProperty] private string _editPhoneRes = string.Empty;
    [ObservableProperty] private string _editPhoneWork = string.Empty;
    [ObservableProperty] private string _editEmail = string.Empty;

    // Emergency Contact
    [ObservableProperty] private string _editEmergencyName = string.Empty;
    [ObservableProperty] private string _editEmergencyRelationship = string.Empty;
    [ObservableProperty] private string _editEmergencyPhone = string.Empty;

    // Veteran
    [ObservableProperty] private string _editVeteran = string.Empty;
    [ObservableProperty] private int? _editScPercent;
    [ObservableProperty] private string _editEligibility = string.Empty;
    [ObservableProperty] private string _editPrimaryEligibility = string.Empty;

    // Military
    [ObservableProperty] private DateTime? _editServiceEntry;
    [ObservableProperty] private DateTime? _editServiceSeparation;
    [ObservableProperty] private string _editBranch = string.Empty;
    [ObservableProperty] private string _editDischargeType = string.Empty;
    [ObservableProperty] private string _editPow = string.Empty;

    public string[] SexOptions { get; } = ["M", "F"];
    public string[] MaritalStatusOptions { get; } = ["", "SINGLE", "MARRIED", "DIVORCED", "WIDOWED", "SEPARATED"];
    public string[] YesNoOptions { get; } = ["", "Y", "N"];
    public string[] BranchOptions { get; } = ["", "ARMY", "NAVY", "AIR FORCE", "MARINES", "COAST GUARD", "SPACE FORCE"];
    public string[] DischargeOptions { get; } = ["", "HONORABLE", "GENERAL", "OTHER THAN HONORABLE", "BAD CONDUCT", "DISHONORABLE"];

    public PatientEditViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Patient = await workflow.GetPatientAsync();
        PopulateFields();
    }

    private void PopulateFields()
    {
        if (Patient is null) return;

        EditName = Patient.Name;
        EditSex = Patient.Sex;
        EditDob = Patient.DateOfBirth;
        EditSsn = Patient.SocialSecurityNumber;
        EditMaritalStatus = Patient.MaritalStatus ?? string.Empty;

        EditAddress1 = Patient.StreetAddress1 ?? string.Empty;
        EditAddress2 = Patient.StreetAddress2 ?? string.Empty;
        EditAddress3 = Patient.StreetAddress3 ?? string.Empty;
        EditCity = Patient.City ?? string.Empty;
        EditState = Patient.State ?? string.Empty;
        EditZip = Patient.ZipCode ?? string.Empty;

        EditPhoneRes = Patient.PhoneNumberResidence ?? string.Empty;
        EditPhoneWork = Patient.PhoneNumberWork ?? string.Empty;
        EditEmail = Patient.Email ?? string.Empty;

        EditEmergencyName = Patient.EmergencyContactName ?? string.Empty;
        EditEmergencyRelationship = Patient.EmergencyContactRelationship ?? string.Empty;
        EditEmergencyPhone = Patient.EmergencyContactPhone ?? string.Empty;

        EditVeteran = Patient.Veteran ?? string.Empty;
        EditScPercent = Patient.ServiceConnectedPercentage;
        EditEligibility = Patient.EligibilityCode ?? string.Empty;
        EditPrimaryEligibility = Patient.PrimaryEligibilityCode ?? string.Empty;

        EditServiceEntry = Patient.ServiceEntryDate;
        EditServiceSeparation = Patient.ServiceSeparationDate;
        EditBranch = Patient.ServiceBranch ?? string.Empty;
        EditDischargeType = Patient.ServiceDischargeType ?? string.Empty;
        EditPow = Patient.PrisonerOfWar ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveDemographics()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateDemographicsAsync(
                EditName, EditSex, EditDob,
                string.IsNullOrWhiteSpace(EditSsn) ? null : EditSsn);
            await workflow.UpdateMaritalStatusAsync(
                string.IsNullOrWhiteSpace(EditMaritalStatus) ? null : EditMaritalStatus);
            SuccessMessage = "Demographics saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveAddress()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateAddressAsync(
                NullIfEmpty(EditAddress1), NullIfEmpty(EditAddress2), NullIfEmpty(EditAddress3),
                NullIfEmpty(EditCity), NullIfEmpty(EditState), NullIfEmpty(EditZip));
            SuccessMessage = "Address saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveContact()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateContactInfoAsync(
                NullIfEmpty(EditPhoneRes), NullIfEmpty(EditPhoneWork), NullIfEmpty(EditEmail));
            SuccessMessage = "Contact info saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveEmergencyContact()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateEmergencyContactAsync(
                NullIfEmpty(EditEmergencyName), NullIfEmpty(EditEmergencyRelationship),
                NullIfEmpty(EditEmergencyPhone));
            SuccessMessage = "Emergency contact saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveVeteranInfo()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateVeteranInfoAsync(
                EditVeteran, EditScPercent,
                NullIfEmpty(EditEligibility), NullIfEmpty(EditPrimaryEligibility));
            SuccessMessage = "Veteran info saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveMilitaryService()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null; SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.UpdateMilitaryServiceAsync(
                EditServiceEntry, EditServiceSeparation,
                NullIfEmpty(EditBranch), NullIfEmpty(EditDischargeType), NullIfEmpty(EditPow));
            SuccessMessage = "Military service saved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
