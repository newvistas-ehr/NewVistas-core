// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Instrument Message Queue grain based on VistA
/// LA7 MESSAGE QUEUE file (#62.49). Each grain manages the inbound
/// message queue for one instrument.
/// </summary>
[GenerateSerializer]
public class InstrumentMessageQueueState
{
    /// <summary>
    /// Instrument ID this queue belongs to
    /// </summary>
    [Id(0)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>
    /// Incoming message queue — FIFO order.
    /// Mirrors VistA ^LAHM(62.49,"Q",configIEN,"IQ") incoming queue.
    /// </summary>
    [Id(1)]
    public List<QueuedHL7Message> IncomingQueue { get; set; } = new();

    /// <summary>
    /// Total messages successfully processed (lifetime counter)
    /// </summary>
    [Id(2)]
    public long ProcessedCount { get; set; }

    /// <summary>
    /// Total processing errors (lifetime counter)
    /// </summary>
    [Id(3)]
    public long ErrorCount { get; set; }

    /// <summary>
    /// Timestamp of last successfully processed message
    /// </summary>
    [Id(4)]
    public DateTime? LastProcessedDate { get; set; }

    /// <summary>
    /// Recent processing log entries (kept to last 100)
    /// </summary>
    [Id(5)]
    public List<MessageLogEntry> RecentLog { get; set; } = new();
}

/// <summary>
/// An HL7 message queued for processing.
/// Mirrors a single entry in VistA's ^LAHM(62.49,IEN) incoming queue.
/// </summary>
[GenerateSerializer]
public class QueuedHL7Message
{
    /// <summary>
    /// Unique message ID
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Raw HL7 message content
    /// </summary>
    [Id(1)]
    public string RawContent { get; set; } = string.Empty;

    /// <summary>
    /// When the message was received
    /// </summary>
    [Id(2)]
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current processing status
    /// </summary>
    [Id(3)]
    public MessageProcessingStatus Status { get; set; } = MessageProcessingStatus.QUEUED;

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    [Id(4)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// HL7 Message Type extracted from MSH-9 (e.g., "ORU", "ORM")
    /// </summary>
    [Id(5)]
    public string? MessageType { get; set; }

    /// <summary>
    /// HL7 Message Control ID from MSH-10
    /// </summary>
    [Id(6)]
    public string? MessageControlId { get; set; }
}

/// <summary>
/// Processing log entry for audit trail.
/// Mirrors VistA's LA7LOG error/info logging (CREATE^LA7LOG).
/// </summary>
[GenerateSerializer]
public class MessageLogEntry
{
    /// <summary>
    /// Message ID that was processed
    /// </summary>
    [Id(0)]
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of processing
    /// </summary>
    [Id(1)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing result
    /// </summary>
    [Id(2)]
    public MessageProcessingStatus Status { get; set; }

    /// <summary>
    /// Patient ID extracted from PID segment (if applicable)
    /// </summary>
    [Id(3)]
    public string? PatientId { get; set; }

    /// <summary>
    /// Number of results ingested from this message
    /// </summary>
    [Id(4)]
    public int ResultCount { get; set; }

    /// <summary>
    /// Error or info message
    /// </summary>
    [Id(5)]
    public string? Message { get; set; }
}

/// <summary>
/// Message processing status — tracks lifecycle through the queue.
/// </summary>
[GenerateSerializer]
public enum MessageProcessingStatus
{
    QUEUED,
    PROCESSING,
    COMPLETED,
    ERROR
}
