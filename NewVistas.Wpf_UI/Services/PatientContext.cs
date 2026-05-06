// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;

namespace NewVistas.Wpf_UI.Services;

/// <summary>
/// Shared patient context — holds the currently selected patient ID
/// and is shared across all ViewModels so changing the patient in one
/// view propagates to all others.
/// </summary>
public partial class PatientContext : ObservableObject
{
    [ObservableProperty]
    private string _patientId = string.Empty;

    /// <summary>
    /// One-shot navigation hint consumed by the target page.
    /// Set before navigating (e.g., "NEW_ORDER:PHARMACY") and cleared after the target reads it.
    /// Format: "ACTION" or "ACTION:DETAIL" (e.g., "NEW_ORDER:LAB", "NEW_ORDER:CONSULT").
    /// </summary>
    [ObservableProperty]
    private string? _pendingAction;

    /// <summary>Consume and clear the pending action (returns null if none).</summary>
    public string? TakePendingAction()
    {
        var action = PendingAction;
        PendingAction = null;
        return action;
    }

    /// <summary>
    /// Raised when a ViewModel wants to navigate to another page.
    /// MainViewModel subscribes to this and performs the actual navigation.
    /// </summary>
    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Request navigation to a sidebar page by its label (e.g., "Orders", "Labs").
    /// Optionally set a PendingAction that the target VM will consume.
    /// </summary>
    public void RequestNavigation(string targetLabel, string? action = null)
    {
        PendingAction = action;
        NavigationRequested?.Invoke(targetLabel);
    }
}
