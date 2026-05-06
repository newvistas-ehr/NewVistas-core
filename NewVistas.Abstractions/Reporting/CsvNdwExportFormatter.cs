// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Text;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Default CSV formatter for NDW exports. Writes three files into the
/// output directory:
///   • <c>patients.csv</c> — one row per patient (demographics + IHS eligibility)
///   • <c>problems.csv</c> — one row per problem-list entry
///   • <c>immunizations.csv</c> — one row per immunization
///
/// <para>
/// Round-1 scope: these three domains are the smallest viable NDW-shaped
/// extract. Encounters, labs, pharmacy, and procedures are intentionally
/// deferred to the next iteration (they require more involved modeling and
/// the actual IHS NDW spec to drive column layout).
/// </para>
///
/// <para>
/// Streams patient grain reads sequentially to avoid loading the whole
/// cohort into memory. For very large cohorts (10k+ patients), consider
/// switching to parallel batched reads — the per-file <see cref="StreamWriter"/>
/// pattern stays the same.
/// </para>
/// </summary>
public sealed class CsvNdwExportFormatter : INdwExportFormatter
{
    public string FormatVersion => "csv-v1";

    public async Task<IReadOnlyList<string>> WriteToAsync(NdwExportContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(context.OutputDirectory))
            throw new ArgumentException("OutputDirectory is required.", nameof(context));

        Directory.CreateDirectory(context.OutputDirectory);

        var written = new List<string>();
        written.Add(await WritePatientsAsync(context));
        written.Add(await WriteProblemsAsync(context));
        written.Add(await WriteImmunizationsAsync(context));
        return written;
    }

    // ── patients.csv ────────────────────────────────────────────────────────
    private static async Task<string> WritePatientsAsync(NdwExportContext ctx)
    {
        const string fileName = "patients.csv";
        string path = Path.Combine(ctx.OutputDirectory, fileName);
        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        await writer.WriteLineAsync(
            "Icn,Dfn,Name,Sex,DateOfBirth,SsnLast4,Veteran,PrimaryEligibilityCode,IsActive");

        foreach (string icn in ctx.PatientIcns)
        {
            IPatientGrain grain = ctx.GrainFactory.GetGrain<IPatientGrain>(icn);
            PatientState p = await grain.GetPatientAsync();

            string ssnLast4 = p.SocialSecurityNumber is { Length: >= 4 }
                ? p.SocialSecurityNumber[^4..]
                : string.Empty;

            await writer.WriteLineAsync(string.Join(",", new[]
            {
                Esc(icn),
                Esc(p.Dfn ?? string.Empty),
                Esc(p.Name),
                Esc(p.Sex ?? string.Empty),
                p.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ssnLast4,
                Esc(p.Veteran ?? string.Empty),
                Esc(p.PrimaryEligibilityCode ?? string.Empty),
                p.IsActive ? "Y" : "N",
            }));
        }

        return fileName;
    }

    // ── problems.csv ────────────────────────────────────────────────────────
    private static async Task<string> WriteProblemsAsync(NdwExportContext ctx)
    {
        const string fileName = "problems.csv";
        string path = Path.Combine(ctx.OutputDirectory, fileName);
        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        await writer.WriteLineAsync("Icn,ProblemId,Diagnosis,DiagnosisCode,Status,DateRecorded");

        foreach (string icn in ctx.PatientIcns)
        {
            IPatientGrain grain = ctx.GrainFactory.GetGrain<IPatientGrain>(icn);
            List<ProblemEntry> problems = await grain.GetProblemsAsync();

            foreach (ProblemEntry pr in problems)
            {
                // Filter by date when available; entries with no date pass through.
                if (pr.DateRecorded != default
                    && (pr.DateRecorded < ctx.PeriodStart || pr.DateRecorded > ctx.PeriodEnd))
                    continue;

                await writer.WriteLineAsync(string.Join(",", new[]
                {
                    Esc(icn),
                    Esc(pr.ProblemId),
                    Esc(pr.Diagnosis),
                    Esc(pr.DiagnosisCode ?? string.Empty),
                    Esc(pr.Status ?? string.Empty),
                    pr.DateRecorded == default
                        ? string.Empty
                        : pr.DateRecorded.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                }));
            }
        }

        return fileName;
    }

    // ── immunizations.csv ───────────────────────────────────────────────────
    private static async Task<string> WriteImmunizationsAsync(NdwExportContext ctx)
    {
        const string fileName = "immunizations.csv";
        string path = Path.Combine(ctx.OutputDirectory, fileName);
        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        await writer.WriteLineAsync("Icn,ImmunizationId,ImmunizationName,CvxCode,EventDateTime,Series");

        foreach (string icn in ctx.PatientIcns)
        {
            IPatientGrain grain = ctx.GrainFactory.GetGrain<IPatientGrain>(icn);
            List<ImmunizationEntry> imms = await grain.GetImmunizationsAsync();

            foreach (ImmunizationEntry im in imms)
            {
                if (im.EventDateTime != default
                    && (im.EventDateTime < ctx.PeriodStart || im.EventDateTime > ctx.PeriodEnd))
                    continue;

                await writer.WriteLineAsync(string.Join(",", new[]
                {
                    Esc(icn),
                    Esc(im.ImmunizationId),
                    Esc(im.ImmunizationName ?? string.Empty),
                    Esc(im.CvxCode ?? string.Empty),
                    im.EventDateTime == default
                        ? string.Empty
                        : im.EventDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Esc(im.Series ?? string.Empty),
                }));
            }
        }

        return fileName;
    }

    /// <summary>CSV-escape a string field. Quotes wrap any field containing comma, quote, or newline; embedded quotes double.</summary>
    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
