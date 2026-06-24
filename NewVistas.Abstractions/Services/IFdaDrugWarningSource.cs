// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for ingesting candidate drug safety warnings from external FDA/NLM sources.
/// This is a SEAM only: the default registration is <see cref="StaticFdaDrugWarningSource"/>,
/// which returns a small curated seed set and performs no network access.
///
/// A live implementation would pull from:
///   • openFDA drug label API (https://api.fda.gov/drug/label.json) — structured
///     warning fields (boxed_warning, warnings_and_cautions, spl_medguide) keyed by
///     openfda.pharm_class_epc / rxcui. Filter to product_type "HUMAN PRESCRIPTION DRUG"
///     so OTC "Drug Facts" labels don't shadow the Rx warning sections.
///   • DailyMed /spls (published_date_comparison=gt) — to detect newly issued labels.
///   • MedWatch RSS — net-new Drug Safety Communications (narrative; curated, not class-keyed).
///
/// A returned draft is a *candidate*: a human (pharmacy/clinical informatics) reviews
/// it, maps the FDA pharmacologic class to the local VA drug class code(s), and
/// promotes it to an active <see cref="DrugSafetyAdvisoryState"/>. Nothing reaches a
/// patient without that review and a provider's dispatch decision.
/// </summary>
public interface IFdaDrugWarningSource
{
    /// <summary>Whether a live FDA/NLM integration is configured. False for the static default.</summary>
    bool IsLiveSource { get; }

    /// <summary>Returns candidate warnings awaiting review and class mapping.</summary>
    Task<List<FdaDrugWarningDraft>> FetchCandidateWarningsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A candidate warning sourced from FDA/NLM, not yet promoted to an advisory.
/// Carries suggested text and target classes for a reviewer to confirm or edit.
/// </summary>
public sealed class FdaDrugWarningDraft
{
    public string Title { get; init; } = string.Empty;
    public AdvisorySourceType SourceType { get; init; } = AdvisorySourceType.OpenFdaLabel;
    public string SourceReference { get; init; } = string.Empty;
    public DateTime? SourcePublishedDate { get; init; }
    public AdvisorySeverity Severity { get; init; } = AdvisorySeverity.Moderate;
    public AdvisoryActionType ActionType { get; init; } = AdvisoryActionType.WarnPatient;

    /// <summary>Suggested VA drug class codes (reviewer confirms the mapping).</summary>
    public List<string> TargetDrugClassCodes { get; init; } = new();

    /// <summary>Suggested patient-facing message (provider edits before sending).</summary>
    public string SuggestedMessage { get; init; } = string.Empty;

    /// <summary>Provider-facing clinical context.</summary>
    public string ClinicalSummary { get; init; } = string.Empty;
}

/// <summary>
/// Offline default. Returns a small curated seed — including the real May 2010 FDA
/// PPI/fracture Drug Safety Communication (VA class GA301) and an Rx→OTC switch
/// reconciliation example — so the workflow is exercisable without network access.
/// </summary>
public sealed class StaticFdaDrugWarningSource : IFdaDrugWarningSource
{
    /// <inheritdoc/>
    public bool IsLiveSource => false;

    /// <inheritdoc/>
    public Task<List<FdaDrugWarningDraft>> FetchCandidateWarningsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<FdaDrugWarningDraft>
        {
            new()
            {
                Title = "Proton pump inhibitors and possible increased risk of bone fracture",
                SourceType = AdvisorySourceType.FdaDrugSafetyCommunication,
                SourceReference = "FDA Drug Safety Communication, May 25, 2010",
                SourcePublishedDate = new DateTime(2010, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                Severity = AdvisorySeverity.High,
                ActionType = AdvisoryActionType.WarnPatient,
                TargetDrugClassCodes = ["GA301"],
                SuggestedMessage =
                    "An FDA safety review found that prescription proton pump inhibitors "
                    + "(medicines for heartburn/reflux such as omeprazole) may be associated "
                    + "with a possible increased risk of fractures of the hip, wrist, and spine, "
                    + "especially with high doses or use longer than one year. Please do not stop "
                    + "your medicine on your own — talk with us about whether it is still needed "
                    + "and about bone health.",
                ClinicalSummary =
                    "Reassess PPI indication and duration; use lowest effective dose; consider "
                    + "calcium/vitamin D and fracture-risk assessment for long-term users.",
            },
            new()
            {
                Title = "Omeprazole now available over the counter — confirm current use",
                SourceType = AdvisorySourceType.RxToOtcSwitch,
                SourceReference = "Rx-to-OTC market switch",
                Severity = AdvisorySeverity.Info,
                ActionType = AdvisoryActionType.ReconcileMedication,
                TargetDrugClassCodes = ["GA301"],
                SuggestedMessage =
                    "This medication is now available without a prescription. If you are still "
                    + "taking it, let us know so we can keep your medication list accurate.",
                ClinicalSummary =
                    "Patients may continue an OTC version off the prescription record. Confirm "
                    + "current use and document as a Non-VA / patient-reported medication if continued.",
            },
        });
}
