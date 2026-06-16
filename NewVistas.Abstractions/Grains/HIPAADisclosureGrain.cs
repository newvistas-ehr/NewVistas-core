// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class HIPAADisclosureGrain : Grain, IHIPAADisclosureGrain
{
    private readonly IPersistentState<HIPAADisclosureState> _state;

    public HIPAADisclosureGrain(
        [PersistentState("roiDisclosureState", "roiDisclosureStore")] IPersistentState<HIPAADisclosureState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.DisclosureId))
            _state.State.DisclosureId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordDisclosureAsync(
        string patientId, string patientName,
        HIPAADisclosureType disclosureType,
        string recipientName, string recipientOrganization, string recipientAddress,
        string purposeOfDisclosure, string informationDisclosed,
        string dateRangeOfInformation, int numberOfPages,
        bool authorizationReceived, string linkedRequestId,
        string disclosedBy, string disclosedByTitle)
    {
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.DisclosureDate = DateTime.UtcNow;
        _state.State.DisclosureType = disclosureType;
        _state.State.RecipientName = recipientName;
        _state.State.RecipientOrganization = recipientOrganization;
        _state.State.RecipientAddress = recipientAddress;
        _state.State.PurposeOfDisclosure = purposeOfDisclosure;
        _state.State.InformationDisclosed = informationDisclosed;
        _state.State.DateRangeOfInformation = dateRangeOfInformation;
        _state.State.NumberOfPages = numberOfPages;
        _state.State.AuthorizationReceived = authorizationReceived;
        _state.State.LinkedRequestId = linkedRequestId;
        _state.State.DisclosedBy = disclosedBy;
        _state.State.DisclosedByTitle = disclosedByTitle;
        _state.State.CreatedDate = DateTime.UtcNow;
        // TPO (Treatment, Payment, HealthcareOperations) NOT subject to accounting per 45 CFR 164.528
        _state.State.IsSubjectToAccounting = disclosureType is not (
            HIPAADisclosureType.Treatment or
            HIPAADisclosureType.Payment or
            HIPAADisclosureType.HealthcareOperations);
        await _state.WriteStateAsync();
    }

    public Task<HIPAADisclosureState> GetDisclosureAsync() => Task.FromResult(_state.State);
}
