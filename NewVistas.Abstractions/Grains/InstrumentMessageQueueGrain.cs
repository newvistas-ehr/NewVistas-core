// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers.HL7v2;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Instrument Message Queue Grain — VistA LA7 MESSAGE QUEUE file (#62.49).
/// Manages inbound HL7 message processing for a single instrument.
///
/// Mirrors LA7VIN.m (queue polling) and LA7VIN1.m (message parsing/routing).
/// Routes ORU^R01 results into the existing ILabResultIngestion grain.
/// </summary>
public class InstrumentMessageQueueGrain : Grain, IInstrumentMessageQueueGrain
{
    private readonly IPersistentState<InstrumentMessageQueueState> _state;
    private const int MaxLogEntries = 100;

    public InstrumentMessageQueueGrain(
        [PersistentState("instrumentMessageQueue", "instrumentMessageQueueStore")]
        IPersistentState<InstrumentMessageQueueState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstrumentId))
        {
            _state.State.InstrumentId = this.GetPrimaryKeyString();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> EnqueueMessageAsync(string rawHL7)
    {
        // Basic MSH validation before accepting
        string stripped = rawHL7.TrimStart('\x0B').TrimEnd('\x1C', '\x0D');
        if (!stripped.StartsWith("MSH"))
            throw new ArgumentException("HL7 message must begin with MSH segment.");

        string messageId = $"MSG-{Guid.NewGuid()}";

        // Quick parse to extract message type and control ID for the queue entry
        string? messageType = null;
        string? controlId = null;
        try
        {
            HL7Message parsed = HL7Parser.Parse(stripped);
            messageType = parsed.MessageType;
            controlId = parsed.MessageControlId;
        }
        catch
        {
            // Accept anyway — full parse happens during ProcessNext
        }

        _state.State.IncomingQueue.Add(new QueuedHL7Message
        {
            MessageId = messageId,
            RawContent = stripped,
            ReceivedDate = DateTime.UtcNow,
            Status = MessageProcessingStatus.QUEUED,
            MessageType = messageType,
            MessageControlId = controlId
        });

        await _state.WriteStateAsync();

        // Notify the instrument grain that a message was received
        string instrumentId = _state.State.InstrumentId;
        if (instrumentId.StartsWith("LA-MQ:"))
        {
            string instKey = instrumentId.Replace("LA-MQ:", "LA-INST:");
            IAutoInstrumentGrain inst = GrainFactory.GetGrain<IAutoInstrumentGrain>(instKey);
            await inst.RecordMessageReceivedAsync();
        }

        return messageId;
    }

    public async Task<bool> ProcessNextAsync()
    {
        QueuedHL7Message? next = _state.State.IncomingQueue
            .FirstOrDefault(m => m.Status == MessageProcessingStatus.QUEUED);

        if (next == null) return false;

        next.Status = MessageProcessingStatus.PROCESSING;

        try
        {
            HL7Message parsed = HL7Parser.Parse(next.RawContent);
            int resultCount = 0;
            string? patientId = null;

            switch (parsed.MessageType)
            {
                case "ORU":
                    (patientId, resultCount) = await ProcessORU(parsed);
                    break;

                case "ORM":
                    // Order messages — log for now, future expansion
                    patientId = HL7Parser.ExtractPatientId(parsed)?.PatientId;
                    break;

                default:
                    // Unknown message type — log and skip
                    break;
            }

            next.Status = MessageProcessingStatus.COMPLETED;
            _state.State.ProcessedCount++;
            _state.State.LastProcessedDate = DateTime.UtcNow;

            AddLogEntry(next.MessageId, MessageProcessingStatus.COMPLETED, patientId, resultCount, null);
        }
        catch (Exception ex)
        {
            next.Status = MessageProcessingStatus.ERROR;
            next.ErrorMessage = ex.Message;
            _state.State.ErrorCount++;

            AddLogEntry(next.MessageId, MessageProcessingStatus.ERROR, null, 0, ex.Message);

            // Notify instrument of error
            string instrumentId = _state.State.InstrumentId;
            if (instrumentId.StartsWith("LA-MQ:"))
            {
                string instKey = instrumentId.Replace("LA-MQ:", "LA-INST:");
                IAutoInstrumentGrain inst = GrainFactory.GetGrain<IAutoInstrumentGrain>(instKey);
                await inst.RecordErrorAsync();
            }
        }

        await _state.WriteStateAsync();
        return true;
    }

    public async Task<int> ProcessAllAsync()
    {
        int count = 0;
        while (await ProcessNextAsync())
            count++;
        return count;
    }

    public Task<int> GetQueueDepthAsync()
    {
        int depth = _state.State.IncomingQueue
            .Count(m => m.Status == MessageProcessingStatus.QUEUED);
        return Task.FromResult(depth);
    }

    public Task<InstrumentMessageQueueState> GetStatsAsync()
        => Task.FromResult(_state.State);

    public Task<List<MessageLogEntry>> GetRecentLogAsync()
        => Task.FromResult(_state.State.RecentLog);

    public async Task PurgeCompletedAsync()
    {
        _state.State.IncomingQueue.RemoveAll(
            m => m.Status == MessageProcessingStatus.COMPLETED
              || m.Status == MessageProcessingStatus.ERROR);

        // Trim log to last MaxLogEntries
        if (_state.State.RecentLog.Count > MaxLogEntries)
        {
            _state.State.RecentLog = _state.State.RecentLog
                .OrderByDescending(l => l.Timestamp)
                .Take(MaxLogEntries)
                .ToList();
        }

        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Processes an ORU^R01 (unsolicited result) message.
    /// Extracts PID, OBR, OBX segments and routes each result
    /// into the existing ILabResultIngestion grain.
    ///
    /// Mirrors VistA's LA7VIN1.m ORU processing path.
    /// </summary>
    private async Task<(string? patientId, int resultCount)> ProcessORU(HL7Message msg)
    {
        HL7PatientId? patient = HL7Parser.ExtractPatientId(msg);
        if (patient == null || string.IsNullOrEmpty(patient.PatientId))
            throw new InvalidOperationException("ORU message missing patient identification (PID segment).");

        string patientId = patient.PatientId;

        List<HL7ObservationRequest> requests = HL7Parser.ExtractObservationRequests(msg);
        List<HL7ObservationResult> observations = HL7Parser.ExtractObservationResults(msg);

        if (observations.Count == 0)
            throw new InvalidOperationException("ORU message contains no OBX (observation result) segments.");

        // Get instrument grain for test code mapping
        string instrumentId = _state.State.InstrumentId;
        IAutoInstrumentGrain? inst = null;
        if (instrumentId.StartsWith("LA-MQ:"))
        {
            string instKey = instrumentId.Replace("LA-MQ:", "LA-INST:");
            inst = GrainFactory.GetGrain<IAutoInstrumentGrain>(instKey);
        }

        // Get the ingestion grain for this patient
        ILabResultIngestion ingestion = GrainFactory.GetGrain<ILabResultIngestion>(patientId);

        // Panel name from the first OBR
        string? panelName = requests.Count > 0 ? requests[0].UniversalServiceText : null;
        string? specimen = requests.Count > 0 ? requests[0].SpecimenSource : null;

        int resultCount = 0;

        foreach (HL7ObservationResult obs in observations)
        {
            // Skip non-final results
            if (obs.ResultStatus == "X") continue; // Cancelled

            // Resolve test mapping if we have an instrument
            string loincCode = obs.ObservationId ?? "";
            string testName = obs.ObservationText ?? "";

            if (inst != null && !string.IsNullOrEmpty(obs.ObservationId))
            {
                InstrumentTestMapping? mapping = await inst.ResolveTestMappingAsync(obs.ObservationId);
                if (mapping != null)
                {
                    loincCode = mapping.InternalLoincCode;
                    testName = string.IsNullOrEmpty(testName) ? mapping.TestName : testName;
                }
            }

            // Map HL7 abnormal flags to our LabAbnormalFlag enum
            LabAbnormalFlag abnormalFlag = MapAbnormalFlag(obs.AbnormalFlags);

            // Parse reference range (format: "low-high" or "low - high")
            string referenceRange = obs.ReferenceRange ?? "";

            // Build the LabResultDetail for ingestion
            LabResultDetail detail = new()
            {
                ResultId = $"LA-RESULT-{Guid.NewGuid()}",
                LoincCode = loincCode,
                TestName = testName,
                Value = obs.ObservationValue ?? "",
                Units = obs.Units ?? "",
                ReferenceRange = referenceRange,
                AbnormalFlag = abnormalFlag,
                ResultDate = DateTimeOffset.UtcNow,
                FacilityCode = msg.SendingFacility ?? "",
                FacilityName = msg.SendingFacility ?? "",
                Specimen = specimen ?? "",
                PanelName = panelName
            };

            await ingestion.IngestResult(detail);
            resultCount++;
        }

        return (patientId, resultCount);
    }

    /// <summary>
    /// Maps HL7 Table 0078 abnormal flag codes to our LabAbnormalFlag enum.
    /// Mirrors VistA's result status mapping in LA7VOBX.m OBX11.
    /// </summary>
    private static LabAbnormalFlag MapAbnormalFlag(string? hl7Flag)
    {
        return hl7Flag?.ToUpperInvariant() switch
        {
            "H" => LabAbnormalFlag.High,
            "L" => LabAbnormalFlag.Low,
            "HH" or "HU" => LabAbnormalFlag.CriticalHigh,
            "LL" or "LU" => LabAbnormalFlag.CriticalLow,
            "A" or "AA" => LabAbnormalFlag.Abnormal,
            "N" or null or "" => LabAbnormalFlag.Normal,
            _ => LabAbnormalFlag.Abnormal
        };
    }

    private void AddLogEntry(string messageId, MessageProcessingStatus status,
        string? patientId, int resultCount, string? message)
    {
        _state.State.RecentLog.Add(new MessageLogEntry
        {
            MessageId = messageId,
            Timestamp = DateTime.UtcNow,
            Status = status,
            PatientId = patientId,
            ResultCount = resultCount,
            Message = message
        });

        // Keep log trimmed
        if (_state.State.RecentLog.Count > MaxLogEntries)
        {
            _state.State.RecentLog.RemoveAt(0);
        }
    }
}
