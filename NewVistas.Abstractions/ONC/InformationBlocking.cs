// ONC Information Blocking Rules — 45 CFR Part 171
// Reference: 21st Century Cures Act §3022; ONC Final Rule (85 FR 25641, May 1, 2020)
// Updated by HTI-1 Final Rule (89 FR 1429, January 9, 2024)

namespace NewVistas.Abstractions.ONC;

/// <summary>
/// Defines the ONC Information Blocking regulatory framework under 45 CFR Part 171.
/// Information blocking is prohibited for health IT developers, HIE/HINs, and health care providers.
/// Penalties: up to $1,000,000 per violation for developers/HIEs (OIG enforcement);
/// Medicare payment disincentives for health care providers (§171.1000–171.1002).
/// </summary>
public static class InformationBlocking
{
    /// <summary>
    /// Actors subject to the information blocking prohibition (§171.101).
    /// </summary>
    public enum CoveredActors
    {
        /// <summary>Health IT developers of certified health IT.</summary>
        HealthItDeveloper,

        /// <summary>Health Information Networks (HINs) or Health Information Exchanges (HIEs).</summary>
        HealthInformationNetworkOrExchange,

        /// <summary>Health care providers.</summary>
        HealthCareProvider
    }

    /// <summary>
    /// Definition elements for "information blocking" under §171.103.
    /// A practice constitutes information blocking when ALL elements are met.
    /// </summary>
    public static class Definition
    {
        /// <summary>
        /// The practice is likely to interfere with the access, exchange, or use of
        /// Electronic Health Information (EHI).
        /// </summary>
        public const string InterferesWithEhiAccess = "Likely to interfere with access, exchange, or use of EHI";

        /// <summary>
        /// For health IT developers and HIE/HINs: the actor knows or should know
        /// that the practice is likely to interfere.
        /// </summary>
        public const string DeveloperKnowledgeStandard = "Actor knows or should know the practice interferes";

        /// <summary>
        /// For health care providers: the provider knows that the practice is
        /// unreasonable and likely to interfere.
        /// </summary>
        public const string ProviderKnowledgeStandard = "Provider knows the practice is unreasonable and likely interferes";
    }

    /// <summary>
    /// Exceptions under Subpart B — Not fulfilling requests.
    /// When one of these exceptions applies, declining to provide EHI is NOT information blocking.
    /// </summary>
    public static class SubpartB_NotFulfillingRequests
    {
        /// <summary>§171.201 — Preventing Harm Exception.
        /// May decline to provide EHI if providing it would create a substantial risk
        /// of harm to the patient or another person (e.g., sensitive psychiatric notes,
        /// information about an abuser in a domestic violence situation).
        /// </summary>
        public const string PreventingHarm = "§171.201";

        /// <summary>§171.202 — Privacy Exception.
        /// May decline if providing EHI is prohibited by applicable privacy law,
        /// such as state law or 42 CFR Part 2 (substance use disorder records).
        /// Actor must implement a process consistent with applicable law.
        /// </summary>
        public const string Privacy = "§171.202";

        /// <summary>§171.203 — Security Exception.
        /// May decline if there is a legitimate, documented security threat.
        /// The security practice must be reasonable and consistent with policies
        /// applied uniformly regardless of who is requesting the EHI.
        /// </summary>
        public const string Security = "§171.203";

        /// <summary>§171.204 — Infeasibility Exception.
        /// May decline if fulfilling the request is technically infeasible,
        /// including due to uncontrollable events, legal restrictions,
        /// or requestor's inability to receive the EHI.
        /// Must document and communicate the specific reason for infeasibility.
        /// </summary>
        public const string Infeasibility = "§171.204";

        /// <summary>§171.205 — Health IT Performance Exception.
        /// May temporarily restrict access to maintain or improve health IT system
        /// performance (e.g., scheduled maintenance windows, emergency outages).
        /// Duration must be no longer than necessary.
        /// </summary>
        public const string HealthItPerformance = "§171.205";

        /// <summary>§171.206 — Protecting Care Access Exception (NEW in HTI-1, 2024).
        /// May limit EHI sharing to reduce potential legal exposure related to
        /// lawful health care activities (e.g., reproductive health care services)
        /// that may be criminalized in some jurisdictions.
        /// Requires documented policy applied consistently.
        /// </summary>
        public const string ProtectingCareAccess = "§171.206";
    }

