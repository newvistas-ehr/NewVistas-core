// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// A single medical/procedure prior-authorization request lifecycle grain — the parallel to the drug
/// <see cref="IPriorAuthorizationGrain"/>, keyed on a CPT procedure + payer. Grain key:
/// <c>PROC-AUTH:{guid}</c>. Manual-first: the submission channel is tracked so a phone/portal/fax PA is
/// fully modeled; an Electronic channel routes through the deferred 278/PAS transmitter seam.
/// </summary>
public interface IProcedureAuthorizationGrain : IGrainWithStringKey
{
    Task<ProcedureAuthorizationState> GetAsync();

    Task SubmitAsync(
        string patientId, string cptCode, string cptDescription, string payerId, string payerName,
        string orderingProviderId, string orderingProviderName, List<string> diagnosisCodes,
        string clinicalJustification, DateTime? serviceStartDate, DateTime? serviceEndDate,
        ProcedureAuthSubmissionChannel channel, string? orderId, string? consultId, string? externalReferralId);

    /// <summary>Payer pended the request for more information.</summary>
    Task PendAsync(string infoRequested);

    Task ApproveAsync(string reviewerId, string reviewerName, string authorizationNumber, DateTime? expirationDate);

    Task DenyAsync(string reviewerId, string reviewerName, List<ProcedureDenialReason> denialReasons);

    Task ExpireAsync();

    Task CancelAsync();
}

/// <summary>Per-patient index of procedure-PA requests. Grain key: <c>PROC-AUTH-IDX:{patientId}</c>.</summary>
public interface IProcedureAuthorizationIndexGrain : IGrainWithStringKey
{
    Task AddOrUpdateAsync(ProcedureAuthIndexEntry entry);
    Task RemoveAsync(string procAuthId);
    Task<List<ProcedureAuthIndexEntry>> GetAllAsync();
    Task<List<ProcedureAuthIndexEntry>> GetByStatusAsync(ProcedureAuthorizationStatus status);
    Task ClearAsync();
}

/// <summary>
/// The learned half of the requirements KB for ONE (payer, procedure) pair. Grain key:
/// <c>PAYER-PROC:{payerId}:{cptCode}</c>. Accumulates observed denial reasons (by category) and
/// approval evidence from real procedure-PA outcomes.
/// </summary>
public interface IPayerProcedureRequirementIndexGrain : IGrainWithStringKey
{
    /// <summary>Records a denial: increments each cited category; unmapped reasons are kept separately.</summary>
    Task RecordDenialAsync(List<PriorAuthRequirementCategory> categories, string reasonText, string procAuthId);

    /// <summary>Records an approval and which requirement categories it satisfied (optional).</summary>
    Task RecordApprovalAsync(List<PriorAuthRequirementCategory> categoriesSatisfied, string procAuthId);

    Task<PayerProcedureRequirementProfile> GetProfileAsync();
}
