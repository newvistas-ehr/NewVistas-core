// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Consult Service Directory — singleton index of available consult services.
/// Maps to VistA SERVICE/SECTION file (#49).
/// </summary>
public class ConsultServiceDirectoryGrain : Grain, IConsultServiceDirectoryGrain
{
    private readonly IPersistentState<ConsultServiceDirectoryState> _state;

    public ConsultServiceDirectoryGrain(
        [PersistentState("consultServiceDirectoryState", "consultServiceStore")] IPersistentState<ConsultServiceDirectoryState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Services.Count == 0)
        {
            await SeedDemoDataAsync();
        }
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<ConsultServiceEntry>> SearchServicesAsync(string searchText, int maxResults)
    {
        string search = searchText.ToUpperInvariant();
        List<ConsultServiceEntry> results = _state.State.Services
            .Where(s => s.IsActive &&
                (s.ServiceName.ToUpperInvariant().Contains(search) ||
                 (s.Specialty != null && s.Specialty.ToUpperInvariant().Contains(search))))
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<ConsultServiceEntry?> GetServiceAsync(string serviceId)
    {
        ConsultServiceEntry? entry = _state.State.Services
            .FirstOrDefault(s => s.ServiceId == serviceId);
        return Task.FromResult(entry);
    }

    public async Task AddServiceAsync(ConsultServiceEntry entry)
    {
        if (!_state.State.Services.Any(s => s.ServiceId == entry.ServiceId))
        {
            _state.State.Services.Add(entry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveServiceAsync(string serviceId)
    {
        int removed = _state.State.Services.RemoveAll(s => s.ServiceId == serviceId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<ConsultServiceEntry>> GetAllServicesAsync() =>
        Task.FromResult(_state.State.Services);

    public async Task SeedDemoDataAsync()
    {
        _state.State.Services =
        [
            new ConsultServiceEntry { ServiceId = "SVC-CARDIOLOGY", ServiceName = "Cardiology", Specialty = "Cardiovascular Medicine", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-PULMONOLOGY", ServiceName = "Pulmonology", Specialty = "Pulmonary Medicine", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-NEUROLOGY", ServiceName = "Neurology", Specialty = "Neurological Medicine", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-ORTHOPEDICS", ServiceName = "Orthopedics", Specialty = "Orthopedic Surgery", IsActive = true, AcceptsInterfacility = false },
            new ConsultServiceEntry { ServiceId = "SVC-GASTROENTEROLOGY", ServiceName = "Gastroenterology", Specialty = "Gastroenterology", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-UROLOGY", ServiceName = "Urology", Specialty = "Urology", IsActive = true, AcceptsInterfacility = false },
            new ConsultServiceEntry { ServiceId = "SVC-OPHTHALMOLOGY", ServiceName = "Ophthalmology", Specialty = "Ophthalmology", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-DERMATOLOGY", ServiceName = "Dermatology", Specialty = "Dermatology", IsActive = true, AcceptsInterfacility = false },
            new ConsultServiceEntry { ServiceId = "SVC-PSYCHIATRY", ServiceName = "Psychiatry", Specialty = "Mental Health", IsActive = true, AcceptsInterfacility = true },
            new ConsultServiceEntry { ServiceId = "SVC-PHYSICAL-THERAPY", ServiceName = "Physical Therapy", Specialty = "Rehabilitation Medicine", IsActive = true, AcceptsInterfacility = false }
        ];
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
