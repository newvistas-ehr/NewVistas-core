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
/// Home-Health Agency Directory Grain — singleton directory of home-health agencies. Key
/// "HHA-DIRECTORY". Mirrors the PharmacyDirectory singleton pattern; auto-seeds a demo set on first
/// read so the picker is never empty.
/// </summary>
public class HomeHealthAgencyDirectoryGrain : Grain, IHomeHealthAgencyDirectoryGrain
{
    private readonly IPersistentState<HomeHealthAgencyDirectoryState> _state;

    public HomeHealthAgencyDirectoryGrain(
        [PersistentState("homeHealthAgencyDirectory", "homeHealthAgencyDirectoryStore")]
        IPersistentState<HomeHealthAgencyDirectoryState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(HomeHealthAgencyEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.AgencyId))
            return;

        _state.State.Agencies[entry.AgencyId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(string agencyId, bool isActive)
    {
        if (_state.State.Agencies.TryGetValue(agencyId, out HomeHealthAgencyEntry? entry))
        {
            _state.State.Agencies[agencyId] = entry with { IsActive = isActive };
            await _state.WriteStateAsync();
        }
    }

    public async Task<HomeHealthAgencyEntry?> GetAsync(string agencyId)
    {
        await EnsureSeededAsync();
        return _state.State.Agencies.GetValueOrDefault(agencyId);
    }

    public async Task<List<HomeHealthAgencyEntry>> SearchAsync(string searchTerm, bool externalOnly = false, int maxResults = 25)
    {
        await EnsureSeededAsync();
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<HomeHealthAgencyEntry>();

        string term = searchTerm.Trim();

        return _state.State.Agencies.Values
            .Where(a => a.IsActive)
            .Where(a => !externalOnly || a.Kind != HomeHealthAgencyKinds.InHouse)
            .Where(a => a.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(a.AgencyId, term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    public async Task<List<HomeHealthAgencyEntry>> GetAllAsync(bool externalOnly = false)
    {
        await EnsureSeededAsync();
        return _state.State.Agencies.Values
            .Where(a => a.IsActive)
            .Where(a => !externalOnly || a.Kind != HomeHealthAgencyKinds.InHouse)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Agencies.Count);

    private async Task EnsureSeededAsync()
    {
        if (!_state.State.DemoSeeded)
            await SeedDemoAgenciesAsync();
    }

    public async Task SeedDemoAgenciesAsync()
    {
        if (_state.State.DemoSeeded) return;

        // Demo NPI/CCN ids are illustrative. The in-house agency is the health system's own licensed
        // home-health agency — the delivering org for hospital-provided care that bills through it.
        HomeHealthAgencyEntry[] seed =
        {
            new() { AgencyId = "HHA-NEWVISTAS", Name = "NEWVISTAS HOME HEALTH (IN-HOUSE)", Kind = HomeHealthAgencyKinds.InHouse,
                    Npi = "1902884561", Ccn = "227001",
                    Address = "1 Medical Center Dr", City = "Springfield", State = "MA", Zip = "01101", Phone = "413-555-0700",
                    ServiceArea = "Hampden County, MA",
                    Disciplines = new() { HomeCareDiscipline.SkilledNursing, HomeCareDiscipline.PhysicalTherapy,
                                          HomeCareDiscipline.OccupationalTherapy, HomeCareDiscipline.MedicalSocialWork,
                                          HomeCareDiscipline.HomeHealthAide } },
            new() { AgencyId = "HHA-VALLEY-VNA", Name = "VALLEY VNA HOME HEALTH", Kind = HomeHealthAgencyKinds.External,
                    Npi = "1730188456", Ccn = "227312",
                    Address = "35 Pearl St", City = "Holyoke", State = "MA", Zip = "01040", Phone = "413-555-0810", Fax = "413-555-0811",
                    ServiceArea = "Hampden & Hampshire County, MA",
                    Disciplines = new() { HomeCareDiscipline.SkilledNursing, HomeCareDiscipline.PhysicalTherapy,
                                          HomeCareDiscipline.OccupationalTherapy, HomeCareDiscipline.HomeHealthAide } },
            new() { AgencyId = "HHA-PIONEER", Name = "PIONEER VALLEY VISITING NURSES", Kind = HomeHealthAgencyKinds.External,
                    Npi = "1043319872", Ccn = "227455",
                    Address = "140 High St", City = "Greenfield", State = "MA", Zip = "01301", Phone = "413-555-0920",
                    ServiceArea = "Franklin County, MA",
                    Disciplines = new() { HomeCareDiscipline.SkilledNursing, HomeCareDiscipline.PhysicalTherapy,
                                          HomeCareDiscipline.SpeechLanguagePathology } },
        };

        foreach (HomeHealthAgencyEntry a in seed)
            _state.State.Agencies.TryAdd(a.AgencyId, a);

        _state.State.DemoSeeded = true;
        await _state.WriteStateAsync();
    }
}
