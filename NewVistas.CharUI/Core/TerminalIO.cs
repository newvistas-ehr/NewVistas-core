// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.CharUI.Core;

/// <summary>
/// Static helper for all console I/O, enforcing VistA-style terminal conventions.
/// All menus and screens use this class instead of Console.* directly.
/// Color/attribute support via ANSI SGR sequences (TerminalColor).
/// </summary>
public static class TerminalIO
{
    public const int ScreenWidth = 80;

    // ── Output ──────────────────────────────────────────────────────────

    public static void Write(string text) => Console.Write(text);

    public static void WriteLine(string text = "") => Console.WriteLine(text);

    public static void WriteBlank() => Console.WriteLine();

    public static void WriteDivider(char c = '-')
        => Console.WriteLine(TerminalColor.Dim(new string(c, ScreenWidth)));

    public static void WriteHeader(string title)
    {
        WriteBlank();
        WriteLine(TerminalColor.BrightCyan($"--- {title} ---"));
        WriteBlank();
    }

    /// <summary>
    /// Writes the facility banner and patient context header.
    /// Facility name in bold, patient name in bright white, CWAD in red.
    /// </summary>
    public static void WritePatientBanner(SessionState session, PatientContext patient)
    {
        string now = DateTime.Now.ToString("MM/dd/yyyy HH:mm");
        string facility = session.FacilityName.ToUpperInvariant();
        int pad = ScreenWidth - facility.Length - now.Length;
        if (pad < 1) pad = 1;
        WriteLine(TerminalColor.Bold(facility) + new string(' ', pad) + TerminalColor.Dim(now));

        if (patient.HasPatient)
        {
            string ssn = !string.IsNullOrEmpty(patient.Ssn4)
                ? $"xxx-xx-{patient.Ssn4}" : "Unknown";
            string dob = patient.DateOfBirth?.ToString("MM/dd/yyyy") ?? "Unknown";
            string age = patient.Age.HasValue ? $"({patient.Age} yr)" : "";
            string sex = !string.IsNullOrEmpty(patient.Sex) ? patient.Sex : "";
            string cwad = !string.IsNullOrEmpty(patient.CwadFlags)
                ? $"  [{TerminalColor.CwadFlags(patient.CwadFlags)}]" : "";
            string sc = patient.IsServiceConnected
                ? TerminalColor.Green($"SC: {patient.ServiceConnectedPercent ?? 0}%") + "  " : "";
            string loc = !string.IsNullOrEmpty(patient.RoomBed)
                ? TerminalColor.Cyan($"Loc: {patient.RoomBed}") + "  " : "";

            string patientName = TerminalColor.BrightWhite($"{patient.PatientName,-20}");
            WriteLine($"Patient: {patientName} {ssn}    DOB: {dob} {age}  {sex}{cwad}");
            if (sc.Length > 0 || loc.Length > 0)
                WriteLine($"  {sc}{loc}");
        }

        WriteDivider('=');
    }

    /// <summary>
    /// Writes a columnar table. Headers in bold, data rows plain.
    /// </summary>
    public static void WriteTable(string[] headers, int[] widths, IEnumerable<string[]> rows)
    {
        // Bold header row
        string[] coloredHeaders = headers.Select(h => TerminalColor.Bold(h)).ToArray();
        WriteTableRow(coloredHeaders, widths, isHeader: true);
        string divider = string.Join("  ", widths.Select(w => new string('-', w)));
        WriteLine(TerminalColor.Dim(divider));
        foreach (string[] row in rows)
            WriteTableRow(row, widths, isHeader: false);
    }

