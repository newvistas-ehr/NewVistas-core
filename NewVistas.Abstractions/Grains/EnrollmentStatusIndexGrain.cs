// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EnrollmentStatusIndexGrain : Grain, IEnrollmentStatusIndexGrain
{
    private readonly IPersistentState<EnrollmentStatusIndexState> _state;

    public EnrollmentStatusIndexGrain(
        [PersistentState("enrollmentStatusIndexState", "enrollmentStatusIndexStore")]
        IPersistentState<EnrollmentStatusIndexState> state)
    {
        _state = state;
    }

    public Task<List<EnrollmentStatusEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task SeedDefaultsAsync()
    {
        if (_state.State.Entries.Count > 0)
            return; // already seeded

        _state.State.Entries = new List<EnrollmentStatusEntry>
        {
            new() { StatusCode = "UNVERIFIED",               StatusName = "Unverified",                    Description = "Application received, not yet reviewed.", IsActive = true },
            new() { StatusCode = "VERIFIED",                 StatusName = "Verified",                      Description = "Enrollment verified and active.", IsActive = true },
            new() { StatusCode = "INACTIVE",                 StatusName = "Inactive",                      Description = "Previously enrolled, now inactive.", IsActive = true },
            new() { StatusCode = "REJECTED",                 StatusName = "Rejected",                      Description = "Application rejected.", IsActive = true },
            new() { StatusCode = "CANCELLED",                StatusName = "Cancelled",                     Description = "Application cancelled by veteran.", IsActive = true },
            new() { StatusCode = "CLOSED",                   StatusName = "Closed",                        Description = "Enrollment closed administratively.", IsActive = true },
            new() { StatusCode = "PENDING REAPPLICATION",   StatusName = "Pending Reapplication",         Description = "Pending reapplication.", IsActive = true },
            new() { StatusCode = "PENDING MT REFUSAL",       StatusName = "Pending MT Refusal",            Description = "Pending — means test refusal.", IsActive = true },
            new() { StatusCode = "PENDING PURPLE HEART",     StatusName = "Pending Purple Heart",          Description = "Pending — Purple Heart veteran determination.", IsActive = true },
            new() { StatusCode = "PENDING ENVIRONMENTAL",    StatusName = "Pending Environmental",         Description = "Pending — environmental exposure review.", IsActive = true },
            new() { StatusCode = "PENDING MST",              StatusName = "Pending MST",                   Description = "Pending — MST treatment history review.", IsActive = true },
            new() { StatusCode = "PENDING OTHER",            StatusName = "Pending Other",                 Description = "Pending — other administrative hold.", IsActive = true },
            new() { StatusCode = "NOT ELIGIBLE REFUSED",     StatusName = "Not Eligible - Refused",        Description = "Not eligible — veteran refused enrollment.", IsActive = true },
            new() { StatusCode = "NOT ELIGIBLE OTHER FED",   StatusName = "Not Eligible - Other Federal",  Description = "Not eligible — covered by other federal health plan.", IsActive = true },
            new() { StatusCode = "DECEASED",                 StatusName = "Deceased",                      Description = "Not eligible — deceased veteran.", IsActive = true },
            new() { StatusCode = "PENDING MEANS TEST",       StatusName = "Pending Means Test",            Description = "Pending — financial means test in progress.", IsActive = true },
            new() { StatusCode = "PENDING ELIGIBILITY",      StatusName = "Pending Eligibility",           Description = "Pending — eligibility determination incomplete.", IsActive = true },
            new() { StatusCode = "NOT ELIGIBLE INCOME",      StatusName = "Not Eligible - Income",         Description = "Not eligible — income threshold exceeded.", IsActive = true },
            new() { StatusCode = "LIMITED BENEFITS",         StatusName = "Limited Benefits",              Description = "Enrolled — eligible for limited benefits only.", IsActive = true },
            new() { StatusCode = "PENDING COMBAT VET",       StatusName = "Pending Combat Veteran",        Description = "Pending — combat veteran review period.", IsActive = true },
            new() { StatusCode = "PENDING CATASTROPHIC",     StatusName = "Pending Catastrophic",          Description = "Pending — catastrophic disability determination.", IsActive = true },
            new() { StatusCode = "NOT ELIGIBLE NON-VET",     StatusName = "Not Eligible - Non-Veteran",    Description = "Not eligible — non-veteran status confirmed.", IsActive = true },
            new() { StatusCode = "PENDING ALIEN",            StatusName = "Pending Alien Eligibility",     Description = "Pending — alien eligibility review.", IsActive = true },
            new() { StatusCode = "SUSPENDED",                StatusName = "Suspended",                     Description = "Enrollment held pending administrative review.", IsActive = true },
        };

        await _state.WriteStateAsync();
    }
}
