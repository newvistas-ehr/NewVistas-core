// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Drug interaction severity levels, mapped to VistA's severity codes
/// in the Drug Interaction files (#56-1 through #56-6).
/// Higher values represent more clinically serious interactions.
/// </summary>
[GenerateSerializer]
public enum InteractionSeverity
{
    Unknown = 0,
    Minor = 1,
    Moderate = 2,
    Significant = 3,
    Contraindicated = 4
}

/// <summary>
/// A known drug-drug interaction between two active pharmaceutical ingredients.
/// Maps to VistA Drug Interaction files (#56-1 through #56-6).
///
/// Interactions are keyed at the ingredient level (File #50.416) so that
/// every VA Product containing a flagged ingredient participates in the check,
/// regardless of dosage form or strength.
///
/// The pair is stored in order-independent form: IngredientIen1 is always
/// the lexicographically smaller IEN so that lookups by either ordering hit
/// the same dictionary key. Use DrugInteractionKeyHelper.MakePairKey to
/// produce the canonical key before any dictionary read or write.
/// </summary>
[GenerateSerializer]
public class DrugInteractionPair
{
    /// <summary>
    /// IEN of the first ingredient in File #50.416 (lexicographically smaller).
    /// </summary>
    [Id(0)]
    public string IngredientIen1 { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the first ingredient, e.g., "WARFARIN SODIUM".
    /// </summary>
    [Id(1)]
    public string IngredientName1 { get; set; } = string.Empty;

    /// <summary>
    /// IEN of the second ingredient in File #50.416 (lexicographically larger).
    /// </summary>
    [Id(2)]
    public string IngredientIen2 { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the second ingredient, e.g., "ASPIRIN".
    /// </summary>
    [Id(3)]
    public string IngredientName2 { get; set; } = string.Empty;

    /// <summary>
    /// Clinical severity of this interaction.
    /// </summary>
    [Id(4)]
    public InteractionSeverity Severity { get; set; }

    /// <summary>
    /// Brief clinical narrative describing the interaction.
    /// Mirrors the INTERACTION DESCRIPTION field in VistA.
    /// </summary>
    [Id(5)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Observed or expected clinical effects of the interaction. May be null.
    /// </summary>
    [Id(6)]
    public string? ClinicalEffects { get; set; }

    /// <summary>
    /// Pharmacodynamic or pharmacokinetic mechanism. May be null if unknown.
    /// </summary>
    [Id(7)]
    public string? Mechanism { get; set; }

    /// <summary>
    /// Clinical management guidance (monitoring, dose adjustment, alternatives).
    /// May be null if not specified.
    /// </summary>
    [Id(8)]
    public string? Management { get; set; }
}

/// <summary>
/// A drug-drug interaction found during a patient medication check.
/// Returned by IDrugInteractionCheckerGrain.CheckInteractionsAsync.
///
/// Contains the two specific DrugIngredient instances from the patient's
/// medication list that triggered the interaction, plus the underlying
/// interaction pair data (severity, description, management guidance).
/// </summary>
[GenerateSerializer]
public class DrugInteractionResult
{
    /// <summary>
    /// First drug ingredient from the patient's medication list.
    /// </summary>
    [Id(0)]
    public DrugIngredient Drug1 { get; set; } = new();

    /// <summary>
    /// Second drug ingredient from the patient's medication list.
    /// </summary>
    [Id(1)]
    public DrugIngredient Drug2 { get; set; } = new();

    /// <summary>
    /// The interaction record describing severity, effects, and management.
    /// </summary>
    [Id(2)]
    public DrugInteractionPair Interaction { get; set; } = new();
}

/// <summary>
/// Persistent state for IDrugInteractionDatasetGrain.
/// Stores all known drug-drug interaction pairs keyed by their canonical pair key.
/// </summary>
[GenerateSerializer]
public class DrugInteractionDatasetState
{
    /// <summary>
    /// All known interaction pairs, keyed by canonical pair key.
    /// Key format: "{ingredientIen1}:{ingredientIen2}" (lexicographic order).
    /// Populated by LoadInteractionsAsync; replaced entirely on each load.
    /// </summary>
    [Id(0)]
    public Dictionary<string, DrugInteractionPair> PairsByKey { get; set; } = new();

    /// <summary>
    /// Whether the dataset has been loaded with interaction data at least once.
    /// </summary>
    [Id(1)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent bulk load.
    /// </summary>
    [Id(2)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>
    /// Count of pairs currently stored. Kept in sync with PairsByKey.Count
    /// to avoid dictionary enumeration in GetStatusAsync.
    /// </summary>
    [Id(3)]
    public int TotalPairs { get; set; }

    /// <summary>
    /// Monotonic dataset version, incremented on every LoadInteractionsAsync
    /// and ClearAsync. Checker grains compare their cached snapshot version
    /// against this to decide whether to re-pull (never time-based).
    /// </summary>
    [Id(4)]
    public long Version { get; set; }
}

/// <summary>
/// Lightweight version probe for the interaction dataset. Returned by
/// IDrugInteractionDatasetGrain.GetVersionAsync — one tiny payload per
/// interaction check so StatelessWorker checkers can validate their
/// silo-local cache without shipping the full pair dictionary.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DrugInteractionDatasetVersion
{
    /// <summary>Current monotonic dataset version.</summary>
    [Id(0)]
    public long Version { get; init; }

    /// <summary>Whether the dataset has been loaded with interaction data.</summary>
    [Id(1)]
    public bool IsLoaded { get; init; }
}

/// <summary>
/// Full versioned copy of the interaction dataset. Returned by
/// IDrugInteractionDatasetGrain.GetSnapshotAsync when a checker's
/// silo-local cache is missing or stale (pull-through population).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record DrugInteractionSnapshot
{
    /// <summary>Dataset version this snapshot represents.</summary>
    [Id(0)]
    public long Version { get; init; }

    /// <summary>Whether the dataset has been loaded with interaction data.</summary>
    [Id(1)]
    public bool IsLoaded { get; init; }

    /// <summary>All interaction pairs keyed by canonical pair key.</summary>
    [Id(2)]
    public Dictionary<string, DrugInteractionPair> PairsByKey { get; init; } = new();
}

/// <summary>
/// Status of an interaction check. DataUnavailable means the interaction
/// dataset has not been loaded — callers MUST fail closed (block the
/// fill/order), never treat it as "no interactions found".
/// </summary>
[GenerateSerializer]
public enum DrugInteractionCheckStatus
{
    /// <summary>Check ran against loaded data; Results is authoritative.</summary>
    Ok = 0,

    /// <summary>Interaction dataset not loaded — interactions could NOT be verified.</summary>
    DataUnavailable = 1
}

/// <summary>
/// Envelope returned by IDrugInteractionCheckerGrain.CheckInteractionsAsync.
/// Forces every caller to distinguish "no interactions found" (Ok + empty
/// Results) from "could not check" (DataUnavailable) — the latter blocks.
/// </summary>
[GenerateSerializer]
public sealed class DrugInteractionCheckResponse
{
    /// <summary>Whether the check ran against loaded interaction data.</summary>
    [Id(0)]
    public DrugInteractionCheckStatus Status { get; set; }

    /// <summary>Interactions found. Only meaningful when Status is Ok.</summary>
    [Id(1)]
    public List<DrugInteractionResult> Results { get; set; } = new();

    /// <summary>Dataset version the check ran against (0 when unavailable).</summary>
    [Id(2)]
    public long DatasetVersion { get; set; }
}

/// <summary>
/// Status snapshot for the drug interaction dataset.
/// Returned by IDrugInteractionDatasetGrain.GetStatusAsync.
/// </summary>
[GenerateSerializer]
public class DrugInteractionStatus
{
    /// <summary>Whether the dataset has ever been loaded.</summary>
    [Id(0)]
    public bool IsLoaded { get; set; }

    /// <summary>UTC timestamp of the last successful load.</summary>
    [Id(1)]
    public DateTime? LastLoadedDate { get; set; }

    /// <summary>Number of interaction pairs currently in the dataset.</summary>
    [Id(2)]
    public int TotalPairs { get; set; }

    /// <summary>
    /// Whether interaction data is available to checkers. With pull-through
    /// caching this mirrors IsLoaded (checkers self-populate on any silo);
    /// retained for DTO compatibility.
    /// </summary>
    [Id(3)]
    public bool IsCachePopulated { get; set; }

    /// <summary>Current monotonic dataset version.</summary>
    [Id(4)]
    public long Version { get; set; }
}
