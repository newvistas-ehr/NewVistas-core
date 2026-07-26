// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for transmitting a medical/procedure prior-authorization request electronically. The US
/// standards are the <b>X12 278</b> health-care services review transaction and the FHIR <b>Da Vinci
/// PAS</b> (Prior Authorization Support) profile, routed to the payer via a clearinghouse.
///
/// Like <c>IOutboundPrescriptionTransmitter</c>, the offline default
/// (<see cref="NullProcedurePriorAuthTransmitter"/>) does not touch the network, so the system runs
/// fully offline out of the box — and, importantly, so the <b>manual</b> submission channels
/// (portal / phone / fax) are the first-class path. A real implementation is a deferred, config-gated
/// enhancement; a payer that only accepts a portal or a phone call is handled by NOT transmitting and
/// tracking the manual submission instead.
/// </summary>
public interface IProcedurePriorAuthTransmitter
{
    /// <summary>True when a live 278 / Da Vinci PAS connection is configured. The offline default is false.</summary>
    bool IsEnabled { get; }

    /// <summary>Transmit a procedure prior-auth request to the payer and report the outcome.</summary>
    Task<ProcedurePaTransmissionResult> SubmitAsync(ProcedurePaRequestMessage message, CancellationToken cancellationToken = default);
}

/// <summary>The data a procedure prior-auth submission carries — enough to represent an X12 278 request.</summary>
public sealed record ProcedurePaRequestMessage
{
    public required string ProcAuthId { get; init; }
    public required string PatientId { get; init; }
    public required string CptCode { get; init; }
    public required string PayerId { get; init; }
    public IReadOnlyList<string> DiagnosisCodes { get; init; } = new List<string>();
    public string? ClinicalJustification { get; init; }
    public DateTime? ServiceStartDate { get; init; }
    public DateTime? ServiceEndDate { get; init; }
}

/// <summary>Outcome of a transmission attempt.</summary>
/// <param name="Transmitted">True if the request was actually sent.</param>
/// <param name="Status">Short status code, e.g. "TRANSMITTED" / "NOT_TRANSMITTED" / "ERROR".</param>
/// <param name="AuthReferenceId">The tracking/reference id assigned by the payer/clearinghouse, if sent.</param>
/// <param name="Detail">Human-readable detail (recorded on the PA for audit).</param>
public sealed record ProcedurePaTransmissionResult(bool Transmitted, string Status, string? AuthReferenceId, string Detail);

/// <summary>
/// Offline default: never touches the network. Reports the feature disabled and returns a result
/// describing the 278 that <i>would</i> be sent, so the PA is recorded and the demo is honest that
/// nothing left the building (there is no clearinghouse connection).
/// </summary>
public sealed class NullProcedurePriorAuthTransmitter : IProcedurePriorAuthTransmitter
{
    /// <inheritdoc/>
    public bool IsEnabled => false;

    /// <inheritdoc/>
    public Task<ProcedurePaTransmissionResult> SubmitAsync(ProcedurePaRequestMessage message, CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcedurePaTransmissionResult(
            Transmitted: false,
            Status: "NOT_TRANSMITTED",
            AuthReferenceId: null,
            Detail: $"X12 278 / Da Vinci PAS is not configured (offline). A prior-auth request for CPT " +
                    $"{message.CptCode} would be transmitted to payer {message.PayerId} via a clearinghouse."));
}
