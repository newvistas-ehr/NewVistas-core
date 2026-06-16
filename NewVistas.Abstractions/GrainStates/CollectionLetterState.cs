// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Letter type/phase in the collection dunning cycle.
/// Maps to VistA PRCA collection correspondence (PRCA*4.5*316).
/// </summary>
[GenerateSerializer]
public enum CollectionLetterType
{
    /// <summary>First notice — friendly reminder that payment is due.</summary>
    FirstNotice = 0,

    /// <summary>Second notice — overdue warning with interest/penalty notice.</summary>
    SecondNotice = 1,

    /// <summary>Third notice — final demand before collection referral.</summary>
    ThirdNotice = 2,

    /// <summary>Intent to collect — formal pre-referral notification.</summary>
    IntentToCollect = 3,

    /// <summary>Administrative review letter — copay exemption review notice.</summary>
    AdministrativeReview = 4,

    /// <summary>AR statement — periodic account statement.</summary>
    Statement = 5,

    /// <summary>Payment plan offer/confirmation letter.</summary>
    PaymentPlanOffer = 6,

    /// <summary>Final notice before Treasury Offset Program referral.</summary>
    TopPreReferral = 7,
}

/// <summary>
/// Status of a generated collection letter.
/// </summary>
[GenerateSerializer]
public enum CollectionLetterStatus
{
    /// <summary>Letter generated but not yet printed/sent.</summary>
    Generated = 0,

    /// <summary>Letter has been printed.</summary>
    Printed = 1,

    /// <summary>Letter has been mailed to the patient.</summary>
    Mailed = 2,

    /// <summary>Letter was returned as undeliverable.</summary>
    Returned = 3,

    /// <summary>Letter generation was cancelled before sending.</summary>
    Cancelled = 4,
}

/// <summary>
/// Individual line item on a collection letter (one per AR account).
/// </summary>
[GenerateSerializer]
public record CollectionLetterLineItem
{
    /// <summary>AR account ID this line references.</summary>
    [Id(0)] public string ARAccountId { get; init; } = string.Empty;

    /// <summary>Description of the charge (e.g., "Outpatient Copay — 2026-01-15").</summary>
    [Id(1)] public string Description { get; init; } = string.Empty;

    /// <summary>Original charge amount.</summary>
    [Id(2)] public decimal OriginalAmount { get; init; }

    /// <summary>Current outstanding balance on this account.</summary>
    [Id(3)] public decimal CurrentBalance { get; init; }

    /// <summary>Date the charge was established.</summary>
    [Id(4)] public DateTime DateEstablished { get; init; }

    /// <summary>Days past due.</summary>
    [Id(5)] public int DaysPastDue { get; init; }
}

/// <summary>
/// State for a single collection/dunning letter (VistA PRCA collection correspondence).
/// MUMPS: RCCLLT*.m, RCCL*.m
/// Grain key: "AR-LETTER:{guid}"
/// </summary>
[GenerateSerializer]
public class CollectionLetterState
{
    /// <summary>Unique letter identifier — grain key. (.001)</summary>
    [Id(0)] public string LetterId { get; set; } = string.Empty;

    /// <summary>Patient this letter is addressed to. (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Type/phase of the collection letter. (.02)</summary>
    [Id(2)] public CollectionLetterType LetterType { get; set; }

    /// <summary>Current status of the letter. (.03)</summary>
    [Id(3)] public CollectionLetterStatus Status { get; set; }

    /// <summary>Addressee name (denormalized from patient).</summary>
    [Id(4)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Mailing address.</summary>
    [Id(5)] public string? MailingAddress { get; set; }

    /// <summary>Date the letter was generated.</summary>
    [Id(6)] public DateTime GeneratedDate { get; set; }

    /// <summary>Date the letter was printed (null if not yet printed).</summary>
    [Id(7)] public DateTime? PrintedDate { get; set; }

    /// <summary>Date the letter was mailed (null if not yet mailed).</summary>
    [Id(8)] public DateTime? MailedDate { get; set; }

    /// <summary>Date the letter was returned undeliverable (null if not returned).</summary>
    [Id(9)] public DateTime? ReturnedDate { get; set; }

    /// <summary>Total amount owed across all line items.</summary>
    [Id(10)] public decimal TotalAmountDue { get; set; }

    /// <summary>Individual AR account line items included in this letter.</summary>
    [Id(11)] public List<CollectionLetterLineItem> LineItems { get; set; } = new();

    /// <summary>Due date for payment printed on the letter.</summary>
    [Id(12)] public DateTime? PaymentDueDate { get; set; }

    /// <summary>Number of days given to respond/pay.</summary>
    [Id(13)] public int ResponseDays { get; set; } = 30;

    /// <summary>Body text of the letter.</summary>
    [Id(14)] public string LetterBody { get; set; } = string.Empty;

    /// <summary>User who generated the letter.</summary>
    [Id(15)] public string? GeneratedByUserId { get; set; }

    /// <summary>Display name of user who generated the letter.</summary>
    [Id(16)] public string? GeneratedByUserName { get; set; }

    /// <summary>Sequence number in the dunning cycle for this patient (1st, 2nd, 3rd...).</summary>
    [Id(17)] public int DunningSequence { get; set; }

    /// <summary>Optional notes.</summary>
    [Id(18)] public string? Notes { get; set; }

    /// <summary>When this record was created.</summary>
    [Id(19)] public DateTime CreatedDate { get; set; }

    /// <summary>When this record was last modified.</summary>
    [Id(20)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight index entry for collection letters.
/// </summary>
[GenerateSerializer]
public record CollectionLetterIndexEntry
{
    /// <summary>Letter ID (grain key).</summary>
    [Id(0)] public string LetterId { get; init; } = string.Empty;

    /// <summary>Letter type/phase.</summary>
    [Id(1)] public CollectionLetterType LetterType { get; init; }

    /// <summary>Current letter status.</summary>
    [Id(2)] public CollectionLetterStatus Status { get; init; }

    /// <summary>Total amount due on the letter.</summary>
    [Id(3)] public decimal TotalAmountDue { get; init; }

    /// <summary>Date the letter was generated.</summary>
    [Id(4)] public DateTime GeneratedDate { get; init; }

    /// <summary>Dunning cycle sequence number.</summary>
    [Id(5)] public int DunningSequence { get; init; }
}

/// <summary>
/// Per-patient index of collection letters.
/// Grain key: "AR-LETTER-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class CollectionLetterIndexState
{
    /// <summary>Patient this index belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All collection letter entries, newest first.</summary>
    [Id(1)] public List<CollectionLetterIndexEntry> Entries { get; set; } = new();
}
