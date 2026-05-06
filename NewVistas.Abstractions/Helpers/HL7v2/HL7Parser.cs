// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.Helpers.HL7v2;

/// <summary>
/// HL7v2 message parser. Converts raw HL7 text into structured <see cref="HL7Message"/> objects.
///
/// Mirrors the parsing logic in VistA routines:
/// - LA7VIN1.m NXTMSG — iterates segments, validates segment type format
/// - LA7VHLU2.m GETSEG — extracts individual segments
/// - LA7VOBX.m / LA7VOBR.m — field-level extraction for OBX/OBR
///
/// Handles standard HL7v2 delimiters: | (field), ^ (component), ~ (repetition),
/// \ (escape), &amp; (subcomponent). MSH segment gets special treatment since
/// the field separator character is defined in MSH-1.
/// </summary>
public static class HL7Parser
{
    /// <summary>
    /// Parses a raw HL7v2 message string into a structured message.
    /// Supports both \r and \r\n line endings (HL7 standard uses \r).
    /// </summary>
    public static HL7Message Parse(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("HL7 message cannot be empty.", nameof(rawMessage));

        // Strip MLLP framing if present (\x0B prefix, \x1C\x0D suffix)
        rawMessage = StripMllpFraming(rawMessage);

        HL7Message msg = new() { RawMessage = rawMessage };

        // Split into segments — HL7 uses \r, but accept \r\n and \n
        string[] lines = rawMessage.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length == 0)
            throw new ArgumentException("HL7 message contains no segments.", nameof(rawMessage));

        // First segment must be MSH
        if (!lines[0].StartsWith("MSH"))
            throw new ArgumentException("HL7 message must begin with MSH segment.", nameof(rawMessage));

        // Extract field separator from MSH-1 (character at position 3)
        msg.FieldSeparator = lines[0][3];

        // Extract encoding characters from MSH-2 (positions 4-7)
        int firstFieldEnd = lines[0].IndexOf(msg.FieldSeparator, 4);
        if (firstFieldEnd > 4)
            msg.EncodingCharacters = lines[0][4..firstFieldEnd];
        else if (lines[0].Length >= 8)
            msg.EncodingCharacters = lines[0][4..8];

        // Parse each segment
        foreach (string line in lines)
        {
            HL7Segment segment = ParseSegment(line, msg.FieldSeparator);
            msg.Segments.Add(segment);
        }

        // Extract MSH header fields
        ParseMshHeader(msg);

