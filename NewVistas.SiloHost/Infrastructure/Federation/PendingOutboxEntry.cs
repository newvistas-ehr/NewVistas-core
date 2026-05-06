// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// A single unsent row claimed by the drainer for shipping. Carries just enough
/// for the drainer to deserialize the envelope and ack/retry by EventId.
/// </summary>
public sealed record PendingOutboxEntry(string EventId, byte[] EnvelopeBlob, int Attempts);
