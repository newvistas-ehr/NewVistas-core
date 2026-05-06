// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class CongressionalInquiryGrain : Grain, ICongressionalInquiryGrain
{
    private readonly IPersistentState<CongressionalInquiryState> _state;

    public CongressionalInquiryGrain(
        [PersistentState("paCongressState", "paCongressStore")] IPersistentState<CongressionalInquiryState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InquiryId))
            _state.State.InquiryId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task CreateInquiryAsync(
        string patientId, string patientName,
        CongressionalInquiryType inquiryType,
        string congressionalOfficeName, string congressionalContactName,
        string congressionalPhone, string congressionalEmail,
        string subject, string inquiryText,
        string linkedComplaintId, string createdBy)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ReceivedDate = DateTime.UtcNow;
        _state.State.InquiryType = inquiryType;
        _state.State.CongressionalOfficeName = congressionalOfficeName;
        _state.State.CongressionalContactName = congressionalContactName;
        _state.State.CongressionalPhone = congressionalPhone;
        _state.State.CongressionalEmail = congressionalEmail;
        _state.State.Subject = subject;
        _state.State.InquiryText = inquiryText;
        _state.State.LinkedComplaintId = linkedComplaintId;
        _state.State.CreatedBy = createdBy;
        _state.State.Status = ComplaintStatus.Received;
        // Federal requirements: 7-day acknowledgment, 20-day full response
        _state.State.AcknowledgmentDue = DateTime.UtcNow.AddDays(7);
        _state.State.ResponseDue = DateTime.UtcNow.AddDays(20);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AssignHandlerAsync(string handlerId, string handlerName)
    {
        _state.State.AssignedHandlerId = handlerId;
        _state.State.AssignedHandlerName = handlerName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeInquiryAsync(DateTime acknowledgmentDate)
    {
        _state.State.AcknowledgmentDate = acknowledgmentDate;
        _state.State.Status = ComplaintStatus.Acknowledged;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordInterimResponseAsync(string interimResponseText)
    {
        _state.State.InterimResponseText = interimResponseText;
        _state.State.InterimResponseDate = DateTime.UtcNow;
        _state.State.Status = ComplaintStatus.UnderInvestigation;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteInquiryAsync(string finalResponseText, ResolutionOutcome outcome)
    {
        _state.State.FinalResponseText = finalResponseText;
        _state.State.FinalResponseDate = DateTime.UtcNow;
        _state.State.Outcome = outcome;
        _state.State.Status = ComplaintStatus.Resolved;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<CongressionalInquiryState> GetInquiryAsync() => Task.FromResult(_state.State);
}
