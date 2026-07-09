// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// VistA notification type constants — mirrors ORB NOTIFICATION file (#8992.1)
/// and uConst.pas NF_* constants from CPRS.
/// </summary>
public static class NotificationType
{
    public const int LongTextAlert               = -101;
    /// <summary>Emerging-condition cluster threshold reached (proto-condition surveillance).</summary>
    public const int EmergingClusterThreshold    = -102;
    /// <summary>An emerging-condition proto was promoted to a coded condition.</summary>
    public const int EmergingConditionPromoted   = -103;
    public const int LabResults                  = 3;
    public const int FlaggedOrders               = 6;
    public const int FlaggedOrdersComments       = 8;
    public const int OrderRequiresElecSignature  = 12;
    public const int AbnormalLabResults          = 14;
    public const int ImagingResults              = 22;
    public const int ConsultRequestResolution    = 23;
    public const int AbnormalImagingResults      = 25;
    public const int ImagingRequestCancelHeld    = 26;
    public const int NewServiceConsultRequest    = 27;
    public const int ConsultRequestCancelHold    = 30;
    public const int SiteFlaggedResults          = 32;
    public const int OrdererFlaggedResults       = 33;
    public const int OrderRequiresCosignature    = 37;
    public const int LabOrderCanceled            = 42;
    public const int StatResults                 = 44;
    public const int DnrExpiring                 = 45;
    public const int MedicationsExpiringInpt     = 47;
    public const int UnverifiedMedicationOrder   = 48;
    public const int NewOrder                    = 50;
    public const int ImagingResultsAmended       = 53;
    public const int CriticalLabResults          = 57;
    public const int UnverifiedOrder             = 59;
    public const int FlaggedOiResults            = 60;
    public const int FlaggedOiOrder              = 61;
    public const int DcOrder                     = 62;
    public const int ConsultRequestUpdated       = 63;
    public const int FlaggedOiExpInpt            = 64;
    public const int FlaggedOiExpOutpt           = 65;
    public const int ConsultProcInterpretation   = 66;
    public const int ImagingRequestChanged       = 67;
    public const int LabThresholdExceeded        = 68;
    public const int MammogramResults            = 69;
    public const int PapSmearResults             = 70;
    public const int AnatomicPathologyResults    = 71;
    public const int MedicationsExpiringOutpt    = 72;
    public const int RxRenewalRequest            = 73;
    public const int DeaAutoDcCsMedOrder         = 74;
    public const int DeaCertRevoked              = 75;
    public const int LapsedOrder                 = 78;
    public const int HiRiskOrder                 = 79;
    public const int NewAllergyConflictOrder     = 88;
    public const int ProstheticsRequestUpdated   = 89;
    public const int RtcCancelOrders             = 91;
    public const int NoFlagActionOrder           = 98;
    public const int DcSummUnsignedNote          = 901;
    public const int ConsultUnsignedNote         = 902;
    public const int NotesUnsignedNote           = 903;
    public const int SurgeryUnsignedNote         = 904;
}

/// <summary>
/// Forward type codes for alert forwarding — mirrors VistA ORQALERT forwarding options.
/// </summary>
public static class AlertForwardType
{
    public const string ForwardOnly    = "FW";
    public const string SendAndKeep    = "SK";
    public const string Redirect       = "RD";
}

/// <summary>
/// One forward-history entry recorded when an alert is forwarded.
/// </summary>
[GenerateSerializer]
public class AlertForwardRecord
{
    [Id(0)]
    public string ToRecipientId { get; set; } = string.Empty;

    [Id(1)]
    public string ToRecipientName { get; set; } = string.Empty;

    /// <summary>FW, SK, or RD</summary>
    [Id(2)]
    public string ForwardType { get; set; } = string.Empty;

    [Id(3)]
    public string? Comment { get; set; }

    [Id(4)]
    public string ForwardedByUserId { get; set; } = string.Empty;

    [Id(5)]
    public DateTime ForwardedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the state of a Notification grain — VistA ALERT / ORB NOTIFICATION.
/// Grain key is the XQAID (alert ID).
/// </summary>
[GenerateSerializer]
public class NotificationState
{
    /// <summary>Alert ID (XQAID) — grain key</summary>
    [Id(0)]
    public string AlertId { get; set; } = string.Empty;

    /// <summary>Patient DFN associated with this alert</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Notification type — one of the NotificationType constants.
    /// Maps to ORB NOTIFICATION file (#8992.1).
    /// </summary>
    [Id(2)]
    public int NotificationType { get; set; }

    /// <summary>Human-readable notification type text</summary>
    [Id(3)]
    public string NotificationTypeText { get; set; } = string.Empty;

    /// <summary>Recipient user ID (DUZ)</summary>
    [Id(4)]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>Recipient user name</summary>
    [Id(5)]
    public string RecipientName { get; set; } = string.Empty;

    /// <summary>
    /// Package that generated the alert (e.g., LAB, OR, RADIOLOGY, TIU, GMRC)
    /// </summary>
    [Id(6)]
    public string? SendingPackage { get; set; }

    /// <summary>Alert message text displayed to the user</summary>
    [Id(7)]
    public string? MessageText { get; set; }

    /// <summary>
    /// Follow-up action text (what happens when user acts on alert)
    /// </summary>
    [Id(8)]
    public string? FollowUpAction { get; set; }

    /// <summary>
    /// Extended follow-up text (SMART ALERT info block)
    /// </summary>
    [Id(9)]
    public string? FollowUpText { get; set; }

    /// <summary>
    /// True if this alert is flagged as critical (requires immediate attention)
    /// </summary>
    [Id(10)]
    public bool IsCritical { get; set; }

    /// <summary>
    /// XQADATA — ancillary data passed with the alert (e.g., order IEN, lab result IEN)
    /// </summary>
    [Id(11)]
    public string? XqaData { get; set; }

    /// <summary>
    /// Alert status: ACTIVE, PROCESSED, DELETED, FORWARDED
    /// </summary>
    [Id(12)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>Date/time the alert was generated</summary>
    [Id(13)]
    public DateTime AlertDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>Date/time the alert was processed/read</summary>
    [Id(14)]
    public DateTime? ProcessedDateTime { get; set; }

    /// <summary>User who processed the alert</summary>
    [Id(15)]
    public string? ProcessedByUserId { get; set; }

    /// <summary>Date/time the alert was deleted</summary>
    [Id(16)]
    public DateTime? DeletedDateTime { get; set; }

    /// <summary>User who deleted the alert</summary>
    [Id(17)]
    public string? DeletedByUserId { get; set; }

    /// <summary>Forward history — populated when alert is forwarded</summary>
    [Id(18)]
    public List<AlertForwardRecord> ForwardHistory { get; set; } = new();

    /// <summary>Follow-up comments added by users</summary>
    [Id(19)]
    public List<string> FollowUpComments { get; set; } = new();

    /// <summary>
    /// True if this is a SMART ALERT (contains extended structured info block)
    /// </summary>
    [Id(20)]
    public bool IsSmartAlert { get; set; }

    [Id(21)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