    /// <summary>
    /// Exceptions under Subpart C — How requests are fulfilled.
    /// When one of these exceptions applies, fulfilling a request in an alternate
    /// manner, with fees, or under license is NOT information blocking.
    /// </summary>
    public static class SubpartC_HowRequestsAreFulfilled
    {
        /// <summary>§171.301 — Content and Manner Exception.
        /// May respond in a different form or format than requested, provided:
        /// (1) the actor is not technically capable of fulfilling in the requested manner, OR
        /// (2) the alternate manner is mutually agreed upon by the actor and requestor.
        /// Cannot use format limitations as a pretext to withhold EHI.
        /// </summary>
        public const string ContentAndManner = "§171.301";

        /// <summary>§171.302 — Fees Exception.
        /// May charge reasonable, cost-based fees for fulfilling EHI requests,
        /// EXCEPT may not charge for EHI needed for treatment or required under other law.
        /// Fees must be based on actual costs, applied consistently, and not used
        /// as a tool to discourage or delay access.
        /// </summary>
        public const string Fees = "§171.302";

        /// <summary>§171.303 — Licensing Exception.
        /// May require licensing of interoperability elements (interfaces, software)
        /// provided the license terms are reasonable and non-discriminatory (RAND).
        /// Cannot use licensing requirements to block interoperability with competitors.
        /// </summary>
        public const string Licensing = "§171.303";
    }

    /// <summary>
    /// Exceptions under Subpart D — TEFCA (Trusted Exchange Framework and Common Agreement).
    /// </summary>
    public static class SubpartD_Tefca
    {
        /// <summary>§171.403 — TEFCA Manner Exception.
        /// May limit EHI exchange responses to TEFCA-based exchange if:
        /// (1) all applicable TEFCA requirements are met, AND
        /// (2) the limitation is not used to exclude specific requestors.
        /// </summary>
        public const string TefcaManner = "§171.403";
    }

    /// <summary>
    /// Conditions of Certification related to information blocking under §170.401–§170.406.
    /// Health IT developers must comply with these as a condition of maintaining certification.
    /// </summary>
    public static class ConditionsOfCertification
    {
        /// <summary>§170.401 — Information blocking compliance.
        /// Developer must not engage in information blocking as defined in §171.103.
        /// </summary>
        public const string InformationBlockingCompliance = "§170.401";

        /// <summary>§170.402 — Assurances condition.
        /// Developer must provide assurances that its health IT will not be used to
        /// engage in information blocking and will be updated to comply with changes in law.
        /// </summary>
        public const string Assurances = "§170.402";

        /// <summary>§170.403 — Communications condition.
        /// Developer must not prohibit or restrict communication about usability problems,
        /// security deficiencies, or other concerns about the health IT product.
        /// </summary>
        public const string Communications = "§170.403";

        /// <summary>§170.404 — Application programming interfaces condition.
        /// Developer must publish and make publicly available a standardized API
        /// conforming to §170.315(g)(10) without special effort.
        /// </summary>
        public const string ApplicationProgrammingInterfaces = "§170.404";

        /// <summary>§170.405 — Real-world testing condition.
        /// Developer must develop and post a real-world testing plan and submit results
        /// demonstrating the health IT functions as certified in real-world settings.
        /// </summary>
        public const string RealWorldTesting = "§170.405";

        /// <summary>§170.406 — Attestations condition.
        /// Developer must annually attest to compliance with all conditions of certification.
        /// </summary>
        public const string Attestations = "§170.406";
    }

    /// <summary>
    /// Enforcement authority and penalty framework.
    /// </summary>
    public static class Enforcement
    {
        /// <summary>
        /// OIG (Office of Inspector General) enforcement applies to:
        /// health IT developers and HIE/HINs.
        /// Maximum penalty: $1,000,000 per violation.
        /// </summary>
        public const string OigMaxPenaltyPerViolation = "$1,000,000";

        /// <summary>
        /// Provider disincentives program (§171.1000–171.1002):
        /// Medicare payment adjustments for health care providers found to have engaged
        /// in information blocking. Administered by CMS.
        /// </summary>
        public const string ProviderDisincentivesProgram = "§171.1000–171.1002";
    }
}
