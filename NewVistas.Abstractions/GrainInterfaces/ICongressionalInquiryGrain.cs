// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single Congressional inquiry with strict federal timeline tracking.
/// Federal requirements: 7-day acknowledgment, 20-day full response.
/// </summary>
public interface ICongressionalInquiryGrain : IGrainWithStringKey
{
    Task CreateInquiryAsync(
        string patientId,
        string patientName,
        CongressionalInquiryType inquiryType,
        string congressionalOfficeName,
        string congressionalContactName,
        string congressionalPhone,
        string congressionalEmail,
        string subject,
        string inquiryText,
        string linkedComplaintId,
        string createdBy);

    Task AssignHandlerAsync(string handlerId, string handlerName);
    Task AcknowledgeInquiryAsync(DateTime acknowledgmentDate);
    Task RecordInterimResponseAsync(string interimResponseText);
    Task CompleteInquiryAsync(string finalResponseText, ResolutionOutcome outcome);
    Task<CongressionalInquiryState> GetInquiryAsync();
}
