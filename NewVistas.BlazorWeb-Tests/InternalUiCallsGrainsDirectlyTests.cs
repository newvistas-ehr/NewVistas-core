// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;

namespace NewVistas.BlazorWeb.Tests;

/// <summary>
/// Architecture guard: the Blazor main UI is an <b>internal</b> client and must talk to the
/// silo directly through <c>OrleansGrainService</c>. The WebServer's REST API exists for
/// <b>outsiders</b> — the patient portal, FHIR consumers, inbound lab interfaces — plus
/// authentication, which the WebServer owns.
///
/// Routing an internal page through REST adds a network hop, loses the grain call context
/// the silo's authorization and audit filters depend on, and blurs the boundary the whole
/// deployment story rests on. This test fails the build when a page starts doing it, rather
/// than leaving the rule to be remembered.
///
/// The three allowed exceptions are genuine external/auth surfaces, listed explicitly so any
/// addition to the list is a deliberate, reviewed act.
/// </summary>
[TestFixture]
public class InternalUiCallsGrainsDirectlyTests
{
    /// <summary>
    /// Pages permitted to inject <c>HttpClient</c>, with the reason each one is legitimate.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedHttpClientPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Login.razor"] = "Authentication is the WebServer's job — it owns identity and JWT issuance.",
        ["PatientPortal.razor"] = "Exercises the patient-facing portal API, which is an outsider surface by definition.",
        ["FhirGateway.razor"] = "Exercises the FHIR endpoint, which is an outsider surface by definition.",
    };

    private static string PagesDirectory()
    {
        // Walk up from the test binary to the repo root, then into the Blazor pages folder.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NewVistas.BlazorWeb")))
            dir = dir.Parent;

        Assert.That(dir, Is.Not.Null, "Could not locate the repository root from the test output directory.");
        return Path.Combine(dir!.FullName, "NewVistas.BlazorWeb", "Components", "Pages");
    }

    [Test]
    public void BlazorPages_DoNotInjectHttpClient_ExceptTheDeclaredExternalSurfaces()
    {
        string pages = PagesDirectory();
        Assert.That(Directory.Exists(pages), Is.True, $"Pages directory not found: {pages}");

        var offenders = new List<string>();

        foreach (string file in Directory.GetFiles(pages, "*.razor", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            string text = File.ReadAllText(file);

            bool injectsHttpClient = Regex.IsMatch(text, @"^\s*@inject\s+HttpClient\b", RegexOptions.Multiline);
            if (!injectsHttpClient) continue;

            if (!AllowedHttpClientPages.ContainsKey(name))
                offenders.Add(name);
        }

        Assert.That(offenders, Is.Empty,
            "These Blazor pages inject HttpClient. Internal UI pages must call grains directly via "
            + "OrleansGrainService; the WebServer REST API is for outsiders (patient portal, FHIR, "
            + "inbound interfaces) and authentication. Offending pages: " + string.Join(", ", offenders));
    }

    [Test]
    public void TheAllowedHttpClientExceptions_StillExist()
    {
        // Keeps the allow-list honest: if a page is renamed or deleted, the exception should be
        // removed rather than left behind to quietly permit a future file of the same name.
        string pages = PagesDirectory();

        foreach (string name in AllowedHttpClientPages.Keys)
        {
            Assert.That(File.Exists(Path.Combine(pages, name)), Is.True,
                $"Allow-listed page '{name}' no longer exists — remove it from AllowedHttpClientPages.");
        }
    }

    // ── WPF main UI ─────────────────────────────────────────────────────────

    /// <summary>
    /// ViewModels permitted to touch HTTP, with the reason. The WPF app authenticates
    /// against the WebServer (AuthService/ApiClient) and has one screen whose purpose is
    /// the outsider-facing FHIR endpoint; everything else must use OrleansGrainService.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedHttpViewModels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FhirGatewayViewModel.cs"] = "Exercises the public FHIR endpoint, an outsider surface by definition.",
    };

    private static string ViewModelsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NewVistas.Wpf_UI")))
            dir = dir.Parent;

        Assert.That(dir, Is.Not.Null, "Could not locate the repository root from the test output directory.");
        return Path.Combine(dir!.FullName, "NewVistas.Wpf_UI", "ViewModels");
    }

    [Test]
    public void WpfViewModels_DoNotUseHttp_ExceptTheDeclaredExternalSurfaces()
    {
        string vms = ViewModelsDirectory();
        Assert.That(Directory.Exists(vms), Is.True, $"ViewModels directory not found: {vms}");

        var offenders = new List<string>();

        foreach (string file in Directory.GetFiles(vms, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            string text = File.ReadAllText(file);

            // Any HTTP verb through the ApiClient, or a raw HttpClient of its own.
            bool usesHttp =
                Regex.IsMatch(text, @"\b_?[Aa]pi(Client)?\s*\.\s*Http\b") ||
                Regex.IsMatch(text, @"\bHttpClient\b");

            if (!usesHttp) continue;

            if (!AllowedHttpViewModels.ContainsKey(name))
                offenders.Add(name);
        }

        Assert.That(offenders, Is.Empty,
            "These WPF ViewModels reach for HTTP. The main UI must call grains directly via "
            + "OrleansGrainService — the WebServer is for outsiders and for authentication "
            + "(are you who you say you are), while authorization (you may do A but not B) is "
            + "enforced in the grains. Offending ViewModels: " + string.Join(", ", offenders));
    }

    [Test]
    public void WpfApiClient_ExposesOnlyAuthentication()
    {
        // The typed data methods that used to mirror grain calls are gone. If someone adds
        // one back, this fails — the shortcut should not be available to take.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NewVistas.Wpf_UI")))
            dir = dir.Parent;

        string apiClient = Path.Combine(dir!.FullName, "NewVistas.Wpf_UI", "Services", "ApiClient.cs");
        Assert.That(File.Exists(apiClient), Is.True);

        string text = File.ReadAllText(apiClient);
        string[] forbidden =
        {
            "GetPatientAsync", "GetCoverSheetAsync", "GetActiveProblemsAsync", "AddProblemAsync",
            "GetOrdersAsync", "PlaceOrderAsync", "GetAllergiesAsync", "GetVitalsAsync",
            "GetNotesAsync", "GetLabResultsAsync", "GetSecurityKeysAsync", "GetOrderableItemsAsync",
        };

        var present = forbidden.Where(m => text.Contains(m, StringComparison.Ordinal)).ToList();
        Assert.That(present, Is.Empty,
            "ApiClient should carry authentication only. Clinical data belongs on grains, and "
            + "security keys are authorization — read them from IAccessControlGrain. Found: "
            + string.Join(", ", present));
    }

    // ── WpfDelphiUI (CPRS-style client) ─────────────────────────────────────

    [Test]
    public void WpfDelphiViewModels_DoNotUseHttp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NewVistas.WpfDelphiUI")))
            dir = dir.Parent;

        string vms = Path.Combine(dir!.FullName, "NewVistas.WpfDelphiUI", "ViewModels");
        Assert.That(Directory.Exists(vms), Is.True, $"ViewModels directory not found: {vms}");

        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(vms, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"\bHttpClient\b") || Regex.IsMatch(text, @"\b_?[Aa]pi(Client)?\s*\.\s*Http\b"))
                offenders.Add(Path.GetFileName(file));
        }

        Assert.That(offenders, Is.Empty,
            "These WpfDelphiUI ViewModels reach for HTTP. Chart data comes from ChartDataService, "
            + "which calls grains directly. Offending ViewModels: " + string.Join(", ", offenders));
    }

    // ── CharUI (terminal client) ────────────────────────────────────────────

    [Test]
    public void CharUi_UsesHttpOnlyForAuthentication()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "NewVistas.CharUI")))
            dir = dir.Parent;

        string root = Path.Combine(dir!.FullName, "NewVistas.CharUI");
        Assert.That(Directory.Exists(root), Is.True);

        // LoginMenu owns sign-in and MFA, which is the WebServer's job. Nothing else
        // in the terminal client should be constructing an HttpClient — notably not the
        // electronic-signature check, which reads the hash from the person grain and must
        // fail closed rather than "accept if the API is unreachable".
        var offenders = new List<string>();
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;

            string name = Path.GetFileName(file);
            if (name.Equals("LoginMenu.cs", StringComparison.OrdinalIgnoreCase)) continue;

            if (Regex.IsMatch(File.ReadAllText(file), @"new\s+HttpClient\b"))
                offenders.Add(name);
        }

        Assert.That(offenders, Is.Empty,
            "Only LoginMenu (authentication) may use HttpClient in CharUI. Offending files: "
            + string.Join(", ", offenders));
    }

    [Test]
    public void BoneHealthPage_UsesGrainsDirectly()
    {
        // The page added for the bone-health module, asserted explicitly because it was written
        // at the same time as its REST controller and is the obvious candidate to get this wrong.
        string page = Path.Combine(PagesDirectory(), "BoneHealth.razor");
        Assert.That(File.Exists(page), Is.True);

        string text = File.ReadAllText(page);
        Assert.Multiple(() =>
        {
            Assert.That(Regex.IsMatch(text, @"^\s*@inject\s+OrleansGrainService\b", RegexOptions.Multiline), Is.True,
                "BoneHealth.razor should inject OrleansGrainService.");
            Assert.That(Regex.IsMatch(text, @"^\s*@inject\s+HttpClient\b", RegexOptions.Multiline), Is.False,
                "BoneHealth.razor must not inject HttpClient.");
            Assert.That(text, Does.Contain("GetGrain<IPatientWorkflowGrain>"),
                "BoneHealth.razor should reach the workflow grain directly.");
        });
    }
}
