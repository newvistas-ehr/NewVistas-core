// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Lifecycle status of a physical therapy referral.
/// </summary>
[GenerateSerializer]
public enum PTReferralStatus
{
    Active,
    Completed,
    Expired,
    Cancelled
}
