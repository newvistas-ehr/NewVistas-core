// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Learned outcome counters for one diagnosis at one granularity for one assertion year
/// (ADR-006). Grain key <c>DX-OUTCOME:{granularity}:{codeKey}:{yyyy}</c>.
///
/// The key is safely splittable because <see cref="DiagnosisCodeRelation.Normalize"/> strips
/// every non-alphanumeric character from the code — unlike the payer/procedure shard, which
/// needed a last-colon trick because payer ids may contain colons.
/// </summary>
public class DiagnosisOutcomeIndexGrain : Grain, IDiagnosisOutcomeIndexGrain
{
    public const string KeyPrefix = "DX-OUTCOME:";

    private readonly IPersistentState<DiagnosisOutcomeState> _state;

    public DiagnosisOutcomeIndexGrain(
        [PersistentState("dxOutcomeState", "dxOutcomeStore")]
        IPersistentState<DiagnosisOutcomeState> state)
    {
        _state = state;
    }

    /// <summary>Build the grain key for a granularity, code and assertion year.</summary>
    public static string KeyFor(DiagnosisCodeGranularity granularity, string codeKey, int year)
        => $"{KeyPrefix}{granularity switch
        {
            DiagnosisCodeGranularity.Code => "CODE",
            DiagnosisCodeGranularity.Category => "CAT",
            _ => "ALL"
        }}:{(granularity == DiagnosisCodeGranularity.All ? "ALL" : codeKey)}:{year:D4}";

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.AssertionYear == 0)
        {
            string key = this.GetPrimaryKeyString();
            string body = key.StartsWith(KeyPrefix, StringComparison.Ordinal)
                ? key[KeyPrefix.Length..]
                : key;
            string[] parts = body.Split(':');
            if (parts.Length == 3)
            {
                _state.State.Granularity = parts[0] switch
                {
                    "CODE" => DiagnosisCodeGranularity.Code,
                    "CAT" => DiagnosisCodeGranularity.Category,
                    _ => DiagnosisCodeGranularity.All
                };
                _state.State.CodeKey = parts[1];
                _ = int.TryParse(parts[2], out int y);
                _state.State.AssertionYear = y;
            }
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordAssertionAsync()
    {
        _state.State.AssertedCount++;
        _state.State.LastRecordedUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAdjudicationAsync(
        DiagnosticEpisodeOutcome outcome,
        string? outcomeCode, string? outcomeDisplay, string? outcomeNote,
        List<string> newEvidenceKeys, List<string> abnormalKeys,
        List<string> presentAtAssertionKeys,
        string? adjudicatingProviderId, bool advisoryWasShown, DateTime occurredUtc)
    {
        DiagnosisOutcomeState s = _state.State;

        // Open never reaches here. ClosedUnadjudicated is counted but stays out of the
        // denominator — it is what makes adjudication coverage computable.
        if (outcome == DiagnosticEpisodeOutcome.ClosedUnadjudicated)
        {
            s.ClosedUnadjudicatedCount++;
            s.LastRecordedUtc = occurredUtc;
            await _state.WriteStateAsync();
            return;
        }

        // A recode is not a clinical adjudication at all — the code set moved, nobody was
        // wrong, and nobody declined to answer. It leaves numerator, denominator AND the
        // coverage ratio untouched, so a mass recode cannot move any reported number.
        if (outcome == DiagnosticEpisodeOutcome.Recoded)
        {
            s.RecodedCount++;
            s.LastRecordedUtc = occurredUtc;
            await _state.WriteStateAsync();
            return;
        }

        s.AdjudicatedCount++;
        if (!advisoryWasShown)
        {
            s.AdjudicatedUnexposedCount++;
            s.LastUnexposedRecordedUtc = occurredUtc;
        }

        switch (outcome)
        {
            case DiagnosticEpisodeOutcome.Confirmed:
                s.ConfirmedCount++;
                break;

            case DiagnosticEpisodeOutcome.Revised:
                s.RevisedCount++;
                if (!advisoryWasShown) s.RevisedUnexposedCount++;
                if (DiagnosisCodeRelation.IsUnspecified(outcomeCode)) s.NosTerminatingRevisedCount++;
                RecordAlternative(s, outcomeCode, outcomeDisplay, advisoryWasShown, occurredUtc);
                break;

            case DiagnosticEpisodeOutcome.Refined:
                // Adjudicated, verdict "not an error": in the denominator, out of the numerator.
                // This deflates the reported rate, which is the safe direction.
                s.RefinedCount++;
                break;

            case DiagnosticEpisodeOutcome.Broadened:
                s.BroadenedCount++;
                break;

            case DiagnosticEpisodeOutcome.ResolvedWithoutAlternate:
                s.ResolvedWithoutAlternateCount++;
                break;
        }

        if (!string.IsNullOrWhiteSpace(adjudicatingProviderId))
            s.AdjudicatingProviderIds.Add(adjudicatingProviderId);

        if (string.IsNullOrEmpty(outcomeCode) && !string.IsNullOrWhiteSpace(outcomeNote))
            RecordUnmapped(s, outcomeNote, occurredUtc);

        RecordDiscriminators(
            s, outcome, newEvidenceKeys, abnormalKeys, presentAtAssertionKeys,
            advisoryWasShown, occurredUtc);

        s.LastRecordedUtc = occurredUtc;
        await _state.WriteStateAsync();
    }

    public Task<DiagnosisOutcomeState> GetStateAsync() => Task.FromResult(_state.State);

    private static void RecordAlternative(
        DiagnosisOutcomeState s, string? code, string? display, bool exposed, DateTime utc)
    {
        string key = DiagnosisCodeRelation.Normalize(code);
        if (key.Length == 0) return;

        DiagnosisRevisionStat? stat = s.RevisedTo.FirstOrDefault(r => r.OutcomeCode == key);
        if (stat is null)
        {
            stat = new DiagnosisRevisionStat { OutcomeCode = key, OutcomeDisplay = display ?? key };
            s.RevisedTo.Add(stat);
        }
        stat.Count++;
        if (!exposed) stat.CountUnexposed++;
        stat.LastSeenUtc = utc;
    }

    private static void RecordUnmapped(DiagnosisOutcomeState s, string text, DateTime utc)
    {
        // Kept rather than dropped: an accumulating pile of unmapped outcomes is itself a
        // finding — the coding vocabulary is missing something clinicians keep needing.
        UnmappedOutcomeNote? note = s.UnmappedOutcomes.FirstOrDefault(u => u.Text == text);
        if (note is null)
        {
            note = new UnmappedOutcomeNote { Text = text };
            s.UnmappedOutcomes.Add(note);
        }
        note.Count++;
        note.LastSeenUtc = utc;
    }

    private static void RecordDiscriminators(
        DiagnosisOutcomeState s, DiagnosticEpisodeOutcome outcome,
        List<string>? newKeys, List<string>? abnormalKeys, List<string>? presentKeys,
        bool exposed, DateTime utc)
    {
        bool revised = outcome == DiagnosticEpisodeOutcome.Revised;
        var abnormal = new HashSet<string>(abnormalKeys ?? new(), StringComparer.Ordinal);

        foreach (string key in (newKeys ?? new()).Distinct(StringComparer.Ordinal))
        {
            DiscriminatorStat stat = Find(s, key);

            if (revised)
            {
                stat.NewInRevised++;
                if (!exposed) stat.NewInRevisedUnexposed++;
                if (abnormal.Contains(key))
                {
                    stat.NewAndAbnormalInRevised++;
                    if (!exposed) stat.NewAndAbnormalInRevisedUnexposed++;
                }
            }
            else
            {
                // The comparison arm. Counting only the revised arm would learn that the CBC
                // — which everyone gets — diagnoses everything.
                stat.NewInNotRevised++;
                if (!exposed) stat.NewInNotRevisedUnexposed++;
                if (abnormal.Contains(key))
                {
                    stat.NewAndAbnormalInNotRevised++;
                    if (!exposed) stat.NewAndAbnormalInNotRevisedUnexposed++;
                }
            }
            stat.LastSeenUtc = utc;
        }

        // A test the clinician already had at assertion is not advice worth giving.
        foreach (string key in (presentKeys ?? new()).Distinct(StringComparer.Ordinal))
        {
            DiscriminatorStat stat = Find(s, key);
            stat.AlreadyPresentAtAssertion++;
            stat.LastSeenUtc = utc;
        }
    }

    private static DiscriminatorStat Find(DiagnosisOutcomeState s, string key)
    {
        DiscriminatorStat? stat = s.Discriminators.FirstOrDefault(d => d.TestKey == key);
        if (stat is null)
        {
            stat = new DiscriminatorStat
            {
                TestKey = key,
                Kind = key.Length > 1 ? key[0] switch
                {
                    'L' => DiagnosticTestKind.Lab,
                    'R' => DiagnosticTestKind.Imaging,
                    'C' => DiagnosticTestKind.Consult,
                    'E' => DiagnosticTestKind.Exam,
                    _ => DiagnosticTestKind.Unspecified
                } : DiagnosticTestKind.Unspecified,
                Display = key.Length > 2 ? key[2..] : key
            };
            s.Discriminators.Add(stat);
        }
        return stat;
    }
}
