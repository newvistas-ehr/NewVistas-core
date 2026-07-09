// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Analytics worker (grain key <c>PROTO-ANALYTICS:{protoConditionId}</c>). [StatelessWorker]: reads
/// the proto and assembles each symptom feature's background prevalence from the assessed-population
/// cohort shards (falling back to the curated catalog when the assessed population is too thin to
/// estimate), then runs the pure <see cref="ProtoConditionAnalytics"/> engine.
/// </summary>
[StatelessWorker]
public class ProtoAnalyticsGrain : Grain, IProtoAnalyticsGrain
{
    /// <summary>Below this many assessed patients, the live rate is too noisy — fall back to the catalog.</summary>
    private const int MinBackgroundAssessed = 10;

    private readonly IGrainFactory _grainFactory;

    public ProtoAnalyticsGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<ProtoAnalyticsReport> AnalyzeAsync()
    {
        string key = this.GetPrimaryKeyString();
        int colon = key.IndexOf(':');
        string protoId = colon >= 0 ? key[(colon + 1)..] : key;

        ProtoConditionState proto = await _grainFactory
            .GetGrain<IProtoConditionGrain>($"PROTO:{protoId}").GetAsync();

        var backgrounds = new List<BackgroundRate>();
        foreach (ProtoFeature f in proto.Features.Where(f => f.Kind == ProtoFeatureKind.Symptom))
        {
            BackgroundRate rate = await SymptomBackgroundAsync(f);
            backgrounds.Add(rate);
        }

        return ProtoConditionAnalytics.Analyze(proto, backgrounds);
    }

    private async Task<BackgroundRate> SymptomBackgroundAsync(ProtoFeature f)
    {
        ISymptomCohortIndexGrain shard =
            _grainFactory.GetGrain<ISymptomCohortIndexGrain>($"SYMPTOM-COHORT:{f.Code}");
        int assessed = await shard.GetAssessedCountAsync();
        int present = await shard.GetPresentCountAsync();

        if (assessed >= MinBackgroundAssessed)
        {
            return new BackgroundRate
            {
                FeatureId = f.FeatureId,
                Rate = (double)present / assessed,
                Source = $"assessed population (n={assessed})"
            };
        }

        // Too thin to estimate live — use the curated catalog fallback, labeled as such.
        return new BackgroundRate
        {
            FeatureId = f.FeatureId,
            Rate = SymptomCatalog.BackgroundPrevalenceFor(f.Code),
            Source = assessed == 0
                ? "curated catalog (no assessed population)"
                : $"curated catalog (assessed n={assessed} too thin)"
        };
    }
}
