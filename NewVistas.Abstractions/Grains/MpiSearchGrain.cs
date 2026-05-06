// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// MPI Search/Index Grain — singleton holding the searchable patient index
/// for cross-facility patient lookup.
///
/// Grain Key: "MPI-INDEX" (singleton)
///
/// Search strategy:
///   - ICN lookup: exact match when input starts with a digit and is 10+ chars
///   - SSN last-4: when input is exactly 4 digits, searches SsnIndex
///   - Name prefix: case-insensitive prefix match on PatientName
///
/// The SsnIndex is maintained on AddOrUpdate/Remove for O(1) SSN lookups.
/// </summary>
public class MpiSearchGrain : Grain, IMpiSearchGrain
{
    private readonly IPersistentState<MpiSearchState> _state;
    private readonly IGrainFactory _grainFactory;

    public MpiSearchGrain(
        [PersistentState("mpiSearchState", "mpiSearchStore")]
        IPersistentState<MpiSearchState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<List<MpiSearchResult>> SearchAsync(string searchText, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<MpiSearchResult>());

        string query = searchText.Trim();

        // If it looks like an ICN (starts with digit, 10+ chars), try exact ICN match
        if (query.Length >= 10 && char.IsDigit(query[0]))
        {
            if (_state.State.Patients.TryGetValue(query, out MpiSearchEntry? entry))
            {
                return Task.FromResult(new List<MpiSearchResult> { ToSearchResult(entry) });
            }
        }

        // If exactly 4 digits, search by SSN last-4
        if (query.Length == 4 && query.All(char.IsDigit))
        {
            List<MpiSearchResult> ssnResults = _state.State.Patients.Values
                .Where(p => p.Ssn.Length >= 4 && p.Ssn[^4..] == query)
                .Take(maxResults)
                .Select(ToSearchResult)
                .ToList();

            return Task.FromResult(ssnResults);
        }

        // Otherwise, search by name prefix (case-insensitive)
        List<MpiSearchResult> nameResults = _state.State.Patients.Values
            .Where(p => p.PatientName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.PatientName)
            .Take(maxResults)
            .Select(ToSearchResult)
            .ToList();

        return Task.FromResult(nameResults);
    }

    public Task<MpiSearchResult?> LookupByIcnAsync(string icn)
    {
        if (_state.State.Patients.TryGetValue(icn, out MpiSearchEntry? entry))
        {
            return Task.FromResult<MpiSearchResult?>(ToSearchResult(entry));
        }

        return Task.FromResult<MpiSearchResult?>(null);
    }

    public Task<List<MpiSearchResult>> LookupBySsnAsync(string ssn)
    {
        if (_state.State.SsnIndex.TryGetValue(ssn, out List<string>? icns))
        {
            List<MpiSearchResult> results = icns
                .Where(icn => _state.State.Patients.ContainsKey(icn))
                .Select(icn => ToSearchResult(_state.State.Patients[icn]))
                .ToList();

            return Task.FromResult(results);
        }

        return Task.FromResult(new List<MpiSearchResult>());
    }

