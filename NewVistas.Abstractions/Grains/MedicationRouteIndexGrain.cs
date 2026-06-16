// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index grain for Standard Medication Routes (VistA File #51.23).
/// Grain key: "MED-ROUTE-INDEX"
///
/// Self-seeding: on first activation, loads all 55 routes from the embedded
/// VistA ZWR data. The static array was extracted verbatim from
/// "51.23+STANDARD MEDICATION ROUTES.zwr" (OSEHRA export, Nov 2018).
/// </summary>
public class MedicationRouteIndexGrain : Grain, IMedicationRouteIndexGrain
{
    private readonly IPersistentState<MedicationRouteIndexState> _state;

    public MedicationRouteIndexGrain(
        [PersistentState("medRouteState", "medRouteStore")]
        IPersistentState<MedicationRouteIndexState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (!_state.State.IsLoaded)
            await SeedFromVistADataAsync();

        await base.OnActivateAsync(cancellationToken);
    }

    private async Task SeedFromVistADataAsync()
    {
        _state.State.ByIen.Clear();
        _state.State.IenByName.Clear();

        foreach ((string ien, string name, string? abbreviation, string vuid) in RouteData)
        {
            MedicationRoute route = new()
            {
                Ien = ien,
                Name = name,
                Abbreviation = string.IsNullOrEmpty(abbreviation) ? null : abbreviation,
                Vuid = vuid,
                IsActive = true
            };

            _state.State.ByIen[ien] = route;
            _state.State.IenByName[name.ToUpperInvariant()] = ien;
        }

        _state.State.IsLoaded = true;
        await _state.WriteStateAsync();
    }

    public Task<List<MedicationRoute>> GetAllRoutesAsync() =>
        Task.FromResult(_state.State.ByIen.Values.OrderBy(r => r.Name).ToList());

    public Task<MedicationRoute?> GetRouteByNameAsync(string name)
    {
        if (_state.State.IenByName.TryGetValue(name.ToUpperInvariant(), out string? ien))
        {
            _state.State.ByIen.TryGetValue(ien, out MedicationRoute? route);
            return Task.FromResult(route);
        }
        return Task.FromResult<MedicationRoute?>(null);
    }

