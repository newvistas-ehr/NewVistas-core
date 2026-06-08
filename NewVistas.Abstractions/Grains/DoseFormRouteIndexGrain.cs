// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index grain mapping dose forms to valid routes of administration,
/// derived from RxNorm dose-form / dose-form-group metadata and reconciled onto
/// the canonical VistA Standard Medication Routes (File #51.23).
/// Grain key: "DOSE-FORM-ROUTE-INDEX".
///
/// Self-seeding: on first activation, loads three embedded tables —
///   A. DFG → valid VistA routes (curated clinical mapping; human-owned)
///   B. DF  → DFG membership (RxNorm has_dose_form_group)
///   C. VistA dose form / dispense unit → RxNorm DF (the join bridge)
///
/// The vocabulary is small (~50 groups, ~150 forms) and stable (changes a few
/// times per year), so it ships embedded and works fully offline. An optional
/// RxNav refresh updates only tables B and C; the curated table A is never
/// overwritten.
/// </summary>
public class DoseFormRouteIndexGrain : Grain, IDoseFormRouteIndexGrain
{
    private readonly IPersistentState<DoseFormRouteIndexState> _state;
    private readonly IRxNavDoseFormClient _rxNav;

    public DoseFormRouteIndexGrain(
        [PersistentState("doseFormRouteState", "doseFormRouteStore")]
        IPersistentState<DoseFormRouteIndexState> state,
        IRxNavDoseFormClient rxNav)
    {
        _state = state;
        _rxNav = rxNav;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (!_state.State.IsLoaded)
            await SeedFromEmbeddedDataAsync();

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task SeedFromEmbeddedDataAsync()
    {
        _state.State.GroupsByName.Clear();
        _state.State.FormsByName.Clear();
        _state.State.VistaFormToRxNormForm.Clear();

        // Table A — dose form group → valid VistA routes (curated).
        foreach ((string name, string rxCui, string[] routes) in DfgRouteData)
        {
            _state.State.GroupsByName[name.ToUpperInvariant()] = new DoseFormGroup
            {
                Name = name,
                RxCui = string.IsNullOrEmpty(rxCui) ? null : rxCui,
                ValidVistaRoutes = routes.ToList()
            };
        }

        // Table B — dose form → dose form group membership.
        foreach ((string name, string rxCui, string[] groups) in DfDfgData)
        {
            _state.State.FormsByName[name.ToUpperInvariant()] = new DoseFormEntry
            {
                Name = name,
                RxCui = string.IsNullOrEmpty(rxCui) ? null : rxCui,
                DoseFormGroupNames = groups.ToList()
            };
        }

        // Table C — VistA dose form / dispense unit → RxNorm dose form(s).
        foreach ((string vistaForm, string[] rxNormForms) in VistaFormData)
            _state.State.VistaFormToRxNormForm[vistaForm.ToUpperInvariant()] = rxNormForms.ToList();

        _state.State.IsLoaded = true;
        _state.State.SourceVersion = "embedded-rxnorm-appendix3-2026";
        _state.State.LastRefreshedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<string>> GetValidRoutesForDoseFormAsync(string vistaDosageFormName) =>
        Task.FromResult(ResolveValidRoutes(vistaDosageFormName));

    public Task<bool> IsRouteValidForDoseFormAsync(string vistaDosageFormName, string route)
    {
        // Fail open: nothing to evaluate → do not warn.
        if (string.IsNullOrWhiteSpace(vistaDosageFormName) || string.IsNullOrWhiteSpace(route))
            return Task.FromResult(true);

        List<string> validRoutes = ResolveValidRoutes(vistaDosageFormName);

        // Unknown/unmapped dose form → fail open.
        if (validRoutes.Count == 0)
            return Task.FromResult(true);

        bool isValid = validRoutes.Contains(route.Trim(), StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(isValid);
    }

    public Task<List<DoseFormGroup>> GetAllGroupsAsync() =>
        Task.FromResult(_state.State.GroupsByName.Values.OrderBy(g => g.Name).ToList());

    public Task<DoseFormGroup?> GetGroupByNameAsync(string name)
    {
        _state.State.GroupsByName.TryGetValue(name.ToUpperInvariant(), out DoseFormGroup? group);
        return Task.FromResult(group);
    }

    public Task<bool> IsLoadedAsync() => Task.FromResult(_state.State.IsLoaded);

    public async Task<int> RefreshFromRxNavAsync()
    {
        // Offline default: the Null client reports disabled and fetches nothing.
        RxNavDoseFormSnapshot? snapshot = await _rxNav.FetchDoseFormMetadataAsync();
        if (snapshot is null || snapshot.DoseForms.Count == 0)
            return 0;

        // Refresh only the DF→DFG bridge (table B). The curated DFG→route
        // mapping (table A) is human-owned and intentionally left untouched.
        foreach (DoseFormEntry form in snapshot.DoseForms)
            _state.State.FormsByName[form.Name.ToUpperInvariant()] = form;

        _state.State.SourceVersion = snapshot.SourceVersion;
        _state.State.LastRefreshedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return snapshot.DoseForms.Count;
    }

    /// <summary>
    /// Resolves the union of valid VistA routes for a VistA dose form / dispense
    /// unit string via the chain: VistA form → RxNorm dose form(s) → group(s) →
    /// routes. Also accepts an RxNorm dose form name directly. Returns an empty
    /// list when nothing maps (caller fails open).
    /// </summary>
    private List<string> ResolveValidRoutes(string vistaDosageFormName)
    {
        if (string.IsNullOrWhiteSpace(vistaDosageFormName))
            return new List<string>();

        string key = vistaDosageFormName.Trim().ToUpperInvariant();

        // Candidate RxNorm dose forms: via the VistA bridge, or the input may
        // already be an RxNorm dose form name.
        List<string> candidateForms;
        if (_state.State.VistaFormToRxNormForm.TryGetValue(key, out List<string>? mapped))
            candidateForms = mapped;
        else if (_state.State.FormsByName.ContainsKey(key))
            candidateForms = new List<string> { key };
        else
            return new List<string>();

        HashSet<string> routes = new(StringComparer.OrdinalIgnoreCase);
        foreach (string formName in candidateForms)
        {
            if (!_state.State.FormsByName.TryGetValue(formName.ToUpperInvariant(), out DoseFormEntry? form))
                continue;

            foreach (string groupName in form.DoseFormGroupNames)
            {
                if (_state.State.GroupsByName.TryGetValue(groupName.ToUpperInvariant(), out DoseFormGroup? group))
                    foreach (string route in group.ValidVistaRoutes)
                        routes.Add(route);
            }
        }

        return routes.OrderBy(r => r).ToList();
    }

    // ───────────────────────────────────────────────────────────────────────
    // Table A — Dose Form Group → valid VistA routes (File #51.23 names).
    // ALL 44 dose form groups from RxNorm Appendix 3. Route strings MUST match
    // File #51.23 field .01 exactly (mind hyphens: INTRA-ARTERIAL; and NASAL,
    // not INTRANASAL) — enforced by the drift-guard unit test.
    //
    // Routes are a CURATED clinical mapping; RxNorm does not publish DFG→route.
    // Physical-form groups (Pill, Granule, Lozenge, etc.) carry the broad oral
    // route; finer routes (SUBLINGUAL/BUCCAL) come from a form's OWN specific
    // group (Sublingual/Buccal Product) so a plain oral tablet is not falsely
    // validated for sublingual use. RxCUIs are intentionally blank — Appendix 3
    // does not publish them; backfill from RxNav later (Oral Product 1151133 is
    // the one verified id kept).
    // ───────────────────────────────────────────────────────────────────────
    private static readonly (string Name, string RxCui, string[] VistaRoutes)[] DfgRouteData =
    [
        ("Buccal Product",            "",        ["BUCCAL", "ORAL"]),
        ("Chewable Product",          "",        ["ORAL"]),
        ("Dental Product",            "",        ["DENTAL", "ORAL"]),
        ("Disintegrating Oral Product","",       ["ORAL"]),
        ("Drug Implant Product",      "",        ["SUBCUTANEOUS", "INTRAUTERINE"]),
        ("Flake Product",             "",        ["ORAL"]),
        ("Granule Product",           "",        ["ORAL", "ENTERAL"]),
        ("Inhalant Product",          "",        ["INHALATION", "NEBULIZATION", "NASAL"]),
        ("Injectable Product",        "",        ["INTRAVENOUS", "INTRAMUSCULAR", "SUBCUTANEOUS", "INTRADERMAL", "INTRA-ARTERIAL", "EPIDURAL", "INTRATHECAL", "INTRA-ARTICULAR", "INTRAOSSEOUS"]),
        ("Intraperitoneal Product",   "",        ["INTRAPERITONEAL"]),
        ("Intratracheal Product",     "",        ["INTRATRACHEAL"]),
        ("Intravesical Product",      "",        ["INTRAVESICAL"]),
        ("Irrigation Product",        "",        ["IRRIGATION"]),
        ("Lozenge Product",           "",        ["ORAL"]),
        ("Medicated Pad or Tape",     "",        ["TOPICAL", "TRANSDERMAL"]),
        ("Mouthwash Product",         "",        ["ORAL"]),
        ("Mucosal Product",           "",        ["BUCCAL", "SUBLINGUAL", "ORAL", "TOPICAL"]),
        ("Nasal Product",             "",        ["NASAL"]),
        ("Ophthalmic Product",        "",        ["OPHTHALMIC", "INTRAOCULAR", "INTRAVITREAL", "SUBCONJUNCTIVAL"]),
        ("Oral Cream Product",        "",        ["ORAL"]),
        ("Oral Film Product",         "",        ["ORAL"]),
        ("Oral Foam Product",         "",        ["ORAL"]),
        ("Oral Gel Product",          "",        ["ORAL"]),
        ("Oral Liquid Product",       "",        ["ORAL", "ENTERAL"]),
        ("Oral Ointment Product",     "",        ["ORAL"]),
        ("Oral Paste Product",        "",        ["ORAL"]),
        ("Oral Powder Product",       "",        ["ORAL"]),
        ("Oral Product",              "1151133", ["ORAL", "ENTERAL"]),
        ("Oral Spray Product",        "",        ["ORAL", "SUBLINGUAL", "TRANSLINGUAL"]),
        ("Otic Product",              "",        ["OTIC"]),
        ("Paste Product",             "",        ["ORAL", "TOPICAL"]),
        ("Pellet Product",            "",        ["ORAL"]),
        ("Pill",                      "",        ["ORAL", "ENTERAL"]),
        // Pyelocalyceal (renal pelvis) instillation has no File #51.23 route —
        // leave empty so it fails open (never warns) rather than mis-mapping.
        ("Pyelocalyceal Product",     "",        []),
        ("Rectal Product",            "",        ["RECTAL"]),
        ("Shampoo Product",           "",        ["TOPICAL"]),
        ("Soap Product",              "",        ["TOPICAL"]),
        ("Sublingual Product",        "",        ["SUBLINGUAL", "ORAL"]),
        ("Toothpaste Product",        "",        ["DENTAL", "ORAL"]),
        ("Topical Product",           "",        ["TOPICAL", "TRANSDERMAL"]),
        ("Transdermal Product",       "",        ["TRANSDERMAL", "TOPICAL"]),
        ("Urethral Product",          "",        ["URETHRAL"]),
        ("Vaginal Product",           "",        ["VAGINAL"]),
        ("Wafer Product",             "",        ["ORAL"]),
    ];

    // ───────────────────────────────────────────────────────────────────────
    // Table B — RxNorm Dose Form (TTY=DF) → Dose Form Group(s) (TTY=DFG).
    // Complete has_dose_form_group membership transcribed from RxNorm Appendix 3
    // (~120 dose forms). A form belongs to every group that lists it; the valid
    // routes are the UNION across its groups' routes. RxCUIs blank (not in
    // Appendix 3) — backfill from RxNav later.
    // ───────────────────────────────────────────────────────────────────────
    private static readonly (string Name, string RxCui, string[] Groups)[] DfDfgData =
    [
        // Buccal
        ("Buccal Film",                       "", ["Buccal Product", "Oral Product"]),
        ("Buccal Tablet",                     "", ["Buccal Product", "Oral Product", "Pill"]),
        ("Sustained Release Buccal Tablet",   "", ["Buccal Product", "Oral Product", "Pill"]),
        // Chewable
        ("Chewable Extended Release Oral Tablet", "", ["Chewable Product", "Oral Product", "Pill"]),
        ("Chewable Tablet",                   "", ["Chewable Product", "Oral Product", "Pill"]),
        ("Chewing Gum",                       "", ["Chewable Product", "Oral Product"]),
        // Dental / mouthwash / paste / toothpaste
        ("Mouthwash",                         "", ["Dental Product", "Mouthwash Product", "Oral Liquid Product", "Oral Product"]),
        ("Toothpaste",                        "", ["Dental Product", "Oral Paste Product", "Paste Product", "Toothpaste Product"]),
        ("Oral Paste",                        "", ["Oral Paste Product", "Paste Product", "Oral Product"]),
        ("Paste",                             "", ["Paste Product"]),
        // Disintegrating
        ("Disintegrating Oral Tablet",        "", ["Disintegrating Oral Product", "Oral Product", "Pill"]),
        // Implants
        ("Drug Implant",                      "", ["Drug Implant Product"]),
        ("Intrauterine System",               "", ["Drug Implant Product"]),
        // Flake
        ("Oral Flakes",                       "", ["Flake Product", "Oral Product"]),
        // Granule
        ("Delayed Release Oral Granules",     "", ["Granule Product", "Oral Product"]),
        ("Granules for Oral Solution",        "", ["Granule Product", "Oral Product"]),
        ("Granules for Oral Suspension",      "", ["Granule Product", "Oral Product"]),
        ("Oral Granules",                     "", ["Granule Product", "Oral Product"]),
        // Inhalant / nasal-inhalant
        ("Dry Powder Inhaler",                "", ["Inhalant Product"]),
        ("Gas for Inhalation",                "", ["Inhalant Product"]),
        ("Inhalation Powder",                 "", ["Inhalant Product"]),
        ("Inhalation Solution",               "", ["Inhalant Product"]),
        ("Inhalation Spray",                  "", ["Inhalant Product"]),
        ("Inhalation Suspension",             "", ["Inhalant Product"]),
        ("Metered Dose Inhaler",              "", ["Inhalant Product"]),
        ("Metered Dose Nasal Spray",          "", ["Inhalant Product", "Nasal Product"]),
        ("Nasal Inhalant",                    "", ["Inhalant Product", "Nasal Product"]),
        ("Nasal Spray",                       "", ["Inhalant Product", "Nasal Product"]),
        // Injectable
        ("Auto-Injector",                     "", ["Injectable Product"]),
        ("Cartridge",                         "", ["Injectable Product"]),
        ("Injectable Foam",                   "", ["Injectable Product"]),
        ("Injectable Solution",               "", ["Injectable Product"]),
        ("Injectable Suspension",             "", ["Injectable Product"]),
        ("Injection",                         "", ["Injectable Product"]),
        ("Jet Injector",                      "", ["Injectable Product"]),
        ("Pen Injector",                      "", ["Injectable Product"]),
        ("Prefilled Syringe",                 "", ["Injectable Product"]),
        // Body-cavity
        ("Intraperitoneal Solution",          "", ["Intraperitoneal Product"]),
        ("Intratracheal Suspension",          "", ["Intratracheal Product"]),
        ("Intravesical Solution",             "", ["Intravesical Product"]),
        ("Intravesical Suspension",           "", ["Intravesical Product"]),
        ("Powder for Intravesical Solution",  "", ["Intravesical Product"]),
        ("Powder for Intravesical Suspension","", ["Intravesical Product"]),
        ("Powder for Pyelocalyceal Solution", "", ["Pyelocalyceal Product"]),
        // Irrigation
        ("Irrigation Solution",               "", ["Irrigation Product"]),
        // Lozenge
        ("Oral Lozenge",                      "", ["Lozenge Product", "Oral Product"]),
        // Medicated pad / tape
        ("Medicated Pad",                     "", ["Medicated Pad or Tape", "Topical Product"]),
        ("Medicated Tape",                    "", ["Medicated Pad or Tape", "Topical Product"]),
        // Mucosal
        ("Mucosal Spray",                     "", ["Mucosal Product"]),
        ("Mucous Membrane Topical Solution",  "", ["Mucosal Product"]),
        // Nasal
        ("Nasal Gel",                         "", ["Nasal Product"]),
        ("Nasal Ointment",                    "", ["Nasal Product"]),
        ("Nasal Powder",                      "", ["Nasal Product"]),
        ("Nasal Solution",                    "", ["Nasal Product"]),
        ("Powder for Nasal Solution",         "", ["Nasal Product"]),
        // Ophthalmic
        ("Ophthalmic Cream",                  "", ["Ophthalmic Product"]),
        ("Ophthalmic Gel",                    "", ["Ophthalmic Product"]),
        ("Ophthalmic Irrigation Solution",    "", ["Ophthalmic Product"]),
        ("Ophthalmic Ointment",               "", ["Ophthalmic Product"]),
        ("Ophthalmic Solution",               "", ["Ophthalmic Product"]),
        ("Ophthalmic Spray",                  "", ["Ophthalmic Product"]),
        ("Ophthalmic Suspension",             "", ["Ophthalmic Product"]),
        // Oral cream / film / foam / gel / ointment / powder / spray
        ("Oral Cream",                        "", ["Oral Cream Product", "Oral Product"]),
        ("Oral Film",                         "", ["Oral Film Product", "Oral Product"]),
        ("Sublingual Film",                   "", ["Oral Film Product", "Sublingual Product", "Oral Product"]),
        ("Oral Foam",                         "", ["Oral Foam Product", "Oral Product"]),
        ("Oral Gel",                          "", ["Oral Gel Product", "Oral Product"]),
        ("Oral Ointment",                     "", ["Oral Ointment Product", "Oral Product"]),
        ("Oral Powder",                       "", ["Oral Powder Product", "Oral Product"]),
        ("Powder for Oral Solution",          "", ["Oral Powder Product", "Oral Product"]),
        ("Powder for Oral Suspension",        "", ["Oral Powder Product", "Oral Product"]),
        ("Sublingual Powder",                 "", ["Oral Powder Product", "Sublingual Product", "Oral Product"]),
        ("Oral Spray",                        "", ["Oral Liquid Product", "Oral Product", "Oral Spray Product"]),
        // Oral liquids
        ("Extended Release Suspension",       "", ["Oral Liquid Product", "Oral Product"]),
        ("Oral Solution",                     "", ["Oral Liquid Product", "Oral Product"]),
        ("Oral Suspension",                   "", ["Oral Liquid Product", "Oral Product"]),
        ("Tablet for Oral Suspension",        "", ["Oral Liquid Product", "Oral Product"]),
        // Oral solids (pills + capsules + tablets)
        ("Delayed Release Oral Capsule",      "", ["Oral Product", "Pill"]),
        ("Delayed Release Oral Tablet",       "", ["Oral Product", "Pill"]),
        ("Effervescent Oral Tablet",          "", ["Oral Product"]),
        ("Extended Release Oral Capsule",     "", ["Oral Product", "Pill"]),
        ("Extended Release Oral Tablet",      "", ["Oral Product", "Pill"]),
        ("Oral Capsule",                      "", ["Oral Product", "Pill"]),
        ("Oral Tablet",                       "", ["Oral Product", "Pill"]),
        ("Sublingual Tablet",                 "", ["Sublingual Product", "Oral Product", "Pill"]),
        ("Oral Pellet",                       "", ["Oral Product", "Pellet Product"]),
        ("Oral Wafer",                        "", ["Oral Product", "Wafer Product"]),
        // Otic
        ("Otic Gel",                          "", ["Otic Product"]),
        ("Otic Ointment",                     "", ["Otic Product"]),
        ("Otic Solution",                     "", ["Otic Product"]),
        ("Otic Suspension",                   "", ["Otic Product"]),
        // Rectal
        ("Enema",                             "", ["Rectal Product"]),
        ("Rectal Cream",                      "", ["Rectal Product"]),
        ("Rectal Foam",                       "", ["Rectal Product"]),
        ("Rectal Gel",                        "", ["Rectal Product"]),
        ("Rectal Ointment",                   "", ["Rectal Product"]),
        ("Rectal Solution",                   "", ["Rectal Product"]),
        ("Rectal Spray",                      "", ["Rectal Product"]),
        ("Rectal Suppository",                "", ["Rectal Product"]),
        // Shampoo / soap
        ("Medicated Shampoo",                 "", ["Shampoo Product"]),
        ("Medicated Bar Soap",                "", ["Soap Product"]),
        ("Medicated Liquid Soap",             "", ["Soap Product"]),
        // Topical / transdermal
        ("Medicated Patch",                   "", ["Topical Product"]),
        ("Powder Spray",                      "", ["Topical Product"]),
        ("Topical Cream",                     "", ["Topical Product"]),
        ("Topical Foam",                      "", ["Topical Product"]),
        ("Topical Gel",                       "", ["Topical Product"]),
        ("Topical Liquefied Gas",             "", ["Topical Product"]),
        ("Topical Lotion",                    "", ["Topical Product"]),
        ("Topical Oil",                       "", ["Topical Product"]),
        ("Topical Ointment",                  "", ["Topical Product"]),
        ("Topical Powder",                    "", ["Topical Product"]),
        ("Topical Solution",                  "", ["Topical Product"]),
        ("Topical Spray",                     "", ["Topical Product"]),
        ("Topical Suspension",                "", ["Topical Product"]),
        ("Transdermal System",                "", ["Topical Product", "Transdermal Product"]),
        // Urethral
        ("Urethral Suppository",              "", ["Urethral Product"]),
        // Vaginal
        ("Douche",                            "", ["Vaginal Product"]),
        ("Vaginal Cream",                     "", ["Vaginal Product"]),
        ("Vaginal Film",                      "", ["Vaginal Product"]),
        ("Vaginal Foam",                      "", ["Vaginal Product"]),
        ("Vaginal Gel",                       "", ["Vaginal Product"]),
        ("Vaginal Insert",                    "", ["Vaginal Product"]),
        ("Vaginal Ointment",                  "", ["Vaginal Product"]),
        ("Vaginal System",                    "", ["Vaginal Product"]),
    ];

    // ───────────────────────────────────────────────────────────────────────
    // Table C — VistA dose form / dispense unit string → RxNorm dose form(s).
    // VistA forms (File #50.606) and dispense units (#50, field 901) are coarse.
    // Genuinely ambiguous forms map to MULTIPLE RxNorm forms so their routes are
    // unioned (e.g. SUPPOSITORY → rectal + vaginal) to avoid false warnings.
    // ───────────────────────────────────────────────────────────────────────
    private static readonly (string VistaForm, string[] RxNormForms)[] VistaFormData =
    [
        // Oral solids
        ("TABLET",              ["Oral Tablet"]),
        ("TAB",                 ["Oral Tablet"]),
        ("TAB,EC",              ["Delayed Release Oral Tablet"]),
        ("TAB,SA",              ["Extended Release Oral Tablet"]),
        ("TAB,ER",              ["Extended Release Oral Tablet"]),
        ("TAB,CHEWABLE",        ["Chewable Tablet"]),
        ("CHEW TAB",            ["Chewable Tablet"]),
        ("TAB,SL",              ["Sublingual Tablet"]),
        ("TAB,BUCCAL",          ["Buccal Tablet"]),
        ("CAPSULE",             ["Oral Capsule"]),
        ("CAP",                 ["Oral Capsule"]),
        ("CAP,SA",              ["Extended Release Oral Capsule"]),
        ("CAP,EC",              ["Delayed Release Oral Capsule"]),
        ("LOZENGE",             ["Oral Lozenge"]),
        ("GRANULE",             ["Oral Granules"]),
        ("GRANULES",            ["Oral Granules"]),
        ("POWDER,ORAL",         ["Oral Powder"]),
        // Oral liquids
        ("SOLUTION,ORAL",       ["Oral Solution"]),
        ("ORAL SOLUTION",       ["Oral Solution"]),
        ("ELIXIR",              ["Oral Solution"]),
        ("SYRUP",               ["Oral Solution"]),
        ("LIQUID",              ["Oral Solution"]),
        ("LIQUID,ORAL",         ["Oral Solution"]),
        ("SUSPENSION,ORAL",     ["Oral Suspension"]),
        ("SUSPENSION",          ["Oral Suspension"]),
        ("SOLUTION",            ["Oral Solution"]),
        ("WAFER",               ["Oral Wafer"]),
        ("PELLET",              ["Oral Pellet"]),
        ("GUM",                 ["Chewing Gum"]),
        ("GUM,CHEWING",         ["Chewing Gum"]),
        ("TAB,EFFERVESCENT",    ["Effervescent Oral Tablet"]),
        // Sublingual / buccal / mucosal
        ("FILM,SL",             ["Sublingual Film"]),
        ("FILM,BUCCAL",         ["Buccal Film"]),
        ("FILM",                ["Oral Film"]),
        ("MOUTHWASH",           ["Mouthwash"]),
        ("PASTE,DENTAL",        ["Toothpaste"]),
        ("TOOTHPASTE",          ["Toothpaste"]),
        ("GEL,DENTAL",          ["Toothpaste"]),
        // Injectable
        ("INJECTION",           ["Injectable Solution"]),
        ("INJ",                 ["Injectable Solution"]),
        ("INJ,SOLN",            ["Injectable Solution"]),
        ("INJECTION,SOLUTION",  ["Injectable Solution"]),
        ("INJ,SUSP",            ["Injectable Suspension"]),
        ("INJECTABLE",          ["Injectable Solution"]),
        ("VIAL",                ["Injectable Solution"]),
        ("SYRINGE",             ["Prefilled Syringe"]),
        ("AUTOINJECTOR",        ["Auto-Injector"]),
        ("IMPLANT",             ["Drug Implant"]),
        // Topical / transdermal
        ("CREAM",               ["Topical Cream"]),
        ("CREAM,TOP",           ["Topical Cream"]),
        ("OINTMENT",            ["Topical Ointment"]),
        ("OINT",                ["Topical Ointment"]),
        ("GEL",                 ["Topical Gel"]),
        ("GEL,TOP",             ["Topical Gel"]),
        ("LOTION",              ["Topical Lotion"]),
        ("FOAM",                ["Topical Foam"]),
        ("OIL",                 ["Topical Oil"]),
        ("OIL,TOP",             ["Topical Oil"]),
        ("PATCH",               ["Transdermal System"]),
        ("PATCH,TRANSDERMAL",   ["Transdermal System"]),
        ("PATCH,MEDICATED",     ["Medicated Patch"]),
        ("TRANSDERMAL",         ["Transdermal System"]),
        ("PAD,MEDICATED",       ["Medicated Pad"]),
        ("SHAMPOO",             ["Medicated Shampoo"]),
        ("SOAP",                ["Medicated Bar Soap"]),
        // Ophthalmic / otic / nasal
        ("SOLUTION,OPHTH",      ["Ophthalmic Solution"]),
        ("OINTMENT,OPHTH",      ["Ophthalmic Ointment"]),
        ("GEL,OPHTH",           ["Ophthalmic Gel"]),
        ("SUSP,OPHTH",          ["Ophthalmic Suspension"]),
        ("CREAM,OPHTH",         ["Ophthalmic Cream"]),
        ("DROPS,OPHTH",         ["Ophthalmic Solution"]),
        ("SOLUTION,OTIC",       ["Otic Solution"]),
        ("SUSP,OTIC",           ["Otic Suspension"]),
        ("GEL,OTIC",            ["Otic Gel"]),
        ("DROPS,OTIC",          ["Otic Solution"]),
        ("SPRAY,NASAL",         ["Nasal Spray"]),
        ("SOLUTION,NASAL",      ["Nasal Solution"]),
        ("GEL,NASAL",           ["Nasal Gel"]),
        ("OINTMENT,NASAL",      ["Nasal Ointment"]),
        // Drops are ambiguous: ophthalmic or otic.
        ("DROPS",               ["Ophthalmic Solution", "Otic Solution"]),
        // Inhalant
        ("INHALER",             ["Metered Dose Inhaler"]),
        ("INHALANT",            ["Metered Dose Inhaler"]),
        ("MDI",                 ["Metered Dose Inhaler"]),
        ("DPI",                 ["Dry Powder Inhaler"]),
        ("SOLN,INHL",           ["Inhalation Solution"]),
        ("SOLUTION,INHALATION", ["Inhalation Solution"]),
        ("POWDER,INHALATION",   ["Inhalation Powder"]),
        // Rectal / vaginal — SUPPOSITORY is ambiguous (rectal or vaginal).
        ("SUPPOSITORY",         ["Rectal Suppository", "Vaginal Insert"]),
        ("SUPP,RTL",            ["Rectal Suppository"]),
        ("SUPP,VAG",            ["Vaginal Insert"]),
        ("ENEMA",               ["Enema"]),
        ("CREAM,RTL",           ["Rectal Cream"]),
        ("OINTMENT,RTL",        ["Rectal Ointment"]),
        ("GEL,RTL",             ["Rectal Gel"]),
        ("SOLUTION,RECTAL",     ["Rectal Solution"]),
        ("CREAM,VAG",           ["Vaginal Cream"]),
        ("GEL,VAG",             ["Vaginal Gel"]),
        ("OINTMENT,VAG",        ["Vaginal Ointment"]),
        ("FOAM,VAG",            ["Vaginal Foam"]),
        ("TAB,VAG",             ["Vaginal Insert"]),
        ("INSERT,VAG",          ["Vaginal Insert"]),
        ("RING,VAG",            ["Vaginal System"]),
        ("SYSTEM,VAG",          ["Vaginal System"]),
        ("DOUCHE",              ["Douche"]),
        // Spray is ambiguous: oral/translingual or nasal.
        ("SPRAY",               ["Mucosal Spray", "Nasal Spray"]),
        // Urethral
        ("SUPP,URETHRAL",       ["Urethral Suppository"]),
        // Irrigation
        ("SOLUTION,IRRIGATION", ["Irrigation Solution"]),
        ("IRRIGATION",          ["Irrigation Solution"]),
    ];
}