    private static void WriteTableRow(string[] values, int[] widths, bool isHeader)
    {
        var parts = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            string val = i < values.Length ? (values[i] ?? "") : "";
            int w = i < widths.Length ? widths[i] : 10;
            if (isHeader)
            {
                // Headers include ANSI codes — pad the raw text, then wrap
                string raw = headers_StripAnsi(val);
                string padded = raw.Length > w ? raw[..w] : raw.PadRight(w);
                // Re-apply the bold from the original value
                parts[i] = TerminalColor.Bold(padded);
            }
            else
            {
                parts[i] = val.Length > w ? val[..w] : val.PadRight(w);
            }
        }
        WriteLine(string.Join("  ", parts));
    }

    /// <summary>Strip ANSI escape sequences for length calculation.</summary>
    private static string headers_StripAnsi(string text)
    {
        // Remove ESC[...m sequences
        int i = 0;
        var clean = new System.Text.StringBuilder(text.Length);
        while (i < text.Length)
        {
            if (text[i] == '\x1b' && i + 1 < text.Length && text[i + 1] == '[')
            {
                // Skip to 'm'
                i += 2;
                while (i < text.Length && text[i] != 'm') i++;
                if (i < text.Length) i++; // skip 'm'
            }
            else
            {
                clean.Append(text[i]);
                i++;
            }
        }
        return clean.ToString();
    }

    /// <summary>Writes a simple label: value pair. Label in cyan.</summary>
    public static void WriteDetail(string label, string? value, int labelWidth = 22)
    {
        string lbl = TerminalColor.Cyan(label.PadRight(labelWidth));
        WriteLine($"  {lbl}: {value ?? TerminalColor.Dim("(none)")}");
    }

    /// <summary>Writes a numbered list for selection.</summary>
    public static void WriteNumberedList<T>(IReadOnlyList<T> items, Func<T, string> formatter)
    {
        if (items.Count == 0)
        {
            WriteLine(TerminalColor.Dim("  (no items)"));
            return;
        }
        for (int i = 0; i < items.Count; i++)
            WriteLine($"  {TerminalColor.Dim($"{i + 1,3}.")} {formatter(items[i])}");
    }

    /// <summary>Writes a paged list, pausing every N items.</summary>
    public static void WritePagedList<T>(IReadOnlyList<T> items, Func<T, string> formatter, int pageSize = 20)
    {
        if (items.Count == 0)
        {
            WriteLine(TerminalColor.Dim("  (no items)"));
            return;
        }
        for (int i = 0; i < items.Count; i++)
        {
            WriteLine($"  {TerminalColor.Dim($"{i + 1,3}.")} {formatter(items[i])}");
            if ((i + 1) % pageSize == 0 && i + 1 < items.Count)
            {
                Write("Press <Enter> for more, or Q to stop... ");
                string? input = Console.ReadLine();
                if (input != null && input.Trim().Equals("Q", StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }
    }

    // ── Clinical display helpers ────────────────────────────────────────

    /// <summary>Writes an error/access-denied message in red.</summary>
    public static void WriteError(string message)
        => WriteLine(TerminalColor.Red($"  {message}"));

    /// <summary>Writes a success message in green.</summary>
    public static void WriteSuccess(string message)
        => WriteLine(TerminalColor.Green($"  {message}"));

    /// <summary>Writes a warning message in yellow.</summary>
    public static void WriteWarning(string message)
        => WriteLine(TerminalColor.Yellow($"  {message}"));

    // ── Input ───────────────────────────────────────────────────────────

    /// <summary>
    /// VistA-style prompt: "Select ACTION: Quit// "
    /// If user presses Enter without typing, returns the default value.
    /// </summary>
    public static string Prompt(string prompt, string? defaultValue = null)
    {
        if (defaultValue != null)
            Write($"{prompt}: {TerminalColor.Dim(defaultValue)}// ");
        else
            Write($"{prompt}: ");

        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
            return defaultValue ?? string.Empty;
        return input.Trim();
    }

    /// <summary>Yes/No prompt with default. Returns true for Yes.</summary>
    public static bool PromptYesNo(string prompt, bool defaultValue = true)
    {
        string def = defaultValue ? "YES" : "NO";
        string input = Prompt(prompt, def);
        if (input.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("Y", StringComparison.OrdinalIgnoreCase))
            return true;
        if (input.Equals("NO", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("N", StringComparison.OrdinalIgnoreCase))
            return false;
        return defaultValue;
    }

    /// <summary>Prompts for a numeric selection within a range. Returns null if invalid.</summary>
    public static int? PromptSelection(string prompt, int min, int max)
    {
        string input = Prompt(prompt);
        if (int.TryParse(input, out int val) && val >= min && val <= max)
            return val;
        return null;
    }

    /// <summary>
    /// VistA-style multi-line text entry. Blank line ends input.
    /// </summary>
    public static string PromptMultiline(string prompt)
    {
        WriteLine(prompt);
        WriteLine(TerminalColor.Dim("  (Enter text. Press Enter on a blank line to finish.)"));
        var lines = new List<string>();
        while (true)
        {
            Write(TerminalColor.Dim("  > "));
            string? line = Console.ReadLine();
            if (string.IsNullOrEmpty(line))
                break;
            lines.Add(line);
        }
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// VistA-style date prompt. Supports: T (today), T-N (N days ago), T+N (N days from now),
    /// and standard MM/DD/YYYY format.
    /// </summary>
    public static DateTime? PromptDate(string prompt, DateTime? defaultValue = null)
    {
        string def = defaultValue?.ToString("MM/dd/yyyy") ?? "";
        string input = Prompt(prompt, string.IsNullOrEmpty(def) ? null : def);

        if (string.IsNullOrWhiteSpace(input))
            return defaultValue;

        // VistA shorthand: T, T-7, T+30
        if (input.Equals("T", StringComparison.OrdinalIgnoreCase))
            return DateTime.Today;
        if (input.StartsWith("T-", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(input[2..], out int daysAgo))
            return DateTime.Today.AddDays(-daysAgo);
        if (input.StartsWith("T+", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(input[2..], out int daysAhead))
            return DateTime.Today.AddDays(daysAhead);

        // Standard date formats
        if (DateTime.TryParse(input, out DateTime dt))
            return dt;

        WriteWarning("?? Invalid date format. Use MM/DD/YYYY, T, T-N, or T+N.");
        return null;
    }

    /// <summary>Pause: "Press Enter to continue..."</summary>
    public static void Pause()
    {
        WriteBlank();
        Write(TerminalColor.Dim("Press <Enter> to continue..."));
        Console.ReadLine();
    }

    /// <summary>Clears the console screen.</summary>
    public static void Clear()
    {
        try { Console.Clear(); }
        catch { /* ignore if not supported */ }
    }
}