    public Task<List<MedicationRoute>> SearchAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<MedicationRoute>());

        List<MedicationRoute> results = _state.State.ByIen.Values
            .Where(r => r.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                     || (r.Abbreviation != null && r.Abbreviation.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(r => r.Name)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> IsLoadedAsync() =>
        Task.FromResult(_state.State.IsLoaded);

    // -----------------------------------------------------------------------
    // Embedded VistA data extracted from ^PS(51.23) ZWR export (OSEHRA 2018)
    // Format: (ien, name, abbreviation/alternate, vuid)
    // Source: 51.23+STANDARD MEDICATION ROUTES.zwr
    // -----------------------------------------------------------------------
    private static readonly (string Ien, string Name, string? Abbreviation, string Vuid)[] RouteData =
    [
        ("1",  "BUCCAL",           "BUCCAL",          "4706230"),
        ("2",  "DENTAL",           "DENTAL",          "4706231"),
        ("3",  "EPIDURAL",         "EPIDURAL",        "4706232"),
        ("4",  "INHALATION",       "INHALATION",      "4706234"),
        ("5",  "INTRA-ARTERIAL",   "INTRA-ARTERIAL",  "4706235"),
        ("6",  "INTRA-ARTICULAR",  "INTRA-ARTICULAR", "4706236"),
        ("7",  "INTRACARDIAC",     "INTRACARDIAC",    "4706237"),
        ("8",  "INTRACAVERNOSAL",  "INTRA-CAVERNOSAL","4706238"),
        ("9",  "INTRADERMAL",      "INTRADERMAL",     "4706239"),
        ("10", "INTRALESIONAL",    "INTRALESIONAL",   "4706240"),
        ("11", "INTRAMUSCULAR",    "INTRAMUSCULAR",   "4706241"),
        ("12", "INTRAOCULAR",      "INTRAOCULAR",     "4706242"),
        ("13", "INTRAPERITONEAL",  "INTRAPERITONEAL", "4706243"),
        ("14", "INTRAPLEURAL",     "INTRAPLEURAL",    "4706244"),
        ("15", "INTRATHECAL",      "INTRATHECAL",     "4706245"),
        ("16", "INTRATRACHEAL",    "INTRATRACHEAL",   "4706246"),
        ("17", "INTRAVENOUS",      "INTRAVENOUS",     "4706248"),
        ("18", "INTRAVESICAL",     "INTRAVESICAL",    "4706249"),
        ("19", "IRRIGATION",       "IRRIGATION",      "4706250"),
        ("20", "NEBULIZATION",     "NEBULIZATION",    "4706253"),
        ("21", "OPHTHALMIC",       "OPHTHALMIC",      "4706254"),
        ("22", "ORAL",             "ORAL",            "4500642"),
        ("23", "OTIC",             "OTIC",            "4706256"),
        ("24", "RECTAL",           "RECTAL",          "4688679"),
        ("25", "SUBCUTANEOUS",     "SUBCUTANEOUS",    "4706258"),
        ("26", "SUBLINGUAL",       "SUBLINGUAL",      "4706259"),
        ("27", "TOPICAL",          "TOPICAL",         "4706260"),
        ("28", "TRANSDERMAL",      "TRANSDERMAL",     "4706261"),
        ("29", "VAGINAL",          "VAGINAL",         "4706263"),
        ("30", "NASAL",            "INTRANASAL",      "4706252"),
        ("31", "URETHRAL",         "INTRA-URETHRAL",  "4706262"),
        ("32", "NOT APPLICABLE",   "NOT APPLICABLE",  "4706337"),
        ("33", "INTRAVITREAL",     "INTRAVITREAL",    "4706338"),
        ("34", "INTRABURSAL",      "INTRABURSAL",     "4706339"),
        ("35", "INTRASYNOVIAL",    "INTRASYNOVIAL",   "4706340"),
        ("36", "INFILTRATION",     "INFILTRATION",    "4706346"),
        ("37", "INTRACAUDAL",      "CAUDAL BLOCK",    "4706347"),
        ("38", "INTRACAVITARY",    "INTRACAVITY",     "4706348"),
        ("39", "INTRASPINAL",      "INTRASPINAL",     "4706349"),
        ("40", "INTRAUTERINE",     "INTRAUTERINE",    "4706350"),
        ("41", "RETROBULBAR",      "RETROBULBAR",     "4706351"),
        ("42", "ENTERAL",          "ORAL",            "4712338"),
        ("43", "INTRA-AMNIOTIC",   null,              "4712295"),
        ("44", "INTRADUCTAL",      "INTRADUCTAL",     "4712291"),
        ("45", "INTRATYMPANIC",    null,              "4712294"),
        ("46", "SUBCONJUNCTIVAL",  "SUBCONJUNCTIVAL", "4712290"),
        ("47", "IONTOPHORESIS",    "NOT APPLICABLE",  "4712354"),
        ("48", "INTRADETRUSOR",    "INTRADETRUSOR",   "4775778"),
        ("49", "INTRAOSSEOUS",     "INTRAOSSEOUS",    "4775781"),
        ("50", "PERIBULBAR",       "PERIBULBAR",      "4775779"),
        ("51", "SUBTENONS",        "SUB-TENON",       "4775780"),
        ("52", "INTRACATHETER",    "INTRA-CATHETER",  "5100845"),
        ("53", "TRANSLINGUAL",     "TRANSLINGUAL",    "5100846"),
        ("54", "INTRAVARICEAL",    "INTRAVARICEAL",   "5199099"),
        ("55", "SUBMUCOSAL",       "SUBMUCOSAL",      "5199100"),
    ];
}
