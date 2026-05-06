// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient HIPAA disclosure index grain for accounting of disclosures.
/// Key pattern: "ROI-DISC-IDX:{patientId}".
/// Patients may request an accounting of all subject disclosures in the past 6 years.
/// </summary>
public interface IHIPAADisclosureIndexGrain : IGrainWithStringKey
{
    Task UpsertDisclosureAsync(HIPAADisclosureIndexEntry entry);
    Task<List<HIPAADisclosureIndexEntry>> GetAllDisclosuresAsync();
    Task<List<HIPAADisclosureIndexEntry>> GetDisclosuresSubjectToAccountingAsync();
    Task<List<HIPAADisclosureIndexEntry>> GetDisclosuresByDateRangeAsync(DateTime startDate, DateTime endDate);
}
