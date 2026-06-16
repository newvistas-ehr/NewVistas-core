// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public partial class PatientWorkflowGrain
{
    // ─── Clinical Reminders (File #811.9) ────────────────────────────────

    public async Task<string> CreateReminderAsync(
        string reminderName, string? reminderDefinitionId, string? category,
        string? priority, string? frequency, DateTime? dueDate)
    {
        var reminderId = $"RMND-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IClinicalReminderGrain>(reminderId);
        await grain.CreateReminderAsync(PatientId, reminderName, reminderDefinitionId,
            category, priority, frequency, dueDate);
        await AppendCappedIdAsync(PatientHistoryDomains.Reminder, reminderId, DateTime.UtcNow);
        return reminderId;
    }

    public async Task CompleteReminderAsync(string reminderId, string? evaluatedByProviderId, string? evaluatedByProviderName)
    {
        var grain = GrainFactory.GetGrain<IClinicalReminderGrain>(reminderId);
        await grain.MarkDoneAsync(DateTime.UtcNow, evaluatedByProviderId, evaluatedByProviderName);
    }

    public async Task<List<ReminderSummary>> GetRemindersAsync()
    {
        // COMPLETE reminder set — callers filter by status (DUE etc.), so a
        // reminder beyond the capped recent window must still be returned.
        var ids = await GetCompleteIdsAsync(PatientHistoryDomains.Reminder);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IClinicalReminderGrain>(id).GetReminderAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(state => new ReminderSummary
            {
                ReminderId = state.ReminderId,
                ReminderName = state.ReminderName ?? "",
                Status = state.Status ?? "",
                DueDate = state.DueDate ?? state.NextDueDate
            })
            .ToList();
    }

    // ─── Immunizations (File #9000010.11) ────────────────────────────────

    public async Task<string> RecordImmunizationAsync(
        string immunizationName, string? cvxCode, DateTime eventDateTime,
        string? series, string? lotNumber, string? manufacturer,
        string? administeredById, string? administeredByName,
        string? administrationSite, string? route, string? dose,
        string? locationId, string? locationName, string? comments)
    {
        var immunizationId = $"IMM-{Guid.NewGuid()}";
        var entry = new ImmunizationEntry
        {
            ImmunizationId = immunizationId,
            ImmunizationName = immunizationName,
            CvxCode = cvxCode,
            EventDateTime = eventDateTime,
            Series = series,
            LotNumber = lotNumber,
            Manufacturer = manufacturer,
            AdministeredById = administeredById,
            AdministeredByName = administeredByName,
            AdministrationSite = administrationSite,
            Route = route,
            Dose = dose,
            LocationId = locationId,
            LocationName = locationName,
            Comments = comments,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        await GetPatientGrain().AddImmunizationAsync(entry);
        return immunizationId;
    }

    public async Task<List<ImmunizationSummary>> GetImmunizationsAsync()
    {
        var entries = await GetPatientGrain().GetImmunizationsAsync();
        return entries.OrderByDescending(s => s.EventDateTime)
            .Select(s => new ImmunizationSummary
            {
                ImmunizationId = s.ImmunizationId, ImmunizationName = s.ImmunizationName,
                CvxCode = s.CvxCode, EventDateTime = s.EventDateTime,
                Series = s.Series, AdministeredByName = s.AdministeredByName
            }).ToList();
    }

    // ─── Health Factors (File #9000010.23) ───────────────────────────────

    public async Task<string> RecordHealthFactorAsync(
        string healthFactorName, string? category, DateTime eventDateTime,
        string? levelSeverity, string? visitId,
        string? locationId, string? locationName,
        string? enteredById, string? enteredByName, string? comments)
    {
        var hfId = $"HF-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(hfId);
        await grain.RecordHealthFactorAsync(PatientId, healthFactorName, null, category,
            eventDateTime, levelSeverity, visitId, locationId, locationName,
            enteredById, enteredByName, comments);
        await AppendCappedIdAsync(PatientHistoryDomains.HealthFactor, hfId, DateTime.UtcNow);
        return hfId;
    }

    public async Task<List<HealthFactorSummary>> GetHealthFactorsAsync()
    {
        var ids = await GetPatientGrain().GetHealthFactorIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IHealthFactorGrain>(id).GetHealthFactorAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.EventDateTime)
            .Select(s => new HealthFactorSummary
            {
                HealthFactorId = s.HealthFactorId, HealthFactorName = s.HealthFactorName,
                Category = s.Category, EventDateTime = s.EventDateTime,
                LevelSeverity = s.LevelSeverity
            }).ToList();
    }

    /// <summary>
    /// Paged full health factor history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<HealthFactorSummary>> GetHealthFactorHistoryAsync(int offset, int maxResults)
    {
        var ids = await GetHistoryPageIdsAsync(PatientHistoryDomains.HealthFactor, offset, maxResults);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IHealthFactorGrain>(id).GetHealthFactorAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(s => new HealthFactorSummary
            {
                HealthFactorId = s.HealthFactorId, HealthFactorName = s.HealthFactorName,
                Category = s.Category, EventDateTime = s.EventDateTime,
                LevelSeverity = s.LevelSeverity
            }).ToList();
    }

    // ─── Mental Health (File #601.71) ────────────────────────────────────

    public async Task<string> RecordMentalHealthScreenAsync(
        string instrumentName, DateTime administrationDateTime,
        decimal? totalScore, string? scoreInterpretation, bool? isPositiveScreen,
        Dictionary<string, string>? responses,
        string? administeredById, string? administeredByName,
        string? locationId, string? locationName, string? comments)
    {
        var mhId = $"MH-{Guid.NewGuid()}";
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(mhId);
        await grain.RecordInstrumentAsync(PatientId, instrumentName, null, administrationDateTime,
            totalScore, scoreInterpretation, isPositiveScreen, responses,
            administeredById, administeredByName, null, null, locationId, locationName, null, comments);
        await AppendCappedIdAsync(PatientHistoryDomains.MentalHealth, mhId, DateTime.UtcNow);
        return mhId;
    }

    public async Task<List<MentalHealthSummary>> GetMentalHealthScreensAsync()
    {
        var ids = await GetPatientGrain().GetMentalHealthIdsAsync();
        var tasks = ids.Select(id => GrainFactory.GetGrain<IMentalHealthGrain>(id).GetInstrumentAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states.OrderByDescending(s => s.AdministrationDateTime)
            .Select(s => new MentalHealthSummary
            {
                InstrumentId = s.InstrumentId, InstrumentName = s.InstrumentName,
                AdministrationDateTime = s.AdministrationDateTime, TotalScore = s.TotalScore,
                ScoreInterpretation = s.ScoreInterpretation, IsPositiveScreen = s.IsPositiveScreen,
                Status = s.Status
            }).ToList();
    }

    /// <summary>
    /// Paged full mental health history (newest first); default reads return only the recent window.
    /// </summary>
    public async Task<List<MentalHealthSummary>> GetMentalHealthHistoryAsync(int offset, int maxResults)
    {
        var ids = await GetHistoryPageIdsAsync(PatientHistoryDomains.MentalHealth, offset, maxResults);
        var tasks = ids.Select(id => GrainFactory.GetGrain<IMentalHealthGrain>(id).GetInstrumentAsync()).ToList();
        var states = await Task.WhenAll(tasks);
        return states
            .Select(s => new MentalHealthSummary
            {
                InstrumentId = s.InstrumentId, InstrumentName = s.InstrumentName,
                AdministrationDateTime = s.AdministrationDateTime, TotalScore = s.TotalScore,
                ScoreInterpretation = s.ScoreInterpretation, IsPositiveScreen = s.IsPositiveScreen,
                Status = s.Status
            }).ToList();
    }

    // ─── Mental Health Depth (File #601.71 Phase 4) ────────────────────────

    public async Task RecordMentalHealthRiskAssessmentAsync(string instrumentId, int riskLevel, string? riskNotes)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        await grain.RecordRiskAssessmentAsync(riskLevel, riskNotes);
    }

    public async Task SetMentalHealthFollowUpAsync(string instrumentId, bool requiresFollowUp, DateTime? followUpDueDate, string? followUpPlan)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        await grain.SetFollowUpAsync(requiresFollowUp, followUpDueDate, followUpPlan);
    }

    public async Task AddMentalHealthItemResponseAsync(string instrumentId, int itemNumber, string questionText, int responseValue, string? responseText)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        await grain.AddItemResponseAsync(itemNumber, questionText, responseValue, responseText);
    }

    public async Task ScoreMentalHealthInstrumentAsync(string instrumentId)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        await grain.ScoreInstrumentAsync();
    }

    public async Task SetMentalHealthPreviousScoreAsync(string instrumentId, decimal previousScore, DateTime previousDate)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        await grain.SetPreviousScoreAsync(previousScore, previousDate);
    }

    public async Task<decimal?> CalculateMentalHealthScoreChangeAsync(string instrumentId)
    {
        var grain = GrainFactory.GetGrain<IMentalHealthGrain>(instrumentId);
        return await grain.CalculateScoreChangeAsync();
    }

    // ─── Dietetics (File #115.2) ─────────────────────────────────────────

    public async Task<string> CreateDietOrderAsync(
        string dietType, string? currentDiet, List<string>? modifications,
        string? texture, string? fluidConsistency, string? calorieLevel,
        string? specialInstructions, DateTime startDateTime,
        string? providerId, string? providerName, string? comments)
    {
        var dietId = $"DIET-{Guid.NewGuid()}";
        var entry = new DieteticsEntry
        {
            DieteticsId = dietId,
            DietType = dietType,
            CurrentDiet = currentDiet,
            Modifications = modifications ?? new(),
            Texture = texture,
            FluidConsistency = fluidConsistency,
            CalorieLevel = calorieLevel,
            SpecialInstructions = specialInstructions,
            StartDateTime = startDateTime,
            ProviderId = providerId,
            ProviderName = providerName,
            Comments = comments,
            Status = "ACTIVE",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        await GetPatientGrain().AddDietOrderAsync(entry);
        return dietId;
    }

    public async Task DiscontinueDietOrderAsync(string dietOrderId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.Status = "DISCONTINUED";
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task<List<DieteticsSummary>> GetDietOrdersAsync()
    {
        var entries = await GetPatientGrain().GetDietOrdersAsync();
        return entries.OrderByDescending(s => s.StartDateTime)
            .Select(s => new DieteticsSummary
            {
                DietOrderId = s.DieteticsId, DietType = s.DietType,
                CurrentDiet = s.CurrentDiet, Status = s.Status, StartDateTime = s.StartDateTime ?? DateTime.MinValue
            }).ToList();
    }

    // ─── Prosthetics (File #669.1) ───────────────────────────────────────

    public async Task<string> IssueProstheticAsync(
        string itemDescription, string? hcpcsCode, string? itemCategory,
        DateTime dateIssued, int quantity, decimal? cost,
        string? providerId, string? providerName,
        string? locationId, string? locationName,
        bool isServiceConnected, string? comments)
    {
        var prosId = $"PROS-{Guid.NewGuid()}";
        var entry = new ProstheticsEntry
        {
            ProstheticsId = prosId,
            ItemDescription = itemDescription,
            HcpcsCode = hcpcsCode,
            ItemCategory = itemCategory,
            DateIssued = dateIssued,
            Quantity = quantity,
            Cost = cost,
            ProviderId = providerId,
            ProviderName = providerName,
            LocationId = locationId,
            LocationName = locationName,
            IsServiceConnected = isServiceConnected,
            Comments = comments,
            Status = "ISSUED",
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };
        await GetPatientGrain().AddProstheticsItemAsync(entry);
        return prosId;
    }

    public async Task<List<ProstheticsSummary>> GetProstheticsAsync()
    {
        var entries = await GetPatientGrain().GetProstheticsItemsAsync();
        return entries.OrderByDescending(s => s.DateIssued)
            .Select(s => new ProstheticsSummary
            {
                ProstheticsId = s.ProstheticsId, ItemDescription = s.ItemDescription,
                HcpcsCode = s.HcpcsCode, Status = s.Status,
                DateIssued = s.DateIssued ?? DateTime.MinValue, IsServiceConnected = s.IsServiceConnected
            }).ToList();
    }

    // ─── Immunization Depth (File #9000010.11 Phase 5) ──────────────────────

    public async Task MarkImmunizationHistoricalAsync(string immunizationId, string informationSource)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.InformationSource = informationSource;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task RecordImmunizationVISAsync(string immunizationId, DateTime visDateOffered, DateTime visDatePublished)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.VisDateOffered = visDateOffered;
            entry.VisDatePublished = visDatePublished;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task SetImmunizationSeriesInfoAsync(string immunizationId, int doseNumber, int dosesInSeries, bool seriesComplete)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.DoseNumber = doseNumber;
            entry.DosesInSeries = dosesInSeries;
            entry.SeriesComplete = seriesComplete;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task SetImmunizationAdministrationDetailsAsync(string immunizationId, string site, string route)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.AdministrationSite = site;
            entry.Route = route;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task SetImmunizationVaccineGroupAsync(string immunizationId, string groupName, string groupCode)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.VaccineGroupName = groupName;
            entry.VaccineGroupCode = groupCode;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task SetImmunizationManufacturerAsync(string immunizationId, string name, string code)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.ManufacturerName = name;
            entry.ManufacturerCode = code;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task UpdateImmunizationRegistryStatusAsync(string immunizationId, string status)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.RegistryStatus = status;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    public async Task AddImmunizationCommentAsync(string immunizationId, string authorName, string commentText)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetImmunizationAsync(immunizationId);
        if (entry != null)
        {
            entry.ImmunizationComments.Add(new ImmunizationComment
            {
                CommentDateTime = DateTime.UtcNow,
                AuthorName = authorName,
                CommentText = commentText
            });
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateImmunizationAsync(entry);
        }
    }

    // ─── Health Factor Depth (File #9000010.23 Phase 5) ─────────────────────

    public async Task UpdateHealthFactorSeverityAsync(string healthFactorId, string severityLevel)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.UpdateSeverityAsync(severityLevel);
    }

    public async Task SetHealthFactorCategoryAsync(string healthFactorId, string category, string? subcategory)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.SetCategoryAsync(category, subcategory);
    }

    public async Task SetHealthFactorValueAsync(string healthFactorId, string value, string? magnitude)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.SetValueAsync(value, magnitude);
    }

    public async Task ResolveHealthFactorAsync(string healthFactorId, string resolvedByName)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.ResolveAsync(resolvedByName);
    }

    public async Task ReactivateHealthFactorAsync(string healthFactorId)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.ReactivateAsync();
    }

    public async Task AddHealthFactorHistoryEntryAsync(string healthFactorId, string value, string? severityLevel, string? comment, string? recordedByName)
    {
        var grain = GrainFactory.GetGrain<IHealthFactorGrain>(healthFactorId);
        await grain.AddHistoryEntryAsync(value, severityLevel, comment, recordedByName);
    }

    // ─── Dietetics Depth (File #115.2 Phase 5) ─────────────────────────────

    public async Task SetDietNutritionGoalsAsync(string dietOrderId, int? calorieTarget, decimal? targetWeight)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.CalorieTarget = calorieTarget;
            entry.TargetWeight = targetWeight;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task SetDietFluidRestrictionAsync(string dietOrderId, int? fluidRestrictionMl)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.FluidRestrictionMl = fluidRestrictionMl;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task SetDietTextureConsistencyAsync(string dietOrderId, string textureConsistency)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.TextureConsistency = textureConsistency;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task SetDietTubeFeedingAsync(string dietOrderId, bool isTubeFeeding, string? formula, decimal? rateMlHr)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.IsTubeFeeding = isTubeFeeding;
            entry.TubeFeedingFormula = formula;
            entry.TubeFeedingRateMlHr = rateMlHr;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task SetDietNPOAsync(string dietOrderId, bool isNPO, DateTime? startDate, DateTime? endDate)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.IsNPO = isNPO;
            entry.NPOStartDate = startDate;
            entry.NPOEndDate = endDate;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task RecordDietMealPreferenceAsync(string dietOrderId, string preferences)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.MealPreferences = preferences;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task RecordDietNutritionAssessmentAsync(string dietOrderId, decimal score, string assessedByName)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.NutritionAssessmentScore = score;
            entry.NutritionAssessmentDate = DateTime.UtcNow;
            entry.AssessedByName = assessedByName;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task RecordDietBMIAsync(string dietOrderId, decimal bmi)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.CurrentBMI = bmi;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    public async Task SetDietAllergyConsiderationsAsync(string dietOrderId, string allergyConsiderations)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetDietOrderAsync(dietOrderId);
        if (entry != null)
        {
            entry.AllergyConsiderations = allergyConsiderations;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateDietOrderAsync(entry);
        }
    }

    // ─── Prosthetics Depth (File #669.1 Phase 5) ────────────────────────────

    public async Task SetProstheticsHcpcsCodeAsync(string prostheticsId, string hcpcsCode)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.HcpcsCode = hcpcsCode;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task RecordProstheticsCostAsync(string prostheticsId, decimal cost, string? vendorName, string? vendorId)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.Cost = cost;
            entry.Vendor = vendorName;
            entry.VendorId = vendorId;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task SetProstheticsWarrantyAsync(string prostheticsId, DateTime startDate, DateTime endDate)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.WarrantyStartDate = startDate;
            entry.WarrantyEndDate = endDate;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task RecordProstheticsDeliveryAsync(string prostheticsId, DateTime deliveryDate, string deliveryMethod, string? trackingNumber)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.DeliveryDate = deliveryDate;
            entry.DeliveryMethod = deliveryMethod;
            entry.TrackingNumber = trackingNumber;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task RecordProstheticsFittingAsync(string prostheticsId, string fittingNotes, string fittedByName)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.FittingNotes = fittingNotes;
            entry.FittedByName = fittedByName;
            entry.FittingDate = DateTime.UtcNow;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task RecordProstheticsSatisfactionAsync(string prostheticsId, int rating)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.PatientSatisfaction = rating;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task ScheduleProstheticsMaintenanceAsync(string prostheticsId, DateTime nextDate, string? notes)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.NextMaintenanceDate = nextDate;
            entry.MaintenanceNotes = notes;
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }

    public async Task AddProstheticsMaintenanceRecordAsync(string prostheticsId, string maintenanceType, string? technicianName, string? notes, decimal? cost)
    {
        var patient = GetPatientGrain();
        var entry = await patient.GetProstheticsItemAsync(prostheticsId);
        if (entry != null)
        {
            entry.MaintenanceHistory.Add(new ProstheticsMaintenanceRecord
            {
                MaintenanceDate = DateTime.UtcNow,
                MaintenanceType = maintenanceType,
                TechnicianName = technicianName,
                Notes = notes,
                Cost = cost
            });
            entry.LastModifiedDate = DateTime.UtcNow;
            await patient.UpdateProstheticsItemAsync(entry);
        }
    }
}
