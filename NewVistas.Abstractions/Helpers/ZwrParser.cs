// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.RegularExpressions;

namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Parses MUMPS ZWR (ZWrite / global export) format into structured ZwrRecord objects.
/// ZWR format: ^GLOBAL(subscript1,subscript2,...)="value"
/// </summary>
public static partial class ZwrParser
{
    // Matches: ^GLOBAL(subscripts)="value" or ^GLOBAL(subscripts)=value
    [GeneratedRegex(@"^\^(\w+)\((.+)\)=(.*)$")]
    private static partial Regex ZwrLineRegex();

    /// <summary>
    /// Parse a single ZWR line. Returns null if the line is not a valid ZWR entry.
    /// </summary>
    public static ZwrRecord? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('^'))
            return null;

        Match match = ZwrLineRegex().Match(line);
        if (!match.Success)
            return null;

        string global = match.Groups[1].Value;
        string subscriptsRaw = match.Groups[2].Value;
        string valueRaw = match.Groups[3].Value;

        // Strip outer quotes from value
        string value = StripQuotes(valueRaw);

        // Parse subscripts (comma-separated, but respect quoted strings)
        List<string> subscripts = ParseSubscripts(subscriptsRaw);
        if (subscripts.Count == 0)
            return null;

        var record = new ZwrRecord { Global = global };

        // Determine if the first subscript is a file number (for compound globals like ^GMR(120.8,...))
        // File numbers are typically decimal numbers like 120.8, 123, 75.1
        // IENs are always integers. If first subscript is decimal, it's a file number.
        int startIndex = 0;
        string firstSub = subscripts[0];

        if (IsFileNumber(global, firstSub))
        {
            record.FileNumber = firstSub;
            startIndex = 1;
        }

        // Next numeric subscript is the IEN
        if (startIndex >= subscripts.Count)
            return null;

        if (!long.TryParse(subscripts[startIndex], out long ien))
            return null;

        record.Ien = ien;
        startIndex++;

        // Remaining subscripts
        for (int i = startIndex; i < subscripts.Count; i++)
        {
            record.Subscripts.Add(StripQuotes(subscripts[i]));
        }

        record.Value = value;
        return record;
    }

    /// <summary>
    /// Parse all lines from a ZWR file, returning records grouped by (Global, FileNumber, IEN).
    /// </summary>
    public static Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>>
        ParseFile(string filePath)
    {
        var result = new Dictionary<(string, string?, long), List<ZwrRecord>>();

        foreach (string line in File.ReadLines(filePath))
        {
            ZwrRecord? record = ParseLine(line);
            if (record == null)
                continue;

            var key = (record.Global, record.FileNumber, record.Ien);
            if (!result.TryGetValue(key, out List<ZwrRecord>? list))
            {
                list = new List<ZwrRecord>();
                result[key] = list;
            }
            list.Add(record);
        }

        return result;
    }

    /// <summary>
    /// Convert VistA FileMan date format to DateTime.
    /// FM format: YYYMMDD.HHMMSS where YYY = year - 1700.
    /// Examples: 3250101 = 2025-01-01, 2800101 = 1980-01-01, 3250315.143022 = 2025-03-15 14:30:22.
    /// </summary>
    public static DateTime? ParseFmDate(string? fmDate)
    {
        if (string.IsNullOrWhiteSpace(fmDate))
            return null;

        string[] parts = fmDate.Split('.');
        string datePart = parts[0].Trim();
        string? timePart = parts.Length > 1 ? parts[1].Trim() : null;

        if (datePart.Length < 7)
            return null;

        if (!int.TryParse(datePart[..3], out int yyy) ||
            !int.TryParse(datePart[3..5], out int month) ||
            !int.TryParse(datePart[5..7], out int day))
            return null;

        int year = yyy + 1700;

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31)
            return null;

        int hour = 0, minute = 0, second = 0;
        if (timePart != null && timePart.Length >= 2)
        {
            int.TryParse(timePart[..2], out hour);
            if (timePart.Length >= 4)
                int.TryParse(timePart[2..4], out minute);
            if (timePart.Length >= 6)
                int.TryParse(timePart[4..6], out second);
        }

        try
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Get piece N (1-based) from a ^-delimited string.
    /// Mirrors MUMPS $PIECE(value,"^",n). Returns null if index is out of range.
    /// </summary>
    public static string? Piece(string? value, int n)
    {
        if (string.IsNullOrEmpty(value) || n < 1)
            return null;

        string[] pieces = value.Split('^');
        if (n > pieces.Length)
            return null;

        string piece = pieces[n - 1];
        return string.IsNullOrEmpty(piece) ? null : piece;
    }

    /// <summary>
    /// Determines if a subscript is a file number rather than an IEN.
    /// Compound globals like ^GMR, ^OR, ^LR, ^TIU, ^RA, ^PS, ^DGPT store
    /// the file number as the first subscript.
    /// </summary>
    private static bool IsFileNumber(string global, string subscript)
    {
        // These globals always use a file number as first subscript
        string[] compoundGlobals = ["GMR", "OR", "LR", "TIU", "RA", "PS", "DGPT"];

        if (!Array.Exists(compoundGlobals, g => g == global))
            return false;

        // File numbers can be decimal (120.8, 75.1) or integer (100, 63, 8925)
        return double.TryParse(subscript, out _);
    }

    /// <summary>
    /// Strip surrounding double quotes from a string. Handles ZWR escaped quotes ("").
    /// </summary>
    private static string StripQuotes(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            s = s[1..^1];
            // Unescape doubled quotes
            s = s.Replace("\"\"", "\"");
        }
        return s;
    }

    /// <summary>
    /// Parse comma-separated subscripts, respecting quoted string subscripts.
    /// E.g., '1,"CH",7250101' → ["1", "\"CH\"", "7250101"]
    /// </summary>
    private static List<string> ParseSubscripts(string raw)
    {
        var result = new List<string>();
        bool inQuotes = false;
        int start = 0;

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(raw[start..i].Trim());
                start = i + 1;
            }
        }

        // Last segment
        if (start < raw.Length)
            result.Add(raw[start..].Trim());

        return result;
    }
}
