// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EligibilityInquiryGrain : Grain, IEligibilityInquiryGrain
{
    private readonly IPersistentState<EligibilityInquiryState> _state;

    public EligibilityInquiryGrain(
        [PersistentState("eligibilityInquiryState", "eligibilityInquiryStore")]
        IPersistentState<EligibilityInquiryState> state)
    {
        _state = state;
    }

    public Task<EligibilityInquiryState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string patientId,
        string? insurancePlanId,
        string? personalPolicyId,
        string payerId,
        string payerName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? patientDateOfBirth,
        List<string> serviceTypeCodes,
        DateTime serviceDate,
        string? initiatedByUserId,
        string? initiatedByUserName,
        string? notes)
    {
        string inquiryId = this.GetPrimaryKeyString();
        DateTime now = DateTime.UtcNow;

        _state.State.InquiryId                = inquiryId;
        _state.State.PatientId                = patientId;
        _state.State.InsurancePlanId          = insurancePlanId;
        _state.State.PersonalPolicyId         = personalPolicyId;
        _state.State.PayerId                  = payerId;
        _state.State.PayerName                = payerName;
        _state.State.SubscriberId             = subscriberId;
        _state.State.SubscriberName           = subscriberName;
        _state.State.RelationshipToSubscriber = relationshipToSubscriber;
        _state.State.PatientDateOfBirth       = patientDateOfBirth;
        _state.State.ServiceTypeCodes         = serviceTypeCodes;
        _state.State.ServiceDate              = serviceDate;
        _state.State.InitiatedByUserId        = initiatedByUserId;
        _state.State.InitiatedByUserName      = initiatedByUserName;
        _state.State.Notes                    = notes;
        _state.State.Status                   = EligibilityInquiryStatus.Draft;
        _state.State.CreatedDate              = now;
        _state.State.LastModifiedDate         = now;

        await _state.WriteStateAsync();
        return inquiryId;
    }

    public async Task SubmitAsync(string? traceNumber)
    {
        _state.State.Status           = EligibilityInquiryStatus.Submitted;
        _state.State.SubmittedDate    = DateTime.UtcNow;
        _state.State.TraceNumber      = traceNumber;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordEligibleResponseAsync(
        CoverageLevel coverageLevel,
        string? responsePlanName,
        DateTime? coverageEffectiveDate,
        DateTime? coverageTerminationDate,
        string? responseGroupNumber,
        List<EligibilityBenefitDetail>? benefitDetails,
        string? payerMessage)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.Status                  = EligibilityInquiryStatus.Eligible;
        _state.State.ResponseDate            = now;
        _state.State.CoverageLevel           = coverageLevel;
        _state.State.ResponsePlanName        = responsePlanName;
        _state.State.CoverageEffectiveDate   = coverageEffectiveDate;
        _state.State.CoverageTerminationDate = coverageTerminationDate;
        _state.State.ResponseGroupNumber     = responseGroupNumber;
        _state.State.PayerMessage            = payerMessage;
        _state.State.LastModifiedDate        = now;

        if (benefitDetails is not null)
            _state.State.BenefitDetails = benefitDetails;

        await _state.WriteStateAsync();
    }

    public async Task RecordIneligibleResponseAsync(
        CoverageLevel coverageLevel,
        List<string>? rejectReasonCodes,
        string? payerMessage)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.Status           = EligibilityInquiryStatus.Ineligible;
        _state.State.ResponseDate     = now;
        _state.State.CoverageLevel    = coverageLevel;
        _state.State.PayerMessage     = payerMessage;
        _state.State.LastModifiedDate = now;

        if (rejectReasonCodes is not null)
            _state.State.RejectReasonCodes = rejectReasonCodes;

        await _state.WriteStateAsync();
    }

    public async Task RecordErrorResponseAsync(
        List<string> rejectReasonCodes,
        string? payerMessage)
    {
        DateTime now = DateTime.UtcNow;

        _state.State.Status            = EligibilityInquiryStatus.Error;
        _state.State.ResponseDate      = now;
        _state.State.RejectReasonCodes = rejectReasonCodes;
        _state.State.PayerMessage      = payerMessage;
        _state.State.LastModifiedDate  = now;

        await _state.WriteStateAsync();
    }
}
