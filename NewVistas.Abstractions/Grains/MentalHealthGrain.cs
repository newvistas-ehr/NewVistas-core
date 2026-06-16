// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.MentalHealth;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Mental Health Grain implementation based on VistA YS MH INSTRUMENT (#601.71)
/// </summary>
public class MentalHealthGrain : Grain, IMentalHealthGrain
{
    private readonly IPersistentState<MentalHealthState> _state;

    public MentalHealthGrain(
        [PersistentState("mentalHealthState", "mentalHealthStore")] IPersistentState<MentalHealthState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstrumentId))
        {
            _state.State.InstrumentId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<MentalHealthState> GetInstrumentAsync() => Task.FromResult(_state.State);

    public async Task RecordInstrumentAsync(
        string patientId, string instrumentName, string? instrumentDefId,
        DateTime administrationDateTime, decimal? totalScore,
        string? scoreInterpretation, bool? isPositiveScreen,
        Dictionary<string, string>? responses,
        string? administeredById, string? administeredByName,
        string? orderingProviderId, string? orderingProviderName,
        string? locationId, string? locationName,
        string? visitId, string? comments)
    {
        // Idempotent: re-issued record on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.InstrumentName = instrumentName;
        _state.State.InstrumentDefId = instrumentDefId;
        _state.State.AdministrationDateTime = administrationDateTime;
        _state.State.TotalScore = totalScore;
        _state.State.ScoreInterpretation = scoreInterpretation;
        _state.State.IsPositiveScreen = isPositiveScreen;
        if (responses != null)
            _state.State.Responses = responses;
        _state.State.AdministeredById = administeredById;
        _state.State.AdministeredByName = administeredByName;
        _state.State.OrderingProviderId = orderingProviderId;
        _state.State.OrderingProviderName = orderingProviderName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.VisitId = visitId;
        _state.State.Comments = comments;
        _state.State.Status = "COMPLETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new MentalHealthRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            InstrumentId = _state.State.InstrumentId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CancelAsync()
    {
        _state.State.Status = "CANCELLED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordRiskAssessmentAsync(int riskLevel, string? riskNotes)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;

        _state.State.RiskLevel = riskLevel;
        _state.State.RiskAssessmentNotes = riskNotes;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new MentalHealthRiskAssessedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            InstrumentId = _state.State.InstrumentId,
            RiskLevel = riskLevel,
            RiskNotes = riskNotes
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task SetFollowUpAsync(bool requiresFollowUp, DateTime? followUpDueDate, string? followUpPlan)
    {
        _state.State.RequiresFollowUp = requiresFollowUp;
        _state.State.FollowUpDueDate = followUpDueDate;
        _state.State.FollowUpPlan = followUpPlan;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddItemResponseAsync(int itemNumber, string questionText, int responseValue, string? responseText)
    {
        MhItemResponse existing = _state.State.ItemResponses
            .FirstOrDefault(r => r.ItemNumber == itemNumber)!;
        if (existing != null)
        {
            existing.QuestionText = questionText;
            existing.ResponseValue = responseValue;
            existing.ResponseText = responseText;
        }
        else
        {
            _state.State.ItemResponses.Add(new MhItemResponse
            {
                ItemNumber = itemNumber,
                QuestionText = questionText,
                ResponseValue = responseValue,
                ResponseText = responseText
            });
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ScoreInstrumentAsync()
    {
        decimal totalScore = _state.State.ItemResponses.Sum(r => r.ResponseValue);
        _state.State.TotalScore = totalScore;
        _state.State.ScoringMethodUsed = "AUTO";

        string instrumentName = _state.State.InstrumentName.ToUpperInvariant();
        string? interpretation = null;
        bool? isPositive = null;
        string? recommendation = null;

        // Try to look up from the instrument library grain
        IMentalHealthInstrumentLibraryGrain library = GrainFactory.GetGrain<IMentalHealthInstrumentLibraryGrain>("MH-INSTRUMENTS");
        GrainStates.MhInstrumentDefinition? definition = await library.GetInstrumentAsync(_state.State.InstrumentName);

        if (definition != null && definition.ScoreRanges.Count > 0)
        {
            foreach (GrainStates.MhScoreRange range in definition.ScoreRanges)
            {
                if (totalScore >= range.MinScore && totalScore <= range.MaxScore)
                {
                    interpretation = range.Interpretation;
                    recommendation = range.Recommendation;
                    break;
                }
            }
            isPositive = totalScore >= definition.PositiveScreenThreshold;
        }
        else
        {
            // Fallback defaults for known instruments
            switch (instrumentName)
            {
                case "PHQ-9":
                    isPositive = totalScore >= 10;
                    interpretation = totalScore switch
                    {
                        <= 4 => "MINIMAL",
                        <= 9 => "MILD",
                        <= 14 => "MODERATE",
                        <= 19 => "MODERATELY SEVERE",
                        _ => "SEVERE"
                    };
                    recommendation = totalScore switch
                    {
                        <= 4 => "No treatment indicated; monitor",
                        <= 9 => "Watchful waiting; repeat PHQ-9 at follow-up",
                        <= 14 => "Consider counseling or pharmacotherapy",
                        <= 19 => "Active treatment with pharmacotherapy and/or psychotherapy",
                        _ => "Immediate initiation of pharmacotherapy and psychotherapy; consider referral to specialist"
                    };
                    break;

                case "GAD-7":
                    isPositive = totalScore >= 10;
                    interpretation = totalScore switch
                    {
                        <= 4 => "MINIMAL",
                        <= 9 => "MILD",
                        <= 14 => "MODERATE",
                        _ => "SEVERE"
                    };
                    recommendation = totalScore switch
                    {
                        <= 4 => "No treatment indicated; monitor",
                        <= 9 => "Watchful waiting; repeat GAD-7 at follow-up",
                        <= 14 => "Consider counseling or pharmacotherapy",
                        _ => "Active treatment with pharmacotherapy and/or psychotherapy"
                    };
                    break;

                case "AUDIT-C":
                    isPositive = totalScore >= 4;
                    interpretation = totalScore < 4 ? "NEGATIVE" : "POSITIVE";
                    recommendation = totalScore < 4
                        ? "Low-risk drinking; no intervention needed"
                        : "Positive screen; conduct full AUDIT and brief intervention";
                    break;

                case "PCL-5":
                    isPositive = totalScore >= 31;
                    interpretation = totalScore < 31 ? "SUBTHRESHOLD" : "PROBABLE PTSD";
                    recommendation = totalScore < 31
                        ? "Below threshold; monitor symptoms"
                        : "Probable PTSD; refer for diagnostic evaluation and evidence-based treatment";
                    break;

                case "COLUMBIA SUICIDE SEVERITY":
                case "COLUMBIA":
                case "C-SSRS":
                    isPositive = _state.State.ItemResponses.Any(r => r.ItemNumber >= 1 && r.ItemNumber <= 5 && r.ResponseValue > 0);
                    interpretation = isPositive == true ? "POSITIVE" : "NEGATIVE";
                    recommendation = isPositive == true
                        ? "Positive suicide screen; initiate safety planning and mental health referral"
                        : "No current suicidal ideation identified";
                    break;
            }
        }

        _state.State.ScoreInterpretation = interpretation;
        _state.State.IsPositiveScreen = isPositive;
        _state.State.ClinicalRecommendation = recommendation;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(_state.State.PatientId))
        {
            var evt = new MentalHealthScoredV1
            {
                EventId = $"CEV-{Guid.NewGuid()}",
                PatientId = _state.State.PatientId,
                OccurredUtc = DateTime.UtcNow,
                UserId = CurrentUserId,
                UserName = CurrentUserName,
                InstrumentId = _state.State.InstrumentId,
                TotalScore = totalScore,
                ScoreInterpretation = interpretation,
                IsPositiveScreen = isPositive,
                ScoringMethod = "AUTO"
            };
            _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));
        }

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task SetPreviousScoreAsync(decimal previousScore, DateTime previousDate)
    {
        _state.State.PreviousScore = previousScore;
        _state.State.PreviousAdministrationDate = previousDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<decimal?> CalculateScoreChangeAsync()
    {
        if (_state.State.TotalScore.HasValue && _state.State.PreviousScore.HasValue)
        {
            return Task.FromResult<decimal?>(_state.State.TotalScore.Value - _state.State.PreviousScore.Value);
        }
        return Task.FromResult<decimal?>(null);
    }
}
