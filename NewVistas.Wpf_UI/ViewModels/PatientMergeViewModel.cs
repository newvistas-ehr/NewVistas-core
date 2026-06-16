// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Standalone admin tool for merging duplicate patient records.
/// Mirrors the Blazor PatientMerge page with a site feature guard.
/// </summary>
public partial class PatientMergeViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _successMessage;

    [ObservableProperty] private string _targetPatientId = string.Empty;
    [ObservableProperty] private string _sourcePatientId = string.Empty;
    [ObservableProperty] private string _reason = string.Empty;

    [ObservableProperty] private PatientState? _targetPreview;
    [ObservableProperty] private PatientState? _sourcePreview;

    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private bool _isConfirmed;

    [ObservableProperty] private MergeResultDto? _mergeResult;

    public PatientMergeViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
        CheckFeatureCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task CheckFeatureAsync()
    {
        try
        {
            var siteGrain = _grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
            IsFeatureEnabled = await siteGrain.IsFeatureEnabledAsync("PATIENT_MERGE");
        }
        catch
        {
            IsFeatureEnabled = false;
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetPatientId) || string.IsNullOrWhiteSpace(SourcePatientId))
        {
            Error = "Both Target and Source Patient IDs are required.";
            return;
        }

        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        MergeResult = null;
        HasPreview = false;
        IsConfirmed = false;

        try
        {
            var targetGrain = _grains.GetGrain<IPatientGrain>(TargetPatientId.Trim());
            var sourceGrain = _grains.GetGrain<IPatientGrain>(SourcePatientId.Trim());

            Task<PatientState> targetTask = targetGrain.GetPatientAsync();
            Task<PatientState> sourceTask = sourceGrain.GetPatientAsync();
            await Task.WhenAll(targetTask, sourceTask);

            TargetPreview = targetTask.Result;
            SourcePreview = sourceTask.Result;
            HasPreview = true;
        }
        catch (Exception ex)
        {
            Error = $"Preview failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteMergeAsync()
    {
        if (!IsConfirmed)
        {
            Error = "You must confirm the merge before executing.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetPatientId) || string.IsNullOrWhiteSpace(SourcePatientId))
        {
            Error = "Both Target and Source Patient IDs are required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Reason))
        {
            Error = "A reason for the merge is required.";
            return;
        }

        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        MergeResult = null;

        try
        {
            string mergeId = Guid.NewGuid().ToString();
            var mergeGrain = _grains.GetGrain<IPatientMergeGrain>($"MERGE:{mergeId}");
            var result = await mergeGrain.ExecuteMergeAsync(
                TargetPatientId.Trim(), SourcePatientId.Trim(), Reason.Trim(), "ADMIN", "Admin User");
            MergeResult = new MergeResultDto
            {
                Success = result.Success,
                MergeId = result.MergeId,
                ErrorMessage = result.ErrorMessage,
                ItemsMoved = result.ItemsMoved
            };
            if (result.Success)
            {
                SuccessMessage = $"Merge completed successfully. Merge ID: {result.MergeId}";
            }
            else
            {
                Error = result.ErrorMessage ?? "Merge failed with unknown error.";
            }
        }
        catch (Exception ex)
        {
            Error = $"Merge failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────

    public class MergeResultDto
    {
        public bool Success { get; set; }
        public string MergeId { get; set; } = "";
        public string? ErrorMessage { get; set; }
        public Dictionary<string, int> ItemsMoved { get; set; } = new();
    }
}
