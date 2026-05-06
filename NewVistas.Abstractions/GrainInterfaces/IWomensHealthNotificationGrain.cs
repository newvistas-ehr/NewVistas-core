// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Women's Health Notification Grain — VistA File #790 (WOMEN'S HEALTH).
/// Key: "WH-NOTE:{guid}"
///
/// Models individual clinical notifications for mammography, Pap smears,
/// contraception counseling, pregnancy, breast health, and menopause/HRT.
/// Mirrors WOCT.m, WOCPAT.m — core WH package routines.
/// </summary>
public interface IWomensHealthNotificationGrain : IGrainWithStringKey
{
    Task<GrainStates.WomensHealthNotificationState> GetAsync();

    Task CreateAsync(
        string patientId,
        GrainStates.WomensHealthNotificationType notificationType,
        DateTime procedureDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        GrainStates.MammographyResult? mammographyResult,
        int? biRadsScore,
        GrainStates.PapSmearResult? papSmearResult,
        string? contraceptiveMethod,
        int? gestationalAgeWeeks,
        DateTime? estimatedDueDate,
        string? pregnancyOutcome,
        bool followUpRequired,
        DateTime? nextDueDate,
        bool isRefusal,
        string? notes);

    /// <summary>Marks the notification as completed, optionally recording follow-up completion.</summary>
    Task CompleteAsync(DateTime? followUpCompletedDate, string? notes);

    /// <summary>Sets or clears the follow-up required flag and optionally updates the next due date.</summary>
    Task SetFollowUpRequiredAsync(bool required, DateTime? nextDueDate);

    Task CancelAsync();
}
