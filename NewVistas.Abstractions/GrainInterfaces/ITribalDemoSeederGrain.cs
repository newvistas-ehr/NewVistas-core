// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Loads the tribal demo dataset (under <c>exports/TribalDemo/</c>) into the
/// running silo by replaying the same workflows a live operator would invoke:
/// patient registration with IHS eligibility hints, CHS authorization
/// requests + approvals/denials, and a completed GPRA report.
///
/// Singleton — keyed by the constant <c>"TRIBAL-DEMO-SEEDER"</c>. Stateless;
/// re-running with the same manifest is idempotent on patient identity
/// (ICNs are derived from each patient's index in the manifest using the
/// externally-supplied-ICN registration path).
///
/// See <see href="../../../exports/TribalDemo/README.md">exports/TribalDemo/README.md</see>
/// for the dataset description and
/// <see href="../Docs/Human-Test-Scripts/Blazor/Admin/13-Tribal-Demo-Data.md">Admin/13-Tribal-Demo-Data.md</see>
/// for the operator runbook.
/// </summary>
public interface ITribalDemoSeederGrain : IGrainWithStringKey
{
    /// <summary>
    /// Load the manifest. Reads <c>patients.json</c>, <c>chs-referrals.json</c>,
    /// and <c>gpra-report.json</c> from <paramref name="manifestDirectory"/>
    /// and creates corresponding grain state. Returns a result summarizing
    /// what was created.
    /// </summary>
    /// <param name="manifestDirectory">Absolute path to the directory containing the JSON manifests (e.g., <c>"exports/TribalDemo"</c>).</param>
    /// <param name="seededByUserId">Operator id for the audit trail.</param>
    /// <param name="seededByUserName">Operator display name.</param>
    [RequiresSecurityKey(SecurityKeys.CanRegisterPatients)]
    [AuditAction("DEMO", "LOAD_TRIBAL_DEMO", EntityType = "DemoDataset", IsClinicalWrite = false)]
    Task<TribalDemoSeedResult> LoadAsync(
        string manifestDirectory,
        string seededByUserId,
        string seededByUserName);
}

/// <summary>
/// Summary returned from <see cref="ITribalDemoSeederGrain.LoadAsync"/>.
/// </summary>
[GenerateSerializer]
public class TribalDemoSeedResult
{
    /// <summary>How many patients were registered (or re-asserted) from <c>patients.json</c>.</summary>
    [Id(0)] public int PatientsRegistered { get; set; }

    /// <summary>How many CHS referrals were created from <c>chs-referrals.json</c>.</summary>
    [Id(1)] public int ChsReferralsCreated { get; set; }

    /// <summary>How many CHS referrals were approved during seeding.</summary>
    [Id(2)] public int ChsReferralsApproved { get; set; }

    /// <summary>How many CHS referrals were denied during seeding.</summary>
    [Id(3)] public int ChsReferralsDenied { get; set; }

    /// <summary>1 if a GPRA report was created from <c>gpra-report.json</c>; 0 if the file was missing.</summary>
    [Id(4)] public int GpraReportsCreated { get; set; }

    /// <summary>ICNs of the patients in the order they were registered.</summary>
    [Id(5)] public List<string> PatientIcns { get; set; } = new();

    /// <summary>Per-file errors encountered (the loader is best-effort: a malformed referral does not abort the whole load).</summary>
    [Id(6)] public List<string> Errors { get; set; } = new();
}
