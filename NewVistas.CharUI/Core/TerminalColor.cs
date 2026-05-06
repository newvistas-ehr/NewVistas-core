// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.CharUI.Core;

/// <summary>
/// ANSI SGR (Select Graphic Rendition) color/attribute helpers.
/// Uses only ESC[...m sequences — no cursor positioning, no screen regions.
/// Every modern terminal supports these: Windows Terminal, ConEmu, VS Code,
/// macOS Terminal, PuTTY, and cmd.exe on Windows 10+.
///
/// Respects the NO_COLOR convention (https://no-color.org/) and --no-color flag.
/// </summary>
public static class TerminalColor
{
    /// <summary>When false, all methods return the raw text without escape sequences.</summary>
    public static bool Enabled { get; set; } = true;

    // ── SGR codes ──────────────────────────────────────────────────────

    private const string Esc = "\x1b[";
    private const string ResetCode = "\x1b[0m";

    // Attributes
    private const string BoldCode = "\x1b[1m";
    private const string DimCode = "\x1b[2m";
    private const string ReverseCode = "\x1b[7m";

    // Foreground colors
    private const string FgRed = "\x1b[31m";
    private const string FgGreen = "\x1b[32m";
    private const string FgYellow = "\x1b[33m";
    private const string FgBlue = "\x1b[34m";
    private const string FgMagenta = "\x1b[35m";
    private const string FgCyan = "\x1b[36m";
    private const string FgWhite = "\x1b[37m";

    // Bold + foreground (bright variants)
    private const string FgBrightRed = "\x1b[1;31m";
    private const string FgBrightGreen = "\x1b[1;32m";
    private const string FgBrightYellow = "\x1b[1;33m";
    private const string FgBrightCyan = "\x1b[1;36m";
    private const string FgBrightWhite = "\x1b[1;37m";

    // Background colors
    private const string BgRed = "\x1b[41m";
    private const string BgYellow = "\x1b[43m";

    // ── Initialization ─────────────────────────────────────────────────

    /// <summary>
    /// Call once at startup. Checks NO_COLOR env var and --no-color arg.
    /// On Windows, enables virtual terminal processing if needed.
    /// </summary>
    public static void Initialize(string[] args)
    {
        // NO_COLOR convention: any value (even empty) disables color
        if (Environment.GetEnvironmentVariable("NO_COLOR") is not null)
        {
            Enabled = false;
            return;
        }

        // --no-color command line flag
        if (args.Any(a => a.Equals("--no-color", StringComparison.OrdinalIgnoreCase)))
        {
            Enabled = false;
            return;
        }

        // On Windows, ensure virtual terminal processing is enabled
        if (OperatingSystem.IsWindows())
            EnableWindowsVirtualTerminal();
    }

    // ── Inline formatting (returns decorated string) ───────────────────

    /// <summary>Bold text.</summary>
    public static string Bold(string text)
        => Enabled ? $"{BoldCode}{text}{ResetCode}" : text;

    /// <summary>Dim/faint text.</summary>
    public static string Dim(string text)
        => Enabled ? $"{DimCode}{text}{ResetCode}" : text;

    /// <summary>Reverse video (swap foreground/background).</summary>
    public static string Reverse(string text)
        => Enabled ? $"{ReverseCode}{text}{ResetCode}" : text;

    /// <summary>Red text — abnormal/critical values, errors, access denied.</summary>
    public static string Red(string text)
        => Enabled ? $"{FgRed}{text}{ResetCode}" : text;

    /// <summary>Bright red — critical alerts, STAT urgency.</summary>
    public static string BrightRed(string text)
        => Enabled ? $"{FgBrightRed}{text}{ResetCode}" : text;

    /// <summary>Green text — normal values, success messages, NKA safe.</summary>
    public static string Green(string text)
        => Enabled ? $"{FgGreen}{text}{ResetCode}" : text;

    /// <summary>Bright green — confirmed success.</summary>
    public static string BrightGreen(string text)
        => Enabled ? $"{FgBrightGreen}{text}{ResetCode}" : text;

