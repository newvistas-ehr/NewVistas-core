// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace NewVistas.Abstractions.Helpers.HL7v2;

/// <summary>
/// HL7v2 message builder for outbound messages. Constructs HL7 messages for:
/// - ACK (acknowledgment) — sent back to instruments after receiving ORU/ORM
/// - ORM^O01 (order message) — downloading work lists to instruments
///
/// Mirrors VistA routines:
/// - LA7UID.m — downloads work lists to instruments
/// - LA7HDR.m / LA7HDR1.m — MSH header construction
/// - LA7VORM.m — ORM order message construction
/// </summary>
public static class HL7Builder
{
    private const char FS = '|';   // Field separator
    private const string EC = @"^~\&"; // Encoding characters
    private const string CR = "\r";    // HL7 segment terminator

    /// <summary>
    /// Builds an ACK (acknowledgment) message in response to a received message.
    /// Mirrors VistA's acknowledgment generation in LA7VIN1.m.
    /// </summary>
    /// <param name="originalControlId">MSH-10 from the received message</param>
    /// <param name="ackCode">AA=Accept, AE=Error, AR=Reject</param>
    /// <param name="sendingApp">Our application name</param>
    /// <param name="sendingFacility">Our facility name</param>
    /// <param name="receivingApp">Instrument's application name</param>
    /// <param name="receivingFacility">Instrument's facility name</param>
    /// <param name="errorMessage">Optional error text for AE/AR responses</param>
    public static string BuildACK(
        string originalControlId,
        string ackCode = "AA",
        string sendingApp = "NEWVISTAS",
        string sendingFacility = "LAB",
        string? receivingApp = null,
        string? receivingFacility = null,
        string? errorMessage = null)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string controlId = $"ACK-{Guid.NewGuid():N}"[..20];

        string msh = $"MSH{FS}{EC}{FS}" +
                     $"{sendingApp}{FS}" +
                     $"{sendingFacility}{FS}" +
                     $"{receivingApp ?? ""}{FS}" +
                     $"{receivingFacility ?? ""}{FS}" +
                     $"{timestamp}{FS}" +
                     $"{FS}" +    // MSH-8: Security
                     $"ACK{FS}" + // MSH-9: Message Type
                     $"{controlId}{FS}" +
                     $"P{FS}" +   // MSH-11: Processing ID (P=Production)
                     $"2.5.1";    // MSH-12: Version

        string msa = $"MSA{FS}{ackCode}{FS}{originalControlId}";

        if (!string.IsNullOrEmpty(errorMessage))
        {
            string err = $"ERR{FS}{FS}{FS}{FS}{errorMessage}";
            return $"{msh}{CR}{msa}{CR}{err}{CR}";
        }

        return $"{msh}{CR}{msa}{CR}";
    }

    /// <summary>
    /// Builds an ORM^O01 (Order) message for downloading a lab order to an instrument.
    /// Mirrors VistA's LA7UID.m download and LA7VORM.m order construction.
    /// </summary>
    /// <param name="patientId">Patient identifier</param>
    /// <param name="patientName">Patient name (family^given)</param>
    /// <param name="dateOfBirth">Patient DOB</param>
    /// <param name="sex">Patient sex (M/F)</param>
    /// <param name="orderId">Placer order number</param>
    /// <param name="testCode">Universal service ID (LOINC or NLT code)</param>
    /// <param name="testName">Test name</param>
    /// <param name="specimenSource">Specimen source description</param>
    /// <param name="collectionDateTime">Requested collection date/time</param>
    /// <param name="sendingApp">Our application name</param>
    /// <param name="sendingFacility">Our facility name</param>
    /// <param name="receivingApp">Instrument's application name</param>
    /// <param name="receivingFacility">Instrument's facility name</param>
    public static string BuildORMOrder(
        string patientId,
        string patientName,
        string? dateOfBirth,
        string? sex,
        string orderId,
        string testCode,
        string testName,
        string? specimenSource,
        DateTime? collectionDateTime,
        string sendingApp = "NEWVISTAS",
        string sendingFacility = "LAB",
        string? receivingApp = null,
        string? receivingFacility = null)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string controlId = $"ORM-{Guid.NewGuid():N}"[..20];

        // MSH segment
        string msh = $"MSH{FS}{EC}{FS}" +
                     $"{sendingApp}{FS}" +
                     $"{sendingFacility}{FS}" +
                     $"{receivingApp ?? ""}{FS}" +
                     $"{receivingFacility ?? ""}{FS}" +
                     $"{timestamp}{FS}" +
                     $"{FS}" +
                     $"ORM^O01{FS}" +
                     $"{controlId}{FS}" +
                     $"P{FS}" +
                     $"2.5.1";

        // PID segment
        string dob = dateOfBirth ?? "";
        string pid = $"PID{FS}" +
                     $"1{FS}" +        // PID-1: Set ID
                     $"{FS}" +          // PID-2: External ID
                     $"{patientId}{FS}" + // PID-3: Patient ID
                     $"{FS}" +          // PID-4: Alternate ID
                     $"{patientName}{FS}" + // PID-5: Patient Name
                     $"{FS}" +          // PID-6: Mother's Maiden
                     $"{dob}{FS}" +     // PID-7: DOB
                     $"{sex ?? ""}";    // PID-8: Sex

        // ORC segment (Common Order)
        string orc = $"ORC{FS}" +
                     $"NW{FS}" +        // ORC-1: Order Control (NW=New Order)
                     $"{orderId}";       // ORC-2: Placer Order Number

        // OBR segment (Observation Request)
        string collDt = collectionDateTime?.ToString("yyyyMMddHHmmss") ?? "";
        string obr = $"OBR{FS}" +
                     $"1{FS}" +          // OBR-1: Set ID
                     $"{orderId}{FS}" +  // OBR-2: Placer Order Number
                     $"{FS}" +           // OBR-3: Filler Order Number
                     $"{testCode}^{testName}{FS}" + // OBR-4: Universal Service ID
                     $"{FS}" +           // OBR-5: Priority
                     $"{collDt}{FS}" +   // OBR-6: Requested Date/Time
                     $"{FS}" +           // OBR-7: Observation Date/Time
                     $"{FS}" +           // OBR-8: Observation End
                     $"{FS}" +           // OBR-9: Collection Volume
                     $"{FS}" +           // OBR-10: Collector ID
                     $"{FS}" +           // OBR-11: Specimen Action Code
                     $"{FS}" +           // OBR-12: Danger Code
                     $"{FS}" +           // OBR-13: Relevant Clinical Info
                     $"{FS}" +           // OBR-14: Specimen Received Date
                     $"{specimenSource ?? ""}"; // OBR-15: Specimen Source

        return $"{msh}{CR}{pid}{CR}{orc}{CR}{obr}{CR}";
    }

    /// <summary>
    /// Wraps an HL7 message in MLLP (Minimal Lower Layer Protocol) framing.
    /// MLLP is the standard transport protocol for HL7 over TCP/IP.
    /// Prefix: \x0B (VT), Suffix: \x1C\x0D (FS + CR)
    /// </summary>
    public static string WrapMllp(string hl7Message)
        => $"\x0B{hl7Message}\x1C\x0D";
}
