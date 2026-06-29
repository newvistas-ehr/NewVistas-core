// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HomeHealthBillingGrain : Grain, IHomeHealthBillingGrain
{
    private const string KeyPrefix = "HHC-BILLING:";
    private readonly IPersistentState<HomeHealthBillingState> _state;
    private readonly IHomeHealthClaimTransmitter? _transmitter;

    public HomeHealthBillingGrain(
        [PersistentState("homeHealthBillingState", "homeHealthBillingStore")] IPersistentState<HomeHealthBillingState> state,
        IServiceProvider serviceProvider)
    {
        _state = state;
        // Optional external-transmission seam — null when no implementation is registered, in which
        // case submissions record a stand-in control number.
        _transmitter = serviceProvider.GetService<IHomeHealthClaimTransmitter>();
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EpisodeId))
        {
            string key = this.GetPrimaryKeyString();
            _state.State.EpisodeId = key.StartsWith(KeyPrefix, StringComparison.Ordinal)
                ? key[KeyPrefix.Length..]
                : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task SubmitNoticeOfAdmissionAsync(string patientId, DateTime admissionDate, DateTime submittedDate)
    {
        _state.State.PatientId = patientId;
        string control = _transmitter is not null
            ? await _transmitter.TransmitNoaAsync(_state.State.EpisodeId, patientId, admissionDate)
            : $"NOA-{Guid.NewGuid():N}"[..16];

        _state.State.Noa.Status = NoaStatus.Submitted;
        _state.State.Noa.AdmissionDate = admissionDate;
        _state.State.Noa.SubmittedDate = submittedDate;
        _state.State.Noa.ControlNumber = control;
        _state.State.Noa.IsLate = submittedDate.Date > admissionDate.Date.AddDays(5);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<string> GenerateClaimAsync(string certificationPeriodId, string paymentPeriodId, string hippsCode, bool isLupa)
    {
        string claimId = $"HHC-CLAIM:{Guid.NewGuid()}";
        _state.State.Claims.Add(new HomeHealthClaim
        {
            ClaimId = claimId,
            CertificationPeriodId = certificationPeriodId,
            PaymentPeriodId = paymentPeriodId,
            HippsCode = hippsCode,
            IsLupa = isLupa,
            Status = HomeHealthClaimStatus.Draft,
            Notes = isLupa ? "LUPA — paid per visit (below the case-mix visit threshold)." : string.Empty
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return claimId;
    }

    public async Task SubmitClaimAsync(string claimId, DateTime submittedDate)
    {
        HomeHealthClaim? claim = _state.State.Claims.FirstOrDefault(c => c.ClaimId == claimId);
        if (claim is null) return;
        string control = _transmitter is not null
            ? await _transmitter.TransmitClaimAsync(_state.State.EpisodeId, claim.HippsCode)
            : $"CLM-{Guid.NewGuid():N}"[..16];
        claim.Status = HomeHealthClaimStatus.Submitted;
        claim.SubmittedDate = submittedDate;
        claim.ControlNumber = control;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<HomeHealthBillingState> GetBillingAsync() => Task.FromResult(_state.State);
}
