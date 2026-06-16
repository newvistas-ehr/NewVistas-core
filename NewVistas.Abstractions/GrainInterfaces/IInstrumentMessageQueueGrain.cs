// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Instrument Message Queue Grain Interface based on VistA LA7 MESSAGE QUEUE
/// file (#62.49). Each grain manages the inbound HL7 message queue for one instrument.
///
/// Grain Key: "LA-MQ:{instrumentId}"
///
/// Mirrors VistA routines:
/// - LA7VIN.m EN/GETIN — polls incoming queue, dequeues messages
/// - LA7VIN1.m NXTMSG — parses and routes each message by type
/// - LA7LOG.m CREATE — logs processing results and errors
///
/// Processing flow:
///   1. TCP listener calls EnqueueMessageAsync with raw HL7
///   2. ProcessNextAsync dequeues, parses via HL7Parser, routes by message type
///   3. ORU^R01 results → ILabResultIngestion.IngestResult (existing grain)
///   4. Statistics and log updated
/// </summary>
public interface IInstrumentMessageQueueGrain : IGrainWithStringKey
{
    /// <summary>
    /// Enqueues a raw HL7 message for processing.
    /// Validates MSH segment is present before accepting.
    /// Returns the assigned message ID.
    /// </summary>
    Task<string> EnqueueMessageAsync(string rawHL7);

    /// <summary>
    /// Processes the next queued message. Dequeues, parses, routes by type.
    /// Returns true if a message was processed, false if queue was empty.
    /// </summary>
    Task<bool> ProcessNextAsync();

    /// <summary>
    /// Processes all queued messages in order. Returns count processed.
    /// Mirrors VistA LA7VIN.m's loop that processes ~10 messages per cycle.
    /// </summary>
    Task<int> ProcessAllAsync();

    /// <summary>
    /// Returns the current queue depth (number of unprocessed messages).
    /// </summary>
    Task<int> GetQueueDepthAsync();

    /// <summary>
    /// Returns processing statistics for this instrument's queue.
    /// </summary>
    Task<GrainStates.InstrumentMessageQueueState> GetStatsAsync();

    /// <summary>
    /// Returns recent processing log entries (last 100).
    /// </summary>
    Task<List<GrainStates.MessageLogEntry>> GetRecentLogAsync();

    /// <summary>
    /// Clears completed/errored messages from the queue and trims log.
    /// Mirrors VistA's LA7PURG.m queue purge routine.
    /// </summary>
    Task PurgeCompletedAsync();
}
