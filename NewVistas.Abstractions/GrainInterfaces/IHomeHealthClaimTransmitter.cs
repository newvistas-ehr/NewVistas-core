// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Seam for transmitting Medicare home-health billing to the external payer pipeline (MAC / iQIES
/// / clearinghouse via the X12 837 claim and the NOA transaction). A real deployment registers an
/// implementation; when none is registered the billing grain records a stand-in control number so
/// the workflow is exercisable end-to-end without an external connection. Parallels the NCPDP
/// SCRIPT e-prescribing transmitter seam.
/// </summary>
public interface IHomeHealthClaimTransmitter
{
    /// <summary>Transmits a Notice of Admission; returns the payer/clearinghouse control number.</summary>
    Task<string> TransmitNoaAsync(string episodeId, string patientId, DateTime admissionDate);

    /// <summary>Transmits a 30-day-period claim (by HIPPS code); returns the control number.</summary>
    Task<string> TransmitClaimAsync(string episodeId, string hippsCode);
}