    public async Task AddOrUpdatePatientAsync(MpiSearchEntry entry)
    {
        // Remove old SSN index entry if updating
        if (_state.State.Patients.TryGetValue(entry.Icn, out MpiSearchEntry? existing))
        {
            RemoveFromSsnIndex(existing.Ssn, existing.Icn);
        }

        _state.State.Patients[entry.Icn] = entry;

        // Maintain SSN index
        if (!string.IsNullOrEmpty(entry.Ssn))
        {
            if (!_state.State.SsnIndex.ContainsKey(entry.Ssn))
            {
                _state.State.SsnIndex[entry.Ssn] = new List<string>();
            }

            if (!_state.State.SsnIndex[entry.Ssn].Contains(entry.Icn))
            {
                _state.State.SsnIndex[entry.Ssn].Add(entry.Icn);
            }
        }

        RecalculateTotals();
        _state.State.LastUpdated = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RemovePatientAsync(string icn)
    {
        if (_state.State.Patients.TryGetValue(icn, out MpiSearchEntry? entry))
        {
            RemoveFromSsnIndex(entry.Ssn, icn);
            _state.State.Patients.Remove(icn);

            RecalculateTotals();
            _state.State.LastUpdated = DateTime.UtcNow;

            await _state.WriteStateAsync();
        }
    }

    public Task<MpiIndexStatus> GetStatusAsync()
    {
        return Task.FromResult(new MpiIndexStatus
        {
            TotalPatients = _state.State.TotalPatients,
            TotalCorrelations = _state.State.TotalCorrelations,
            LastUpdated = _state.State.LastUpdated
        });
    }

    public async Task SeedDemoDataAsync()
    {
        // 10 demo patients with realistic ICNs and multi-facility correlations
        var demoPatients = new (string Icn, string Name, string Ssn, DateTime Dob, string Sex, (string FacId, string FacName, string Dfn)[] Facilities)[]
        {
            ("1000000001V123456", "SMITH,JOHN A", "666000001", new DateTime(1955, 3, 15), "M",
                new[] { ("FACILITY-500", "Anytown VAMC", "100001"), ("FACILITY-612", "Northport VAMC", "200001"), ("FACILITY-528", "Buffalo VAMC", "300001") }),

            ("1000000002V234567", "JOHNSON,MARY B", "666000002", new DateTime(1962, 7, 22), "F",
                new[] { ("FACILITY-500", "Anytown VAMC", "100002"), ("FACILITY-630", "New York Harbor VAMC", "400001") }),

            ("1000000003V345678", "WILLIAMS,ROBERT C", "666000003", new DateTime(1948, 11, 8), "M",
                new[] { ("FACILITY-612", "Northport VAMC", "200002"), ("FACILITY-620", "Richmond VAMC", "500001"), ("FACILITY-652", "Richmond VAMC", "600001") }),

            ("1000000004V456789", "BROWN,PATRICIA D", "666000004", new DateTime(1970, 1, 30), "F",
                new[] { ("FACILITY-500", "Anytown VAMC", "100003"), ("FACILITY-528", "Buffalo VAMC", "300002") }),

            ("1000000005V567890", "DAVIS,MICHAEL E", "666000005", new DateTime(1958, 5, 12), "M",
                new[] { ("FACILITY-630", "New York Harbor VAMC", "400002"), ("FACILITY-620", "Richmond VAMC", "500002") }),

            ("1000000006V678901", "GARCIA,MARIA F", "666000006", new DateTime(1975, 9, 3), "F",
                new[] { ("FACILITY-500", "Anytown VAMC", "100004"), ("FACILITY-612", "Northport VAMC", "200003"), ("FACILITY-630", "New York Harbor VAMC", "400003") }),

            ("1000000007V789012", "MARTINEZ,JAMES G", "666000007", new DateTime(1943, 12, 25), "M",
                new[] { ("FACILITY-528", "Buffalo VAMC", "300003"), ("FACILITY-652", "Richmond VAMC", "600002") }),

            ("1000000008V890123", "ANDERSON,SUSAN H", "666000008", new DateTime(1967, 4, 18), "F",
                new[] { ("FACILITY-500", "Anytown VAMC", "100005") }),

            ("1000000009V901234", "TAYLOR,WILLIAM I", "666000009", new DateTime(1952, 8, 7), "M",
                new[] { ("FACILITY-612", "Northport VAMC", "200004"), ("FACILITY-620", "Richmond VAMC", "500003"), ("FACILITY-528", "Buffalo VAMC", "300004") }),

            ("1000000010V012345", "THOMAS,LINDA J", "666000010", new DateTime(1980, 2, 14), "F",
                new[] { ("FACILITY-630", "New York Harbor VAMC", "400004"), ("FACILITY-652", "Richmond VAMC", "600003") }),
        };

        foreach (var patient in demoPatients)
        {
            // Set up correlation grain
            IMpiCorrelationGrain correlationGrain = _grainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{patient.Icn}");
            await correlationGrain.SetCorrelationAsync(patient.Icn, patient.Name, patient.Ssn, patient.Dob, patient.Sex);

            foreach (var facility in patient.Facilities)
            {
                await correlationGrain.AddLocalCorrelationAsync(facility.FacId, facility.FacName, facility.Dfn, DateTime.UtcNow);
            }

            // Add to search index
            await AddOrUpdatePatientAsync(new MpiSearchEntry
            {
                Icn = patient.Icn,
                PatientName = patient.Name,
                Ssn = patient.Ssn,
                DateOfBirth = patient.Dob,
                Sex = patient.Sex,
                FacilityCount = patient.Facilities.Length,
                IsDeceased = false
            });
        }
    }

    private void RemoveFromSsnIndex(string ssn, string icn)
    {
        if (!string.IsNullOrEmpty(ssn) && _state.State.SsnIndex.TryGetValue(ssn, out List<string>? icns))
        {
            icns.Remove(icn);
            if (icns.Count == 0)
            {
                _state.State.SsnIndex.Remove(ssn);
            }
        }
    }

    private void RecalculateTotals()
    {
        _state.State.TotalPatients = _state.State.Patients.Count;
        _state.State.TotalCorrelations = _state.State.Patients.Values.Sum(p => p.FacilityCount);
    }

    private MpiSearchResult ToSearchResult(MpiSearchEntry entry)
    {
        return new MpiSearchResult
        {
            Icn = entry.Icn,
            PatientName = entry.PatientName,
            Ssn = entry.Ssn,
            DateOfBirth = entry.DateOfBirth,
            Sex = entry.Sex,
            IsDeceased = entry.IsDeceased,
            MergedIntoIcn = entry.MergedIntoIcn,
            TreatingFacilities = new List<string>() // Facility names not stored in index entry
        };
    }
}
