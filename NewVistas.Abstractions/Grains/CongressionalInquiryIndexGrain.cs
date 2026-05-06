// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

[GenerateSerializer]
public class CongressionalInquiryIndexState
{
    [Id(0)] public List<CongressionalInquiryIndexEntry> Inquiries { get; set; } = new();
}

public class CongressionalInquiryIndexGrain : Grain, ICongressionalInquiryIndexGrain
{
    private readonly IPersistentState<CongressionalInquiryIndexState> _state;

    public CongressionalInquiryIndexGrain(
        [PersistentState("paCongressIndexState", "paCongressIndexStore")] IPersistentState<CongressionalInquiryIndexState> state)
    {
        _state = state;
    }

    public async Task UpsertInquiryAsync(CongressionalInquiryIndexEntry entry)
    {
        CongressionalInquiryIndexEntry? existing = _state.State.Inquiries.Find(i => i.InquiryId == entry.InquiryId);
        if (existing is not null)
            _state.State.Inquiries.Remove(existing);
        _state.State.Inquiries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<CongressionalInquiryIndexEntry>> GetAllInquiriesAsync()
    {
        List<CongressionalInquiryIndexEntry> result = _state.State.Inquiries
            .OrderByDescending(i => i.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CongressionalInquiryIndexEntry>> GetPendingInquiriesAsync()
    {
        List<CongressionalInquiryIndexEntry> result = _state.State.Inquiries
            .Where(i => i.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
                     or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted)
            .OrderBy(i => i.ResponseDue)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CongressionalInquiryIndexEntry>> GetOverdueInquiriesAsync()
    {
        DateTime now = DateTime.UtcNow;
        List<CongressionalInquiryIndexEntry> result = _state.State.Inquiries
            .Where(i => i.ResponseDue < now
                     && i.Status is ComplaintStatus.Received or ComplaintStatus.Acknowledged
                        or ComplaintStatus.UnderInvestigation or ComplaintStatus.ResponseDrafted)
            .OrderBy(i => i.ResponseDue)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<List<CongressionalInquiryIndexEntry>> GetInquiriesByPatientAsync(string patientId)
    {
        List<CongressionalInquiryIndexEntry> result = _state.State.Inquiries
            .Where(i => i.PatientId == patientId)
            .OrderByDescending(i => i.ReceivedDate)
            .ToList();
        return Task.FromResult(result);
    }
}
