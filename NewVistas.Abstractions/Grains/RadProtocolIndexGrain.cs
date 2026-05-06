// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class RadProtocolIndexGrain : Grain, IRadProtocolIndexGrain
{
    private readonly IPersistentState<RadProtocolIndexState> _state;
    public RadProtocolIndexGrain([PersistentState("radProtocolIndexState", "radProtocolIndexStore")] IPersistentState<RadProtocolIndexState> state) { _state = state; }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Entries.Count == 0)
        {
            _state.State.Entries = new List<RadProtocolIndexEntry>
            {
                new() { ProtocolId = "PROT-CXR-PA", ProtocolName = "Chest X-Ray PA/Lateral", ImagingType = "GENERAL RADIOLOGY", BodyPart = "CHEST", IsActive = true },
                new() { ProtocolId = "PROT-CT-HEAD", ProtocolName = "CT Head Without Contrast", ImagingType = "CT SCAN", BodyPart = "HEAD", IsActive = true },
                new() { ProtocolId = "PROT-CT-ABD", ProtocolName = "CT Abdomen/Pelvis With Contrast", ImagingType = "CT SCAN", BodyPart = "ABDOMEN", IsActive = true },
                new() { ProtocolId = "PROT-MRI-BRAIN", ProtocolName = "MRI Brain With/Without Contrast", ImagingType = "MRI", BodyPart = "HEAD", IsActive = true },
                new() { ProtocolId = "PROT-MRI-KNEE", ProtocolName = "MRI Knee Without Contrast", ImagingType = "MRI", BodyPart = "EXTREMITY", IsActive = true },
                new() { ProtocolId = "PROT-US-ABD", ProtocolName = "Ultrasound Abdomen Complete", ImagingType = "ULTRASOUND", BodyPart = "ABDOMEN", IsActive = true },
                new() { ProtocolId = "PROT-DEXA", ProtocolName = "DEXA Bone Density", ImagingType = "GENERAL RADIOLOGY", BodyPart = "SPINE", IsActive = true },
                new() { ProtocolId = "PROT-MAMMO", ProtocolName = "Mammography Bilateral Screening", ImagingType = "GENERAL RADIOLOGY", BodyPart = "CHEST", IsActive = true },
            };
            await _state.WriteStateAsync();
        }
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<RadProtocolIndexEntry>> GetAllAsync() => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(RadProtocolIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.ProtocolId == entry.ProtocolId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public Task<List<RadProtocolIndexEntry>> SearchAsync(string query)
        => Task.FromResult(_state.State.Entries.Where(e =>
            e.ProtocolName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (e.BodyPart?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList());

    public Task<List<RadProtocolIndexEntry>> GetByImagingTypeAsync(string imagingType)
        => Task.FromResult(_state.State.Entries.Where(e =>
            e.ImagingType.Equals(imagingType, StringComparison.OrdinalIgnoreCase) && e.IsActive).ToList());
}
