// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// A single unsent row claimed by the drainer for shipping. Carries just enough
/// for the drainer to deserialize the envelope and ack/retry by EventId.
/// </summary>
public sealed record PendingOutboxEntry(string EventId, byte[] EnvelopeBlob, int Attempts);