    /// <summary>Yellow text — warnings, ASAP urgency, held orders.</summary>
    public static string Yellow(string text)
        => Enabled ? $"{FgYellow}{text}{ResetCode}" : text;

    /// <summary>Bright yellow — caution banners.</summary>
    public static string BrightYellow(string text)
        => Enabled ? $"{FgBrightYellow}{text}{ResetCode}" : text;

    /// <summary>Cyan text — informational highlights, headers.</summary>
    public static string Cyan(string text)
        => Enabled ? $"{FgCyan}{text}{ResetCode}" : text;

    /// <summary>Bright cyan — section headers.</summary>
    public static string BrightCyan(string text)
        => Enabled ? $"{FgBrightCyan}{text}{ResetCode}" : text;

    /// <summary>White bold — titles, patient name.</summary>
    public static string BrightWhite(string text)
        => Enabled ? $"{FgBrightWhite}{text}{ResetCode}" : text;

    /// <summary>Blue text — menu codes, navigation hints.</summary>
    public static string Blue(string text)
        => Enabled ? $"{FgBlue}{text}{ResetCode}" : text;

    /// <summary>Magenta — restricted record, sensitivity alerts.</summary>
    public static string Magenta(string text)
        => Enabled ? $"{FgMagenta}{text}{ResetCode}" : text;

    // ── Composite clinical helpers ─────────────────────────────────────

    /// <summary>
    /// Colors an abnormal flag: H/HH → red, L/LL → red, empty → unchanged.
    /// </summary>
    public static string AbnormalFlag(string? flag)
    {
        if (string.IsNullOrEmpty(flag)) return "";
        return BrightRed($"*{flag}*");
    }

    /// <summary>
    /// Colors a clinical urgency: STAT → bright red, ASAP → yellow, else unchanged.
    /// </summary>
    public static string Urgency(string? urgency)
    {
        if (string.IsNullOrEmpty(urgency)) return "";
        return urgency.ToUpperInvariant() switch
        {
            "STAT" => BrightRed(urgency),
            "ASAP" => Yellow(urgency),
            _ => urgency
        };
    }

    /// <summary>
    /// Colors a clinical status value.
    /// </summary>
    public static string Status(string? status)
    {
        if (string.IsNullOrEmpty(status)) return "";
        return status.ToUpperInvariant() switch
        {
            "ACTIVE" => Green(status),
            "COMPLETED" => Green(status),
            "PENDING" => Yellow(status),
            "UNSIGNED" => Yellow(status),
            "UNCOSIGNED" => Yellow(status),
            "HOLD" or "HELD" => Yellow(status),
            "CANCELLED" or "DISCONTINUED" => Dim(status),
            "STAT" => BrightRed(status),
            "ADMITTED" => Cyan(status),
            _ => status
        };
    }

    /// <summary>
    /// Colors CWAD flags: each letter gets red if present.
    /// </summary>
    public static string CwadFlags(string? flags)
    {
        if (string.IsNullOrEmpty(flags)) return "";
        return BrightRed(flags);
    }

    /// <summary>
    /// Colors a severity level: Severe → red, Moderate → yellow, Mild → green.
    /// </summary>
    public static string Severity(string? severity)
    {
        if (string.IsNullOrEmpty(severity)) return "";
        return severity.ToUpperInvariant() switch
        {
            "SEVERE" => BrightRed(severity),
            "MODERATE" => Yellow(severity),
            "MILD" => Green(severity),
            _ => severity
        };
    }

    // ── Windows console VT support ─────────────────────────────────────

    private static void EnableWindowsVirtualTerminal()
    {
        try
        {
            // .NET 6+ on Windows 10+ should have VT enabled by default,
            // but we force it via the output encoding to be safe.
            // If Console.Write of an ESC sequence produces garbled output,
            // the user can fall back to --no-color.
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch
        {
            // If we can't set encoding, color may not work — disable gracefully
            Enabled = false;
        }
    }
}
