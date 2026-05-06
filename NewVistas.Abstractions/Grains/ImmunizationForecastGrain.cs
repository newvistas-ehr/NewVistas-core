// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Optional feature grain for immunization forecasting.
/// Evaluates a patient's immunization history against ACIP vaccine schedules
/// and generates recommendations for due/overdue/upcoming vaccinations.
///
/// Maps to IHS RPMS Immunization Forecasting module (BI FORECAST RPCs).
/// Keyed by "IMM-FORECAST:{patientId}".
///
/// On first activation, seeds the standard ACIP childhood/adult schedule.
/// Sites can customize via AddOrUpdateSeriesDefinitionAsync.
/// </summary>
public class ImmunizationForecastGrain : Grain, IImmunizationForecastGrain
{
    private readonly IPersistentState<ImmunizationForecastState> _state;

    public ImmunizationForecastGrain(
        [PersistentState("immunizationForecastState", "immunizationForecastStore")]
        IPersistentState<ImmunizationForecastState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }

        // Seed default ACIP schedule if empty
        if (_state.State.Schedule.Count == 0)
            SeedDefaultSchedule();

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ImmunizationForecastState> GetForecastStateAsync()
        => Task.FromResult(_state.State);

    public Task<List<VaccineSeriesDefinition>> GetScheduleAsync()
        => Task.FromResult(_state.State.Schedule);

