// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lightweight metadata index — resolves queries like "last N of type X" into batch
/// grain references. Uses pull-on-activate: rebuilds from batch grains when activated,
/// stays current via Orleans Streams while hot, persists cached state on deactivation.
///
/// Index covers the last 2 years by default. If a query asks for older data,
/// the grain extends coverage by reading additional batch grains.
///
/// Grain Key: PatientLabIndex/{patientIcn}
/// </summary>
public class PatientLabIndexGrain : Grain, IPatientLabIndex
{
    private readonly IPersistentState<PatientLabIndexState> _state;
    private StreamSubscriptionHandle<LabResultEvent>? _subscription;

    public PatientLabIndexGrain(
        [PersistentState("patientLabIndexState", "labIndexStore")]
        IPersistentState<PatientLabIndexState> state)
    {
        _state = state;
    }

    private string PatientIcn
    {
        get
        {
            // Key format: PatientLabIndex/{patientIcn}
            var key = this.GetPrimaryKeyString();
            var slashIndex = key.IndexOf('/');
            return slashIndex >= 0 ? key[(slashIndex + 1)..] : key;
        }
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Subscribe to the patient's lab stream for incremental updates while hot
        try
        {
            var streamProvider = this.GetStreamProvider("LabStreams");
            var stream = streamProvider.GetStream<LabResultEvent>(
                StreamId.Create("PatientLabs", PatientIcn));
            _subscription = await stream.SubscribeAsync(OnNewLabResult);
        }
        catch (KeyNotFoundException)
        {
            // Stream provider not configured — run without streaming
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_subscription != null)
            await _subscription.UnsubscribeAsync();

        // Persist cached index for faster next activation
        await _state.WriteStateAsync();

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task RebuildIndex()
    {
        var entries = new List<LabIndexEntry>();

        // Read last 2 years by default
        var batchKeys = LabBatchKeyHelper
            .GetBatchKeysForRange(PatientIcn,
                DateTimeOffset.UtcNow.AddYears(-2),
                DateTimeOffset.UtcNow)
            .ToList();

        // Fan-out: read all batch grains concurrently
        var tasks = batchKeys.Select(key =>
        {
            var batchGrain = GrainFactory.GetGrain<IPatientLabBatch>(key);
            return (Key: key, Task: batchGrain.GetAllResults());
        }).ToList();

        await Task.WhenAll(tasks.Select(t => t.Task));

        foreach (var (key, task) in tasks)
        {
            var results = task.Result;
            foreach (var result in results)
            {
                entries.Add(new LabIndexEntry
                {
                    LoincCode = result.LoincCode,
                    TestName = result.TestName,
                    ResultDate = result.ResultDate,
                    BatchGrainKey = key,
                    ResultId = result.ResultId,
                    AbnormalFlag = result.AbnormalFlag
                });
            }
        }

        _state.State.Entries = entries.OrderByDescending(e => e.ResultDate).ToList();
        _state.State.KnownBatchKeys = batchKeys;
        _state.State.DistinctTestTypes = entries.Select(e => e.LoincCode).ToHashSet();
        _state.State.LastRebuilt = DateTimeOffset.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<IReadOnlyList<LabIndexEntry>> GetLastNByType(string loincCode, int count)
    {
        await EnsureIndexFresh();
        var results = _state.State.Entries
            .Where(e => e.LoincCode == loincCode)
            .Take(count)
            .ToList();
        return results;
    }

    public async Task<IReadOnlyList<LabIndexEntry>> GetByDateRange(
        DateTimeOffset from, DateTimeOffset to)
    {
        await EnsureIndexFresh();
        var results = _state.State.Entries
            .Where(e => e.ResultDate >= from && e.ResultDate <= to)
            .ToList();
        return results;
    }

    public async Task<IReadOnlyCollection<string>> GetDistinctTestTypes()
    {
        await EnsureIndexFresh();
        return _state.State.DistinctTestTypes;
    }

    public async Task<IReadOnlyList<LabIndexEntry>> GetAbnormalByDateRange(
        DateTimeOffset from, DateTimeOffset to)
    {
        await EnsureIndexFresh();
        var results = _state.State.Entries
            .Where(e => e.ResultDate >= from && e.ResultDate <= to &&
                        e.AbnormalFlag != LabAbnormalFlag.Normal)
            .ToList();
        return results;
    }

    private async Task EnsureIndexFresh()
    {
        if (_state.State.LastRebuilt == null ||
            DateTimeOffset.UtcNow - _state.State.LastRebuilt.Value > TimeSpan.FromMinutes(5))
        {
            await RebuildIndex();
        }
    }

    private Task OnNewLabResult(LabResultEvent evt, StreamSequenceToken? token)
    {
        // Incremental update — add to index without full rebuild
        var batchKey = LabBatchKeyHelper.GetBatchKey(PatientIcn, evt.ResultDate);
        _state.State.Entries.Insert(0, new LabIndexEntry
        {
            LoincCode = evt.LoincCode,
            TestName = evt.TestName,
            ResultDate = evt.ResultDate,
            BatchGrainKey = batchKey,
            ResultId = evt.ResultId,
            AbnormalFlag = evt.AbnormalFlag
        });
        _state.State.DistinctTestTypes.Add(evt.LoincCode);
        // Don't persist on every incremental update — will persist on deactivation
        return Task.CompletedTask;
    }
}
