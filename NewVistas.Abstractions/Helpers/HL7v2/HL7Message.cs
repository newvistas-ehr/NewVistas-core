// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.Helpers.HL7v2;

/// <summary>
/// Represents a parsed HL7v2 message. Mirrors the message structure processed
/// by VistA routines LA7VIN1.m (NXTMSG) and LA7VHLU2.m (GETSEG).
///
/// HL7v2 messages consist of segments (one per line), each containing fields
/// separated by | (pipe). Fields may contain components (^), subcomponents (&amp;),
/// and repetitions (~).
/// </summary>
public class HL7Message
{
    /// <summary>
    /// Raw message text before parsing
    /// </summary>
    public string RawMessage { get; set; } = string.Empty;

    /// <summary>
    /// All segments in order
    /// </summary>
    public List<HL7Segment> Segments { get; set; } = new();

    /// <summary>
    /// Field separator character (default: |)
    /// </summary>
    public char FieldSeparator { get; set; } = '|';

    /// <summary>
    /// Encoding characters (default: ^~\&amp;)
    /// </summary>
    public string EncodingCharacters { get; set; } = @"^~\&";

    /// <summary>
    /// Component separator (default: ^)
    /// </summary>
    public char ComponentSeparator => EncodingCharacters.Length > 0 ? EncodingCharacters[0] : '^';

    /// <summary>
    /// Repetition separator (default: ~)
    /// </summary>
    public char RepetitionSeparator => EncodingCharacters.Length > 1 ? EncodingCharacters[1] : '~';

    /// <summary>
    /// Escape character (default: \)
    /// </summary>
    public char EscapeCharacter => EncodingCharacters.Length > 2 ? EncodingCharacters[2] : '\\';

    /// <summary>
    /// Subcomponent separator (default: &amp;)
    /// </summary>
    public char SubcomponentSeparator => EncodingCharacters.Length > 3 ? EncodingCharacters[3] : '&';

    // ── Parsed header (MSH) fields ──

    /// <summary>MSH-3: Sending Application</summary>
    public string? SendingApplication { get; set; }

    /// <summary>MSH-4: Sending Facility</summary>
    public string? SendingFacility { get; set; }

    /// <summary>MSH-5: Receiving Application</summary>
    public string? ReceivingApplication { get; set; }

    /// <summary>MSH-6: Receiving Facility</summary>
    public string? ReceivingFacility { get; set; }

    /// <summary>MSH-7: Message Date/Time</summary>
    public string? MessageDateTime { get; set; }

    /// <summary>MSH-9.1: Message Type (e.g., "ORU")</summary>
    public string? MessageType { get; set; }

    /// <summary>MSH-9.2: Trigger Event (e.g., "R01")</summary>
    public string? TriggerEvent { get; set; }

    /// <summary>MSH-10: Message Control ID</summary>
    public string? MessageControlId { get; set; }

    /// <summary>MSH-12: Version ID (e.g., "2.5.1")</summary>
    public string? VersionId { get; set; }

    // ── Convenience accessors ──

    /// <summary>
    /// Gets the first segment of a given type.
    /// </summary>
    public HL7Segment? GetSegment(string segmentType)
        => Segments.FirstOrDefault(s => s.SegmentType == segmentType);

    /// <summary>
    /// Gets all segments of a given type.
    /// </summary>
    public List<HL7Segment> GetSegments(string segmentType)
        => Segments.Where(s => s.SegmentType == segmentType).ToList();

    /// <summary>
    /// Returns all PID segments (patient identification).
    /// </summary>
    public List<HL7Segment> PidSegments => GetSegments("PID");

    /// <summary>
    /// Returns all OBR segments (observation request).
    /// </summary>
    public List<HL7Segment> ObrSegments => GetSegments("OBR");

    /// <summary>
    /// Returns all OBX segments (observation result).
    /// </summary>
    public List<HL7Segment> ObxSegments => GetSegments("OBX");

    /// <summary>
    /// Returns all ORC segments (common order).
    /// </summary>
    public List<HL7Segment> OrcSegments => GetSegments("ORC");
}

/// <summary>
/// Represents a single HL7v2 segment (one line of the message).
/// </summary>
public class HL7Segment
{
    /// <summary>
    /// Segment type identifier (first 3 chars, e.g., "MSH", "PID", "OBR", "OBX")
    /// </summary>
    public string SegmentType { get; set; } = string.Empty;

    /// <summary>
    /// All fields in this segment (index 0 = segment type for non-MSH, or separator for MSH)
    /// </summary>
    public List<string> Fields { get; set; } = new();

    /// <summary>
    /// Gets a field value by 1-based HL7 field number.
    /// Returns empty string if field does not exist.
    /// </summary>
    public string GetField(int fieldNumber)
    {
        if (fieldNumber < 0 || fieldNumber >= Fields.Count)
            return string.Empty;
        return Fields[fieldNumber] ?? string.Empty;
    }