        return msg;
    }

    /// <summary>
    /// Parses a single segment line into an HL7Segment.
    /// MSH segments receive special handling — MSH-1 is the field separator itself.
    /// </summary>
    private static HL7Segment ParseSegment(string line, char fieldSeparator)
    {
        HL7Segment segment = new();

        if (line.StartsWith("MSH"))
        {
            // MSH is special: field 1 IS the separator, field 2 starts at position 4
            segment.SegmentType = "MSH";
            segment.Fields.Add("MSH");                           // [0] = segment type
            segment.Fields.Add(fieldSeparator.ToString());       // [1] = field separator (MSH-1)

            // Split remaining from position 4 onward
            string rest = line.Length > 4 ? line[4..] : string.Empty;
            string[] fields = rest.Split(fieldSeparator);
            foreach (string f in fields)
                segment.Fields.Add(f);
        }
        else
        {
            string[] parts = line.Split(fieldSeparator);
            segment.SegmentType = parts.Length > 0 ? parts[0] : string.Empty;
            foreach (string p in parts)
                segment.Fields.Add(p);
        }

        return segment;
    }

    /// <summary>
    /// Extracts common MSH header fields into the message object.
    /// </summary>
    private static void ParseMshHeader(HL7Message msg)
    {
        HL7Segment? msh = msg.GetSegment("MSH");
        if (msh == null) return;

        // MSH field numbering: [0]=type, [1]=separator, [2]=encoding chars,
        // [3]=sending app, [4]=sending facility, [5]=receiving app,
        // [6]=receiving facility, [7]=datetime, [8]=security, [9]=message type,
        // [10]=control id, [11]=processing id, [12]=version
        msg.SendingApplication = msh.GetField(3);
        msg.SendingFacility = msh.GetField(4);
        msg.ReceivingApplication = msh.GetField(5);
        msg.ReceivingFacility = msh.GetField(6);
        msg.MessageDateTime = msh.GetField(7);

        // MSH-9 has components: type^trigger^structure
        string msgType = msh.GetField(9);
        if (!string.IsNullOrEmpty(msgType))
        {
            string[] components = msgType.Split(msg.ComponentSeparator);
            msg.MessageType = components.Length > 0 ? components[0] : null;
            msg.TriggerEvent = components.Length > 1 ? components[1] : null;
        }

        msg.MessageControlId = msh.GetField(10);
        msg.VersionId = msh.GetField(12);
    }

    /// <summary>
    /// Extracts patient identification from a PID segment.
    /// Mirrors VistA's UPID^LA7VHLU1 patient lookup.
    /// </summary>
    public static HL7PatientId? ExtractPatientId(HL7Message msg)
    {
        HL7Segment? pid = msg.GetSegment("PID");
        if (pid == null) return null;

        char cs = msg.ComponentSeparator;
        HL7PatientId patient = new();

        // PID-3: Patient Identifier List
        string pid3 = pid.GetField(3);
        if (!string.IsNullOrEmpty(pid3))
        {
            string[] components = pid3.Split(cs);
            patient.PatientId = components.Length > 0 ? components[0] : string.Empty;
            patient.AssigningAuthority = components.Length > 3 ? components[3] : null;
        }

        // PID-5: Patient Name (family^given^middle^suffix^prefix)
        string pid5 = pid.GetField(5);
        if (!string.IsNullOrEmpty(pid5))
        {
            string[] nameComponents = pid5.Split(cs);
            patient.FamilyName = nameComponents.Length > 0 ? nameComponents[0] : null;
            patient.GivenName = nameComponents.Length > 1 ? nameComponents[1] : null;
        }

        // PID-7: Date of Birth
        patient.DateOfBirth = pid.GetField(7);

        // PID-8: Sex
        patient.Sex = pid.GetField(8);

        // PID-19: SSN
        patient.Ssn = pid.GetField(19);

        return patient;
    }

    /// <summary>
    /// Extracts observation requests from all OBR segments.
    /// Mirrors VistA's LA7VOBR.m segment processing.
    /// </summary>
    public static List<HL7ObservationRequest> ExtractObservationRequests(HL7Message msg)
    {
        List<HL7ObservationRequest> results = new();
        char cs = msg.ComponentSeparator;

        foreach (HL7Segment obr in msg.ObrSegments)
        {
            HL7ObservationRequest req = new();

            // OBR-1: Set ID
            if (int.TryParse(obr.GetField(1), out int setId))
                req.SetId = setId;

            // OBR-2: Placer Order Number
            req.PlacerOrderNumber = obr.GetField(2);

            // OBR-3: Filler Order Number
            req.FillerOrderNumber = obr.GetField(3);

            // OBR-4: Universal Service ID (code^text^coding system)
            string obr4 = obr.GetField(4);
            if (!string.IsNullOrEmpty(obr4))
            {
                string[] components = obr4.Split(cs);
                req.UniversalServiceId = components.Length > 0 ? components[0] : null;
                req.UniversalServiceText = components.Length > 1 ? components[1] : null;
                req.UniversalServiceCodingSystem = components.Length > 2 ? components[2] : null;
            }

            // OBR-7: Observation Date/Time
            req.ObservationDateTime = obr.GetField(7);

            // OBR-14: Specimen Received Date/Time
            req.SpecimenReceivedDateTime = obr.GetField(14);

            // OBR-15: Specimen Source
            req.SpecimenSource = obr.GetField(15);

            // OBR-22: Results Rpt/Status Change
            req.ResultsChangeDateTime = obr.GetField(22);

            // OBR-25: Result Status
            req.ResultStatus = obr.GetField(25);

            results.Add(req);
        }

        return results;
    }

    /// <summary>
    /// Extracts observation results from all OBX segments.
    /// Mirrors VistA's LA7VOBX.m OBX segment processing and LA7VIN7.m validation.
    /// </summary>
    public static List<HL7ObservationResult> ExtractObservationResults(HL7Message msg)
    {
        List<HL7ObservationResult> results = new();
        char cs = msg.ComponentSeparator;

        foreach (HL7Segment obx in msg.ObxSegments)
        {
            HL7ObservationResult obs = new();

            // OBX-1: Set ID
            if (int.TryParse(obx.GetField(1), out int setId))
                obs.SetId = setId;

            // OBX-2: Value Type
            obs.ValueType = obx.GetField(2);

            // OBX-3: Observation Identifier (code^text^coding system)
            string obx3 = obx.GetField(3);
            if (!string.IsNullOrEmpty(obx3))
            {
                string[] components = obx3.Split(cs);
                obs.ObservationId = components.Length > 0 ? components[0] : null;
                obs.ObservationText = components.Length > 1 ? components[1] : null;
                obs.ObservationCodingSystem = components.Length > 2 ? components[2] : null;
            }

            // OBX-4: Observation Sub-ID
            obs.ObservationSubId = obx.GetField(4);

            // OBX-5: Observation Value
            obs.ObservationValue = obx.GetField(5);

            // OBX-6: Units (code^text^coding system) — take first component
            string obx6 = obx.GetField(6);
            if (!string.IsNullOrEmpty(obx6))
                obs.Units = obx6.Split(cs)[0];

            // OBX-7: Reference Range
            obs.ReferenceRange = obx.GetField(7);

            // OBX-8: Abnormal Flags
            obs.AbnormalFlags = obx.GetField(8);

            // OBX-11: Observation Result Status
            obs.ResultStatus = obx.GetField(11);

            // OBX-14: Date/Time of Observation
            obs.ObservationDateTime = obx.GetField(14);

            // OBX-18: Equipment Instance Identifier
            obs.EquipmentId = obx.GetField(18);

            results.Add(obs);
        }

        return results;
    }

    /// <summary>
    /// Strips MLLP (Minimal Lower Layer Protocol) framing characters.
    /// MLLP wraps HL7 messages with \x0B (VT) prefix and \x1C\x0D (FS+CR) suffix.
    /// </summary>
    private static string StripMllpFraming(string message)
    {
        // Remove leading VT (\x0B)
        if (message.Length > 0 && message[0] == '\x0B')
            message = message[1..];

        // Remove trailing FS+CR (\x1C\x0D)
        if (message.Length >= 2 && message[^2] == '\x1C' && message[^1] == '\x0D')
            message = message[..^2];
        else if (message.Length >= 1 && message[^1] == '\x1C')
            message = message[..^1];

        return message;
    }
}
