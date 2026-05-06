// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Entry point for lab result ingestion. Orchestrates the minimal write path:
///   1. Write to time-partitioned batch grain
///   2. Push-update the summary grain
///   3. Publish to Orleans Stream (index grain picks up IF hot)
/// Only 2 grains are touched on every write. The stream publish is fire-and-forget.
///
/// Grain Key: LabIngestion/{patientIcn}
/// </summary>
public class LabResultIngestionGrain : Grain, ILabResultIngestion
{
    public async Task IngestResult(LabResultDetail result)
    {
        var patientIcn = this.GetPrimaryKeyString();

        // Step 1: Write to the time-partitioned batch grain
        var batchKey = LabBatchKeyHelper.GetBatchKey(patientIcn, result.ResultDate);
        var batchGrain = GrainFactory.GetGrain<IPatientLabBatch>(batchKey);
        await batchGrain.AddResult(result);

        // Step 2: Update the summary grain (push — must be immediately consistent)
        var summaryKey = $"PatientLabSummary/{patientIcn}";
        var summaryGrain = GrainFactory.GetGrain<IPatientLabSummary>(summaryKey);
        var evt = LabResultEvent.FromResult(result, patientIcn);
        await summaryGrain.RecordNewResult(evt);

        // Step 3: Publish to Orleans Stream (index grain picks this up IF it's hot)
        try
        {
            var streamProvider = this.GetStreamProvider("LabStreams");
            var stream = streamProvider.GetStream<LabResultEvent>(
                StreamId.Create("PatientLabs", patientIcn));
            await stream.OnNextAsync(evt);
        }
        catch (KeyNotFoundException)
        {
            // Stream provider not configured — index grain will rebuild on next activation
        }
    }
}