    /// <summary>
    /// Gets a component within a field (1-based component number within 1-based field).
    /// Components are separated by ^ within a field.
    /// </summary>
    public string GetComponent(int fieldNumber, int componentNumber, char componentSeparator = '^')
    {
        string field = GetField(fieldNumber);
        if (string.IsNullOrEmpty(field)) return string.Empty;

        string[] components = field.Split(componentSeparator);
        int idx = componentNumber - 1;
        if (idx < 0 || idx >= components.Length) return string.Empty;
        return components[idx];
    }
}

/// <summary>
/// Parsed patient identification from a PID segment.
/// </summary>
public class HL7PatientId
{
    /// <summary>PID-3: Patient Identifier List (first component = ID)</summary>
    public string PatientId { get; set; } = string.Empty;

    /// <summary>PID-3: Assigning Authority (component 4)</summary>
    public string? AssigningAuthority { get; set; }

    /// <summary>PID-5: Patient Name — Family</summary>
    public string? FamilyName { get; set; }

    /// <summary>PID-5: Patient Name — Given</summary>
    public string? GivenName { get; set; }

    /// <summary>PID-7: Date of Birth</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>PID-8: Sex</summary>
    public string? Sex { get; set; }

    /// <summary>PID-19: SSN</summary>
    public string? Ssn { get; set; }
}

/// <summary>
/// Parsed observation request from an OBR segment.
/// </summary>
public class HL7ObservationRequest
{
    /// <summary>OBR-1: Set ID</summary>
    public int SetId { get; set; }

    /// <summary>OBR-2: Placer Order Number</summary>
    public string? PlacerOrderNumber { get; set; }

    /// <summary>OBR-3: Filler Order Number</summary>
    public string? FillerOrderNumber { get; set; }

    /// <summary>OBR-4.1: Universal Service ID — identifier (NLT code or LOINC)</summary>
    public string? UniversalServiceId { get; set; }

    /// <summary>OBR-4.2: Universal Service ID — text (test name)</summary>
    public string? UniversalServiceText { get; set; }

    /// <summary>OBR-4.3: Universal Service ID — coding system</summary>
    public string? UniversalServiceCodingSystem { get; set; }

    /// <summary>OBR-7: Observation Date/Time</summary>
    public string? ObservationDateTime { get; set; }

    /// <summary>OBR-14: Specimen Received Date/Time</summary>
    public string? SpecimenReceivedDateTime { get; set; }

    /// <summary>OBR-15: Specimen Source</summary>
    public string? SpecimenSource { get; set; }

    /// <summary>OBR-22: Results Rpt/Status Change Date/Time</summary>
    public string? ResultsChangeDateTime { get; set; }

    /// <summary>OBR-25: Result Status (F=Final, P=Preliminary, C=Correction)</summary>
    public string? ResultStatus { get; set; }
}

/// <summary>
/// Parsed observation result from an OBX segment.
/// </summary>
public class HL7ObservationResult
{
    /// <summary>OBX-1: Set ID</summary>
    public int SetId { get; set; }

    /// <summary>OBX-2: Value Type (NM=Numeric, ST=String, CE=Coded Entry, TX=Text)</summary>
    public string? ValueType { get; set; }

    /// <summary>OBX-3.1: Observation Identifier — code</summary>
    public string? ObservationId { get; set; }

    /// <summary>OBX-3.2: Observation Identifier — text (test name)</summary>
    public string? ObservationText { get; set; }

    /// <summary>OBX-3.3: Observation Identifier — coding system (LN=LOINC, 99VA64=VistA)</summary>
    public string? ObservationCodingSystem { get; set; }

    /// <summary>OBX-4: Observation Sub-ID</summary>
    public string? ObservationSubId { get; set; }

    /// <summary>OBX-5: Observation Value</summary>
    public string? ObservationValue { get; set; }

    /// <summary>OBX-6: Units (e.g., "mg/dL", "K/cmm")</summary>
    public string? Units { get; set; }

    /// <summary>OBX-7: Reference Range (e.g., "4.5-11.0")</summary>
    public string? ReferenceRange { get; set; }

    /// <summary>OBX-8: Abnormal Flags (H=High, L=Low, HH=Critical High, LL=Critical Low, N=Normal)</summary>
    public string? AbnormalFlags { get; set; }

    /// <summary>OBX-11: Observation Result Status (F=Final, P=Preliminary, C=Corrected, X=Cancelled)</summary>
    public string? ResultStatus { get; set; }

    /// <summary>OBX-14: Date/Time of the Observation</summary>
    public string? ObservationDateTime { get; set; }

    /// <summary>OBX-18: Equipment Instance Identifier</summary>
    public string? EquipmentId { get; set; }
}
