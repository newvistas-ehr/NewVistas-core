// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for transmitting an outpatient prescription to an external pharmacy. The US standard is
/// <b>NCPDP SCRIPT</b> (NewRx / RxRenewal / RxChange / CancelRx) routed over the <b>Surescripts</b>
/// network; the destination pharmacy is identified by its NCPDP Provider ID. The protocol is the
/// same for every chain (CVS / Walgreens / independent / mail-order), so the transmitter only
/// needs the message + the pharmacy's NCPDP id.
///
/// Like <c>IRxNavDoseFormClient</c>, the offline default (<see cref="NullOutboundPrescriptionTransmitter"/>)
/// does not touch the network, so the system runs fully offline out of the box. A real implementation
/// (e.g. <c>SurescriptsPrescriptionTransmitter</c>) is a deferred, config-gated enhancement that
/// builds the actual SCRIPT NewRx and sends it.
/// </summary>
public interface IOutboundPrescriptionTransmitter
{
    /// <summary>True when a live e-prescribing connection is configured. The offline default is false.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Transmit a new prescription (NCPDP SCRIPT NewRx) to the destination pharmacy and report the outcome.
    /// </summary>
    Task<PrescriptionTransmissionResult> TransmitNewRxAsync(NewRxMessage message, CancellationToken cancellationToken = default);
}

/// <summary>The data a NewRx carries — enough to represent an NCPDP SCRIPT new-prescription message.</summary>
public sealed record NewRxMessage
{
    public required string PrescriptionId { get; init; }
    public required string PatientId { get; init; }
    public required string DrugName { get; init; }
    public string? Sig { get; init; }
    public int? Quantity { get; init; }
    public int? DaysSupply { get; init; }
    public int? Refills { get; init; }
    public required string PrescriberName { get; init; }
    public required string PharmacyName { get; init; }

    /// <summary>Destination pharmacy NCPDP Provider ID — the SCRIPT routing address.</summary>
    public string? PharmacyNcpdpId { get; init; }
}

/// <summary>Outcome of a transmission attempt.</summary>
/// <param name="Transmitted">True if the message was actually sent.</param>
/// <param name="Status">Short status code, e.g. "TRANSMITTED" / "NOT_TRANSMITTED" / "ERROR".</param>
/// <param name="MessageId">The SCRIPT message id assigned by the network, if sent.</param>
/// <param name="Detail">Human-readable detail (recorded on the prescription for audit).</param>
public sealed record PrescriptionTransmissionResult(bool Transmitted, string Status, string? MessageId, string Detail);

/// <summary>
/// Offline default: never touches the network. Reports the feature disabled and returns a result
/// describing the NewRx that <i>would</i> be routed, so the prescription is created and the demo
/// is honest that nothing was actually sent (there is no Surescripts connection).
/// </summary>
public sealed class NullOutboundPrescriptionTransmitter : IOutboundPrescriptionTransmitter
{
    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<PrescriptionTransmissionResult> TransmitNewRxAsync(NewRxMessage message, CancellationToken cancellationToken = default)
        => Task.FromResult(new PrescriptionTransmissionResult(
            Transmitted: false,
            Status: "NOT_TRANSMITTED",
            MessageId: null,
            Detail: $"E-prescribing is offline (no Surescripts connection). A NewRx for \"{message.DrugName}\" " +
                    $"would be routed to {message.PharmacyName}" +
                    (string.IsNullOrEmpty(message.PharmacyNcpdpId) ? "" : $" (NCPDP {message.PharmacyNcpdpId})") +
                    " via NCPDP SCRIPT over Surescripts."));
}