    public async Task AddOrUpdateSeriesDefinitionAsync(VaccineSeriesDefinition definition)
    {
        _state.State.Schedule.RemoveAll(s => s.VaccineGroup == definition.VaccineGroup);
        _state.State.Schedule.Add(definition);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveSeriesDefinitionAsync(string vaccineGroup)
    {
        _state.State.Schedule.RemoveAll(s => s.VaccineGroup == vaccineGroup);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<ImmunizationForecastResult> GenerateForecastAsync(
        DateTime patientDateOfBirth,
        List<ImmunizationEntry> immunizationHistory)
    {
        DateTime today = DateTime.UtcNow.Date;
        int ageMonths = CalculateAgeInMonths(patientDateOfBirth, today);
        var recommendations = new List<ForecastRecommendation>();

        foreach (VaccineSeriesDefinition series in _state.State.Schedule)
        {
            // Check age eligibility
            if (series.MinAgeMonths > 0 && ageMonths < series.MinAgeMonths)
            {
                recommendations.Add(new ForecastRecommendation
                {
                    VaccineGroup = series.VaccineGroup,
                    Status = "NOT_RECOMMENDED",
                    DosesReceived = 0,
                    DosesRequired = series.DosesRequired,
                    StatusReason = $"Patient is {ageMonths} months old; minimum age is {series.MinAgeMonths} months."
                });
                continue;
            }

            if (series.MaxAgeMonths > 0 && ageMonths > series.MaxAgeMonths)
            {
                recommendations.Add(new ForecastRecommendation
                {
                    VaccineGroup = series.VaccineGroup,
                    Status = "NOT_RECOMMENDED",
                    DosesReceived = 0,
                    DosesRequired = series.DosesRequired,
                    StatusReason = $"Patient is {ageMonths} months old; maximum age is {series.MaxAgeMonths} months."
                });
                continue;
            }

            // Find doses received for this series (match by CVX code or vaccine group name)
            List<ImmunizationEntry> seriesDoses = immunizationHistory
                .Where(imm =>
                    (imm.CvxCode != null && series.CvxCodes.Contains(imm.CvxCode)) ||
                    string.Equals(imm.VaccineGroupName, series.VaccineGroup, StringComparison.OrdinalIgnoreCase))
                .Where(imm => !imm.IsContraindicated)
                .OrderBy(imm => imm.EventDateTime)
                .ToList();

            // Check for contraindications
            bool hasContraindication = immunizationHistory
                .Any(imm =>
                    imm.IsContraindicated &&
                    ((imm.CvxCode != null && series.CvxCodes.Contains(imm.CvxCode)) ||
                     string.Equals(imm.VaccineGroupName, series.VaccineGroup, StringComparison.OrdinalIgnoreCase)));

            if (hasContraindication)
            {
                recommendations.Add(new ForecastRecommendation
                {
                    VaccineGroup = series.VaccineGroup,
                    Status = "CONTRAINDICATED",
                    DosesReceived = seriesDoses.Count,
                    DosesRequired = series.DosesRequired,
                    LastDoseDate = seriesDoses.LastOrDefault()?.EventDateTime,
                    StatusReason = "Contraindication recorded."
                });
                continue;
            }

            // Annual vaccines (e.g., Influenza)
            if (series.IsAnnual)
            {
                ForecastRecommendation annualRec = EvaluateAnnualSeries(series, seriesDoses, today);
                recommendations.Add(annualRec);
                continue;
            }

            // Multi-dose series evaluation
            ForecastRecommendation rec = EvaluateMultiDoseSeries(series, seriesDoses, patientDateOfBirth, today);
            recommendations.Add(rec);
        }

        // Store the forecast
        _state.State.Recommendations = recommendations;
        _state.State.LastForecastDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new ImmunizationForecastResult
        {
            Success = true,
            Recommendations = recommendations,
            ForecastDate = DateTime.UtcNow,
            TotalDue = recommendations.Count(r => r.Status == "DUE"),
            TotalOverdue = recommendations.Count(r => r.Status == "OVERDUE"),
            TotalComplete = recommendations.Count(r => r.Status == "COMPLETE")
        };
    }

    // ── Evaluation Logic ────────────────────────────────────────────────

    private static ForecastRecommendation EvaluateMultiDoseSeries(
        VaccineSeriesDefinition series,
        List<ImmunizationEntry> seriesDoses,
        DateTime dateOfBirth,
        DateTime today)
    {
        int dosesReceived = seriesDoses.Count;

        // Series complete
        if (dosesReceived >= series.DosesRequired)
        {
            return new ForecastRecommendation
            {
                VaccineGroup = series.VaccineGroup,
                Status = "COMPLETE",
                DosesReceived = dosesReceived,
                DosesRequired = series.DosesRequired,
                LastDoseDate = seriesDoses.Last().EventDateTime,
                StatusReason = "Series complete."
            };
        }

        // Calculate recommended and earliest dates for next dose
        DateTime earliestDate;
        DateTime recommendedDate;

        if (dosesReceived == 0)
        {
            // First dose — based on minimum age
            earliestDate = dateOfBirth.AddMonths(series.MinAgeMonths);
            recommendedDate = earliestDate;
        }
        else
        {
            DateTime lastDose = seriesDoses.Last().EventDateTime;
            int intervalIndex = dosesReceived - 1;

            int minIntervalDays = intervalIndex < series.MinIntervalDays.Count
                ? series.MinIntervalDays[intervalIndex] : 28;
            int recIntervalDays = intervalIndex < series.RecommendedIntervalDays.Count
                ? series.RecommendedIntervalDays[intervalIndex] : minIntervalDays;

            earliestDate = lastDose.AddDays(minIntervalDays);
            recommendedDate = lastDose.AddDays(recIntervalDays);
        }

        // Determine status
        string status;
        string reason;

        if (today >= recommendedDate.AddDays(28))
        {
            status = "OVERDUE";
            int daysOverdue = (int)(today - recommendedDate).TotalDays;
            reason = $"Dose {dosesReceived + 1} of {series.DosesRequired} is {daysOverdue} days overdue.";
        }
        else if (today >= earliestDate)
        {
            status = "DUE";
            reason = $"Dose {dosesReceived + 1} of {series.DosesRequired} is due.";
        }
        else
        {
            status = "UPCOMING";
            reason = $"Dose {dosesReceived + 1} of {series.DosesRequired} earliest on {earliestDate:yyyy-MM-dd}.";
        }

        return new ForecastRecommendation
        {
            VaccineGroup = series.VaccineGroup,
            Status = status,
            DosesReceived = dosesReceived,
            DosesRequired = series.DosesRequired,
            EarliestDate = earliestDate,
            RecommendedDate = recommendedDate,
            LastDoseDate = seriesDoses.LastOrDefault()?.EventDateTime,
            StatusReason = reason
        };
    }

    private static ForecastRecommendation EvaluateAnnualSeries(
        VaccineSeriesDefinition series,
        List<ImmunizationEntry> seriesDoses,
        DateTime today)
    {
        // For annual vaccines, check if a dose was received in the current season
        // Influenza season runs roughly Jul 1 – Jun 30
        DateTime seasonStart = today.Month >= 7
            ? new DateTime(today.Year, 7, 1)
            : new DateTime(today.Year - 1, 7, 1);

        ImmunizationEntry? currentSeasonDose = seriesDoses
            .FirstOrDefault(d => d.EventDateTime >= seasonStart);

        if (currentSeasonDose != null)
        {
            return new ForecastRecommendation
            {
                VaccineGroup = series.VaccineGroup,
                Status = "COMPLETE",
                DosesReceived = seriesDoses.Count,
                DosesRequired = 1,
                LastDoseDate = currentSeasonDose.EventDateTime,
                StatusReason = $"Current season dose received on {currentSeasonDose.EventDateTime:yyyy-MM-dd}."
            };
        }

        // No dose this season — due
        bool isOverdue = today.Month >= 11; // Overdue if after November
        return new ForecastRecommendation
        {
            VaccineGroup = series.VaccineGroup,
            Status = isOverdue ? "OVERDUE" : "DUE",
            DosesReceived = seriesDoses.Count,
            DosesRequired = 1,
            RecommendedDate = seasonStart,
            LastDoseDate = seriesDoses.LastOrDefault()?.EventDateTime,
            StatusReason = isOverdue
                ? "Annual vaccine overdue for current season."
                : "Annual vaccine due for current season."
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static int CalculateAgeInMonths(DateTime dateOfBirth, DateTime asOf)
    {
        int months = (asOf.Year - dateOfBirth.Year) * 12 + (asOf.Month - dateOfBirth.Month);
        if (asOf.Day < dateOfBirth.Day) months--;
        return Math.Max(0, months);
    }

    /// <summary>
    /// Seed the default ACIP schedule. Covers the core childhood and adult series.
    /// Sites can customize via AddOrUpdateSeriesDefinitionAsync.
    /// </summary>
    private void SeedDefaultSchedule()
    {
        _state.State.Schedule =
        [
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Hepatitis B",
                CvxCodes = ["08", "44", "110"],
                DosesRequired = 3,
                MinAgeMonths = 0,
                MaxAgeMonths = 0,
                MinIntervalDays = [28, 56],
                RecommendedIntervalDays = [30, 150],
                SortOrder = 1
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "DTaP/Tdap",
                CvxCodes = ["20", "50", "106", "107", "110", "115", "120"],
                DosesRequired = 5,
                MinAgeMonths = 2,
                MaxAgeMonths = 0,
                MinIntervalDays = [28, 28, 180, 180],
                RecommendedIntervalDays = [60, 60, 365, 365],
                SortOrder = 2
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "IPV (Polio)",
                CvxCodes = ["10", "89", "110"],
                DosesRequired = 4,
                MinAgeMonths = 2,
                MaxAgeMonths = 216, // 18 years
                MinIntervalDays = [28, 28, 180],
                RecommendedIntervalDays = [60, 60, 365],
                SortOrder = 3
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "MMR",
                CvxCodes = ["03", "94"],
                DosesRequired = 2,
                MinAgeMonths = 12,
                MaxAgeMonths = 0,
                MinIntervalDays = [28],
                RecommendedIntervalDays = [1095], // ~3 years (dose 2 at age 4-6)
                SortOrder = 4
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Varicella",
                CvxCodes = ["21", "94"],
                DosesRequired = 2,
                MinAgeMonths = 12,
                MaxAgeMonths = 0,
                MinIntervalDays = [84],
                RecommendedIntervalDays = [1095],
                SortOrder = 5
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "PCV (Pneumococcal)",
                CvxCodes = ["133", "152", "215"],
                DosesRequired = 4,
                MinAgeMonths = 2,
                MaxAgeMonths = 60, // 5 years
                MinIntervalDays = [28, 28, 56],
                RecommendedIntervalDays = [60, 60, 180],
                SortOrder = 6
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Hepatitis A",
                CvxCodes = ["83", "84", "85"],
                DosesRequired = 2,
                MinAgeMonths = 12,
                MaxAgeMonths = 0,
                MinIntervalDays = [180],
                RecommendedIntervalDays = [180],
                SortOrder = 7
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Influenza",
                CvxCodes = ["88", "140", "141", "150", "153", "155", "158", "185", "186", "197"],
                DosesRequired = 1,
                MinAgeMonths = 6,
                MaxAgeMonths = 0,
                IsAnnual = true,
                SortOrder = 8
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "COVID-19",
                CvxCodes = ["207", "208", "210", "211", "212", "213", "217", "218", "219", "228", "229", "230", "300", "301", "302"],
                DosesRequired = 2,
                MinAgeMonths = 6,
                MaxAgeMonths = 0,
                MinIntervalDays = [21],
                RecommendedIntervalDays = [56],
                IsAnnual = true,
                SortOrder = 9
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "HPV",
                CvxCodes = ["62", "118", "137", "165"],
                DosesRequired = 3,
                MinAgeMonths = 108, // 9 years
                MaxAgeMonths = 324, // 27 years
                MinIntervalDays = [28, 84],
                RecommendedIntervalDays = [60, 150],
                SortOrder = 10
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Meningococcal ACWY",
                CvxCodes = ["114", "136", "147", "148"],
                DosesRequired = 2,
                MinAgeMonths = 132, // 11 years
                MaxAgeMonths = 264, // 22 years
                MinIntervalDays = [56],
                RecommendedIntervalDays = [1825], // ~5 years (dose 2 at 16)
                SortOrder = 11
            },
            new VaccineSeriesDefinition
            {
                VaccineGroup = "Td/Tdap Booster",
                CvxCodes = ["09", "113", "115", "139"],
                DosesRequired = 1,
                MinAgeMonths = 132, // 11 years
                MaxAgeMonths = 0,
                IsAnnual = false,
                MinIntervalDays = [],
                RecommendedIntervalDays = [],
                SortOrder = 12
            }
        ];
    }
}
