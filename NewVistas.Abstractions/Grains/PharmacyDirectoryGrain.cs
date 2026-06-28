// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacy Directory Grain — singleton directory of dispensing pharmacies. Key
/// "PHARMACY-DIRECTORY". Mirrors the ProviderDirectory / ClinicIndex singleton pattern;
/// auto-seeds a demo set on first read so the picker is never empty.
/// </summary>
public class PharmacyDirectoryGrain : Grain, IPharmacyDirectoryGrain
{
    private readonly IPersistentState<PharmacyDirectoryState> _state;

    public PharmacyDirectoryGrain(
        [PersistentState("pharmacyDirectory", "pharmacyDirectoryStore")]
        IPersistentState<PharmacyDirectoryState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(PharmacyDirectoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.PharmacyId))
            return;

        _state.State.Pharmacies[entry.PharmacyId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(string pharmacyId, bool isActive)
    {
        if (_state.State.Pharmacies.TryGetValue(pharmacyId, out PharmacyDirectoryEntry? entry))
        {
            _state.State.Pharmacies[pharmacyId] = entry with { IsActive = isActive };
            await _state.WriteStateAsync();
        }
    }

    public async Task<PharmacyDirectoryEntry?> GetAsync(string pharmacyId)
    {
        await EnsureSeededAsync();
        return _state.State.Pharmacies.GetValueOrDefault(pharmacyId);
    }

    public async Task<List<PharmacyDirectoryEntry>> SearchAsync(string searchTerm, bool outpatientOnly = true, int maxResults = 25)
    {
        await EnsureSeededAsync();
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<PharmacyDirectoryEntry>();

        string term = searchTerm.Trim();

        return _state.State.Pharmacies.Values
            .Where(p => p.IsActive)
            .Where(p => !outpatientOnly || p.Kind != PharmacyKinds.Inpatient)
            .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(p.PharmacyId, term, StringComparison.OrdinalIgnoreCase)
                     || (p.NcpdpId != null && p.NcpdpId.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    public async Task<List<PharmacyDirectoryEntry>> GetAllAsync(bool outpatientOnly = true)
    {
        await EnsureSeededAsync();
        return _state.State.Pharmacies.Values
            .Where(p => p.IsActive)
            .Where(p => !outpatientOnly || p.Kind != PharmacyKinds.Inpatient)
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Pharmacies.Count);

    private async Task EnsureSeededAsync()
    {
        if (_state.State.Pharmacies.Count == 0)
            await SeedDemoPharmaciesAsync();
    }

    public async Task SeedDemoPharmaciesAsync()
    {
        if (_state.State.Pharmacies.Count > 0) return;

        // Demo NCPDP ids are illustrative (real ones are assigned by NCPDP). The hospital
        // pharmacy dispenses in-house, so it has no NCPDP id and isn't e-Rx routable.
        PharmacyDirectoryEntry[] seed =
        {
            new() { PharmacyId = "PHARM-HOSPITAL", Name = "NEWVISTAS HOSPITAL PHARMACY", Kind = PharmacyKinds.Inpatient,
                    AcceptsErx = false, Address = "1 Medical Center Dr", City = "Springfield", State = "MA", Zip = "01101", Phone = "413-555-0100" },
            new() { PharmacyId = "PHARM-MAIL-CMOP", Name = "NEWVISTAS MAIL PHARMACY (CMOP)", Kind = PharmacyKinds.Mail,
                    NcpdpId = "5512340", Npi = "1356789012", Address = "500 Distribution Way", City = "Hines", State = "IL", Zip = "60141", Phone = "800-555-6200" },
            new() { PharmacyId = "PHARM-CVS-4501", Name = "CVS PHARMACY #4501", Kind = PharmacyKinds.Retail,
                    NcpdpId = "1234567", Npi = "1234567893", Address = "100 Main St", City = "Springfield", State = "MA", Zip = "01103", Phone = "413-555-0150", Fax = "413-555-0151" },
            new() { PharmacyId = "PHARM-WAG-2210", Name = "WALGREENS #2210", Kind = PharmacyKinds.Retail,
                    NcpdpId = "2345678", Npi = "1245678901", Address = "250 Oak Ave", City = "Springfield", State = "MA", Zip = "01104", Phone = "413-555-0220", Fax = "413-555-0221" },
            new() { PharmacyId = "PHARM-RA-1187", Name = "RITE AID #1187", Kind = PharmacyKinds.Retail,
                    NcpdpId = "3456789", Npi = "1356789013", Address = "77 Elm St", City = "Chicopee", State = "MA", Zip = "01013", Phone = "413-555-0330" },
            new() { PharmacyId = "PHARM-COSTCO-88", Name = "COSTCO PHARMACY #88", Kind = PharmacyKinds.Retail,
                    NcpdpId = "4567890", Npi = "1467890124", Address = "900 Riverdale St", City = "West Springfield", State = "MA", Zip = "01089", Phone = "413-555-0440" },
            new() { PharmacyId = "PHARM-GOODHEALTH", Name = "GOODHEALTH COMMUNITY PHARMACY", Kind = PharmacyKinds.Specialty,
                    NcpdpId = "5678901", Npi = "1578901235", Address = "12 Center Sq", City = "Northampton", State = "MA", Zip = "01060", Phone = "413-555-0550" },
        };

        foreach (PharmacyDirectoryEntry p in seed)
            _state.State.Pharmacies[p.PharmacyId] = p;

        await _state.WriteStateAsync();
    }
}
