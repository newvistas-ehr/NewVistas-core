// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Social Work Referral Grain — VistA File #707 referral sub-file.
/// Key: "SW-REFERRAL:{guid}"
/// </summary>
public class SocialWorkReferralGrain : Grain, ISocialWorkReferralGrain
{
    private readonly IPersistentState<SocialWorkReferralState> _state;

    public SocialWorkReferralGrain(
        [PersistentState("socialWorkReferralState", "socialWorkReferralStore")]
        IPersistentState<SocialWorkReferralState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReferralId))
        {
            _state.State.ReferralId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<SocialWorkReferralState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        DateTime referralDate,
        string? referralSource,
        string? referralReason,
        SocialWorkReferralServiceType serviceType,
        string? agencyName,
        string? agencyContact,
        string? agencyPhone,
        string? socialWorkerId,
        string? socialWorkerName,
        DateTime? followUpDate,
        string? assessmentId,
        string? locationId,
        string? locationName,
        string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.ReferralDate = referralDate;
        _state.State.ReferralSource = referralSource;
        _state.State.ReferralReason = referralReason;
        _state.State.ServiceType = serviceType;
        _state.State.AgencyName = agencyName;
        _state.State.AgencyContact = agencyContact;
        _state.State.AgencyPhone = agencyPhone;
        _state.State.SocialWorkerId = socialWorkerId;
        _state.State.SocialWorkerName = socialWorkerName;
        _state.State.FollowUpDate = followUpDate;
        _state.State.AssessmentId = assessmentId;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Comments = comments;
        _state.State.Status = SocialWorkReferralStatus.Pending;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(
        SocialWorkReferralStatus status,
        string? outcomeNotes,
        DateTime? followUpDate)
    {
        _state.State.Status = status;
        if (outcomeNotes != null)
            _state.State.OutcomeNotes = outcomeNotes;
        if (followUpDate.HasValue)
            _state.State.FollowUpDate = followUpDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcceptAsync(DateTime acceptedDate)
    {
        _state.State.Status = SocialWorkReferralStatus.Active;
        _state.State.AcceptedDate = acceptedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CloseAsync(string? outcomeNotes)
    {
        _state.State.Status = SocialWorkReferralStatus.Closed;
        _state.State.ClosedDate = DateTime.UtcNow;
        if (outcomeNotes != null)
            _state.State.OutcomeNotes = outcomeNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
