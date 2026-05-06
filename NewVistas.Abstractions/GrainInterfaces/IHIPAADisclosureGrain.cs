// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single HIPAA disclosure record.
/// Per 45 CFR 164.528, certain disclosures must be tracked for the accounting of disclosures.
/// VistA File #195.1 (ROI ACCOUNTING OF DISCLOSURES). ROIA.m
/// </summary>
public interface IHIPAADisclosureGrain : IGrainWithStringKey
{
    Task RecordDisclosureAsync(
        string patientId,
        string patientName,
        HIPAADisclosureType disclosureType,
        string recipientName,
        string recipientOrganization,
        string recipientAddress,
        string purposeOfDisclosure,
        string informationDisclosed,
        string dateRangeOfInformation,
        int numberOfPages,
        bool authorizationReceived,
        string linkedRequestId,
        string disclosedBy,
        string disclosedByTitle);

    Task<HIPAADisclosureState> GetDisclosureAsync();
}
