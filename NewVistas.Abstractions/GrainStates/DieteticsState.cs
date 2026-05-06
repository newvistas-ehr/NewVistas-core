// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single diet order entry embedded in the patient grain.
/// Based on VistA FH PATIENT file (#115.2) and NUTRITION ORDER.
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class DieteticsEntry
{
    [Id(0)]
    public string DieteticsId { get; set; } = string.Empty;

    /// <summary>
    /// Diet Order Type � REGULAR, TUBEFEEDING, TPN, NPO, SUPPLEMENT, etc.
    /// </summary>
    [Id(2)]
    public string DietType { get; set; } = string.Empty;

    /// <summary>
    /// Current Diet (.01)
    /// </summary>
    [Id(3)]
    public string? CurrentDiet { get; set; }

    /// <summary>
    /// Diet modifications � CARDIAC, RENAL, DIABETIC, LOW SODIUM, etc.
    /// </summary>
    [Id(4)]
    public List<string> Modifications { get; set; } = new();

    /// <summary>
    /// Supplements
    /// </summary>
    [Id(5)]
    public List<string> Supplements { get; set; } = new();

    /// <summary>
    /// Texture � REGULAR, MECHANICAL SOFT, PUREE, LIQUID, etc.
    /// </summary>
    [Id(6)]
    public string? Texture { get; set; }

    /// <summary>
    /// Fluid Consistency � THIN, NECTAR, HONEY, SPOON-THICK
    /// </summary>
    [Id(7)]
    public string? FluidConsistency { get; set; }

    /// <summary>
    /// Status � ACTIVE, EXPIRED, DISCONTINUED
    /// </summary>
    [Id(8)]
    public string Status { get; set; } = "ACTIVE";

    [Id(9)]
    public DateTime? StartDateTime { get; set; }

    [Id(10)]
    public DateTime? ExpirationDateTime { get; set; }

    /// <summary>
    /// Ordering Provider
    /// </summary>
    [Id(11)]
    public string? ProviderId { get; set; }

    [Id(12)]
    public string? ProviderName { get; set; }

    [Id(13)]
    public string? OrderId { get; set; }

    /// <summary>
    /// Calorie Level
    /// </summary>
    [Id(14)]
    public string? CalorieLevel { get; set; }

    /// <summary>
    /// Allergies/intolerances relevant to diet
    /// </summary>
    [Id(15)]
    public List<string> FoodAllergies { get; set; } = new();

    /// <summary>
    /// Special Instructions
    /// </summary>
    [Id(16)]
    public string? SpecialInstructions { get; set; }

    [Id(17)]
    public string? LocationId { get; set; }

    [Id(18)]
    public string? LocationName { get; set; }

    [Id(19)]
    public string? Comments { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Daily calorie goal
    /// </summary>
    [Id(22)]
    public int? CalorieTarget { get; set; }

    /// <summary>
    /// Maximum daily fluid intake in mL (null = unrestricted)
    /// </summary>
    [Id(23)]
    public int? FluidRestrictionMl { get; set; }

    /// <summary>
    /// Texture consistency — REGULAR, MECHANICAL_SOFT, PUREED, LIQUID
    /// </summary>
    [Id(24)]
    public string? TextureConsistency { get; set; }

    /// <summary>
    /// Tube feeding flag
    /// </summary>
    [Id(25)]
    public bool IsTubeFeeding { get; set; }

    /// <summary>
    /// Tube feeding formula name
    /// </summary>
    [Id(26)]
    public string? TubeFeedingFormula { get; set; }

    /// <summary>
    /// Tube feeding rate in mL/hr
    /// </summary>
    [Id(27)]
    public decimal? TubeFeedingRateMlHr { get; set; }

    /// <summary>
    /// Nothing by mouth flag
    /// </summary>
    [Id(28)]
    public bool IsNPO { get; set; }

    /// <summary>
    /// NPO start date
    /// </summary>
    [Id(29)]
    public DateTime? NPOStartDate { get; set; }

    /// <summary>
    /// NPO end date
    /// </summary>
    [Id(30)]
    public DateTime? NPOEndDate { get; set; }

    /// <summary>
    /// Patient meal preferences
    /// </summary>
    [Id(31)]
    public string? MealPreferences { get; set; }

    /// <summary>
    /// Nutrition assessment score (e.g., MNA score)
    /// </summary>
    [Id(32)]
    public decimal? NutritionAssessmentScore { get; set; }

    /// <summary>
    /// Nutrition assessment date
    /// </summary>
    [Id(33)]
    public DateTime? NutritionAssessmentDate { get; set; }

    /// <summary>
    /// Name of assessor
    /// </summary>
    [Id(34)]
    public string? AssessedByName { get; set; }

    /// <summary>
    /// Current BMI
    /// </summary>
    [Id(35)]
    public decimal? CurrentBMI { get; set; }

    /// <summary>
    /// Target weight
    /// </summary>
    [Id(36)]
    public decimal? TargetWeight { get; set; }

    /// <summary>
    /// Diet-relevant allergy considerations
    /// </summary>
    [Id(37)]
    public string? AllergyConsiderations { get; set; }

    /// <summary>
    /// History of diet modifications
    /// </summary>
    [Id(38)]
    public List<DietModificationEntry> ModificationHistory { get; set; } = new();
}

/// <summary>
/// Record of a diet modification event
/// </summary>
[GenerateSerializer]
public class DietModificationEntry
{
    /// <summary>
    /// Date of modification
    /// </summary>
    [Id(0)]
    public DateTime ModificationDate { get; set; }

    /// <summary>
    /// Type of modification
    /// </summary>
    [Id(1)]
    public string ModificationType { get; set; } = string.Empty;

    /// <summary>
    /// Description of modification
    /// </summary>
    [Id(2)]
    public string? Description { get; set; }

    /// <summary>
    /// Name of ordering provider
    /// </summary>
    [Id(3)]
    public string? OrderedByName { get; set; }
}
