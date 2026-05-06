// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index grain for Dose Units (VistA File #51.24).
/// Grain key: "DOSE-UNIT-INDEX"
///
/// Self-seeding: on first activation, loads all 54 dose units from the embedded
/// VistA ZWR data. The static array was extracted verbatim from
/// "51.24+DOSE UNITS.zwr" (OSEHRA export, Nov 2018).
///
/// Format per entry in ZWR: ^PS(51.24,{ien},0)="{NAME}^{ABBREVIATION}^{IsCounting}"
/// where IsCounting=1 means discrete dispensable unit; 0 = continuous measurement.
/// </summary>
public class DoseUnitIndexGrain : Grain, IDoseUnitIndexGrain
{
    private readonly IPersistentState<DoseUnitIndexState> _state;

    public DoseUnitIndexGrain(
        [PersistentState("doseUnitState", "doseUnitStore")]
        IPersistentState<DoseUnitIndexState> state)
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

        foreach ((string ien, string name, string abbreviation, bool isCounting) in UnitData)
        {
            DoseUnit unit = new()
            {
                Ien = ien,
                Name = name,
                Abbreviation = abbreviation,
                IsCounting = isCounting
            };

            _state.State.ByIen[ien] = unit;
            _state.State.IenByName[name.ToUpperInvariant()] = ien;
        }

        _state.State.IsLoaded = true;
        await _state.WriteStateAsync();
    }

    public Task<List<DoseUnit>> GetAllUnitsAsync() =>
        Task.FromResult(_state.State.ByIen.Values.OrderBy(u => u.Name).ToList());

    public Task<DoseUnit?> GetUnitByNameAsync(string name)
    {
        if (_state.State.IenByName.TryGetValue(name.ToUpperInvariant(), out string? ien))
        {
            _state.State.ByIen.TryGetValue(ien, out DoseUnit? unit);
            return Task.FromResult(unit);
        }
        return Task.FromResult<DoseUnit?>(null);
    }

    public Task<List<DoseUnit>> SearchAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return Task.FromResult(new List<DoseUnit>());

        List<DoseUnit> results = _state.State.ByIen.Values
            .Where(u => u.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                     || u.Abbreviation.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(u => u.Name)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> IsLoadedAsync() =>
        Task.FromResult(_state.State.IsLoaded);

    // -----------------------------------------------------------------------
    // Embedded VistA data extracted from ^PS(51.24) ZWR export (OSEHRA 2018)
    // Format: (ien, name, abbreviation, isCounting)
    // Source: 51.24+DOSE UNITS.zwr — ^PS(51.24,{ien},0)="{NAME}^{ABBR}^{0|1}"
    // -----------------------------------------------------------------------
    private static readonly (string Ien, string Name, string Abbreviation, bool IsCounting)[] UnitData =
    [
        ("1",  "APPLICATION(S)",      "APPLICATION(S)",   true),
        ("2",  "APPLICATORFUL(S)",    "APPLICATORFUL",    true),
        ("3",  "anti-Xa unit",        "AXA IU",           false),
        ("4",  "BAR(S)",              "BARS",             true),
        ("5",  "CAP/TAB",             "TAB-CAPS",         true),
        ("6",  "CAPLET(S)",           "CAPLETS",          true),
        ("7",  "CAPSULE(S)",          "CAPSULE(S)",       true),
        ("8",  "CENTIMETER(S)",       "CENTIMETERS",      true),
        ("9",  "DROP(S)",             "DROP(S)",          true),
        ("10", "EACH",                "EACH",             true),
        ("11", "ENEMA(S)",            "ENEMAS",           true),
        ("12", "GRAM(S)",             "GRAMS",            false),
        ("13", "INCH(ES)",            "INCH(ES)",         true),
        ("14", "INHALATION(S)",       "INHALATIONS",      true),
        ("15", "INSERT(S)",           "INSERTS",          true),
        ("16", "LITER(S)",            "LITERS",           true),
        ("17", "LOZENGE(S)",          "LOZENGES",         true),
        ("18", "MICROGRAM(S)",        "MICROGRAM(S)",     false),
        ("19", "MILLIGRAM(S)",        "MILLIGRAMS",       false),
        ("20", "MG-PE",               "MG PE",            false),
        ("21", "MICRO UNIT(S)",       "MICRO UNITS",      false),
        ("22", "MILLIEQUIVALENT(S)",  "MILLIEQUIVALENTS", false),
        ("23", "MILLIONUNIT(S)",      "MILLIONUNIT(S)",   false),
        ("24", "MILLILITER(S)",       "MILLILITERS",      true),
        ("25", "MILLIMOLE(S)",        "MILLIMOLES",       false),
        ("26", "NANOGRAM(S)",         "NANOGRAMS",        false),
        ("27", "OVULE(S)",            "OVULE(S)",         true),
        ("28", "PACKAGE(S)",          "PACKAGES",         true),
        ("29", "PACKET(S)",           "PACKETS",          true),
        ("30", "PAD(S)",              "PADS",             true),
        ("31", "PATCH(ES)",           "PATCHES",          true),
        ("32", "PELLET(S)",           "PELLETS",          true),
        ("33", "PIECE(S)",            "PIECE(S)",         true),
        ("34", "PUFF(S)",             "PUFF(S)",          true),
        ("35", "SACHET(S)",           "SACHETS",          true),
        ("36", "SCOOPFUL(S)",         "SCOOPFULS",        true),
        ("37", "SPRAY(S)",            "SPRAY(S)",         true),
        ("38", "SQUIRT(S)",           "SQUIRTS",          true),
        ("39", "STRIP(S)",            "STRIP(S)",         true),
        ("40", "SUPPOSITORY(IES)",    "SUPPOSITORY",      true),
        ("41", "TABLESPOONFUL(S)",    "TABLESPOONFULS",   true),
        ("42", "TABLET(S)",           "TABLET(S)",        true),
        ("43", "TEASPOONFUL(S)",      "TEASPOONFULS",     true),
        ("44", "TROCHE(S)",           "TROCHES",          true),
        ("45", "THOUSAND UNITS",      "TU",               false),
        ("46", "UNIT(S)",             "UNIT(S)",          false),
        ("47", "VAGINAL INSERT",      "VAGINAL INSERT",   true),
        ("48", "VAGINAL RING",        "VAGINAL RING",     true),
        ("49", "VIAL(S)",             "VIALS",            true),
        ("50", "WAFER(S)",            "WAFERS",           true),
        ("51", "IMPLANT(S)",          "IMPLANTS",         true),
        ("52", "FILM(S)",             "FILMS",            true),
        ("53", "ELISA UNIT(S)",       "ELISA UNIT",       false),
        ("54", "PUMP(S)",             "PUMPS",            true),
    ];
}
