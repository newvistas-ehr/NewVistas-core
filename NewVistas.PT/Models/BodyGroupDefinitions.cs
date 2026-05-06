// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Static reference data mapping each body group to its standard movements
/// and normal ROM ranges. These are anatomical constants.
/// </summary>
public static class BodyGroupDefinitions
{
    private static readonly IReadOnlyDictionary<BodyGroup, IReadOnlyList<Movement>> Movements =
        new Dictionary<BodyGroup, IReadOnlyList<Movement>>
        {
            [BodyGroup.Cervical] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.LateralFlexionLeft, Movement.LateralFlexionRight,
                Movement.RotationLeft, Movement.RotationRight
            ],
            [BodyGroup.Shoulder] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.Abduction, Movement.Adduction,
                Movement.InternalRotation, Movement.ExternalRotation,
                Movement.HorizontalAbduction, Movement.HorizontalAdduction
            ],
            [BodyGroup.Elbow] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.Pronation, Movement.Supination
            ],
            [BodyGroup.Wrist] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.RadialDeviation, Movement.UlnarDeviation
            ],
            [BodyGroup.Hand] =
            [
                Movement.Grip, Movement.LateralPinch, Movement.TipPinch,
                Movement.PalmarPinch, Movement.FingerFlexion,
                Movement.FingerExtension, Movement.ThumbOpposition
            ],
            [BodyGroup.Hip] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.Abduction, Movement.Adduction,
                Movement.InternalRotation, Movement.ExternalRotation
            ],
            [BodyGroup.Knee] =
            [
                Movement.Flexion, Movement.Extension
            ],
            [BodyGroup.Ankle] =
            [
                Movement.Dorsiflexion, Movement.PlantarFlexion,
                Movement.Inversion, Movement.Eversion
            ],
            [BodyGroup.Foot] =
            [
                Movement.ToeFlexion, Movement.ToeExtension,
                Movement.ToeAbduction, Movement.ToeAdduction
            ],
            [BodyGroup.ThoracicSpine] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.RotationLeft, Movement.RotationRight,
                Movement.LateralFlexionLeft, Movement.LateralFlexionRight
            ],
            [BodyGroup.LumbarSpine] =
            [
                Movement.Flexion, Movement.Extension,
                Movement.RotationLeft, Movement.RotationRight,
                Movement.LateralFlexionLeft, Movement.LateralFlexionRight
            ],
            [BodyGroup.TMJ] =
            [
                Movement.Opening, Movement.LateralExcursionLeft,
                Movement.LateralExcursionRight, Movement.Protrusion
            ]
        };

    /// <summary>
    /// Normal ROM ranges in degrees for each body group + movement combination.
    /// Used for reference display, not validation/rejection.
    /// </summary>
    private static readonly IReadOnlyDictionary<(BodyGroup, Movement), decimal> NormalRomRanges =
        new Dictionary<(BodyGroup, Movement), decimal>
        {
            // Cervical
            [(BodyGroup.Cervical, Movement.Flexion)] = 45m,
            [(BodyGroup.Cervical, Movement.Extension)] = 45m,
            [(BodyGroup.Cervical, Movement.LateralFlexionLeft)] = 45m,
            [(BodyGroup.Cervical, Movement.LateralFlexionRight)] = 45m,
            [(BodyGroup.Cervical, Movement.RotationLeft)] = 80m,
            [(BodyGroup.Cervical, Movement.RotationRight)] = 80m,

            // Shoulder
            [(BodyGroup.Shoulder, Movement.Flexion)] = 180m,
            [(BodyGroup.Shoulder, Movement.Extension)] = 60m,
            [(BodyGroup.Shoulder, Movement.Abduction)] = 180m,
            [(BodyGroup.Shoulder, Movement.Adduction)] = 45m,
            [(BodyGroup.Shoulder, Movement.InternalRotation)] = 70m,
            [(BodyGroup.Shoulder, Movement.ExternalRotation)] = 90m,
            [(BodyGroup.Shoulder, Movement.HorizontalAbduction)] = 45m,
            [(BodyGroup.Shoulder, Movement.HorizontalAdduction)] = 135m,

            // Elbow
            [(BodyGroup.Elbow, Movement.Flexion)] = 150m,
            [(BodyGroup.Elbow, Movement.Extension)] = 0m,
            [(BodyGroup.Elbow, Movement.Pronation)] = 80m,
            [(BodyGroup.Elbow, Movement.Supination)] = 80m,

            // Wrist
            [(BodyGroup.Wrist, Movement.Flexion)] = 80m,
            [(BodyGroup.Wrist, Movement.Extension)] = 70m,
            [(BodyGroup.Wrist, Movement.RadialDeviation)] = 20m,
            [(BodyGroup.Wrist, Movement.UlnarDeviation)] = 30m,

            // Hip
            [(BodyGroup.Hip, Movement.Flexion)] = 120m,
            [(BodyGroup.Hip, Movement.Extension)] = 30m,
            [(BodyGroup.Hip, Movement.Abduction)] = 45m,
            [(BodyGroup.Hip, Movement.Adduction)] = 30m,
            [(BodyGroup.Hip, Movement.InternalRotation)] = 45m,
            [(BodyGroup.Hip, Movement.ExternalRotation)] = 45m,

            // Knee
            [(BodyGroup.Knee, Movement.Flexion)] = 135m,
            [(BodyGroup.Knee, Movement.Extension)] = 0m,

            // Ankle
            [(BodyGroup.Ankle, Movement.Dorsiflexion)] = 20m,
            [(BodyGroup.Ankle, Movement.PlantarFlexion)] = 50m,
            [(BodyGroup.Ankle, Movement.Inversion)] = 35m,
            [(BodyGroup.Ankle, Movement.Eversion)] = 15m,

            // Thoracic Spine
            [(BodyGroup.ThoracicSpine, Movement.Flexion)] = 30m,
            [(BodyGroup.ThoracicSpine, Movement.Extension)] = 25m,
            [(BodyGroup.ThoracicSpine, Movement.RotationLeft)] = 30m,
            [(BodyGroup.ThoracicSpine, Movement.RotationRight)] = 30m,
            [(BodyGroup.ThoracicSpine, Movement.LateralFlexionLeft)] = 25m,
            [(BodyGroup.ThoracicSpine, Movement.LateralFlexionRight)] = 25m,

            // Lumbar Spine
            [(BodyGroup.LumbarSpine, Movement.Flexion)] = 60m,
            [(BodyGroup.LumbarSpine, Movement.Extension)] = 25m,
            [(BodyGroup.LumbarSpine, Movement.RotationLeft)] = 30m,
            [(BodyGroup.LumbarSpine, Movement.RotationRight)] = 30m,
            [(BodyGroup.LumbarSpine, Movement.LateralFlexionLeft)] = 25m,
            [(BodyGroup.LumbarSpine, Movement.LateralFlexionRight)] = 25m,

            // TMJ
            [(BodyGroup.TMJ, Movement.Opening)] = 40m,
            [(BodyGroup.TMJ, Movement.LateralExcursionLeft)] = 10m,
            [(BodyGroup.TMJ, Movement.LateralExcursionRight)] = 10m,
            [(BodyGroup.TMJ, Movement.Protrusion)] = 8m,
        };

    /// <summary>
    /// Rich movement definitions keyed by (BodyGroup, Movement).
    /// Contains measurement type, units, tools, positioning, instructions, and illustration keys.
    /// </summary>
    private static readonly IReadOnlyDictionary<(BodyGroup, Movement), MovementDefinition> MovementDefs;

    static BodyGroupDefinitions()
    {
        var defs = new Dictionary<(BodyGroup, Movement), MovementDefinition>();

        // --- Cervical ---
        AddAngle(defs, BodyGroup.Cervical, Movement.Flexion, "Flexion", 45m,
            "Goniometer", "Seated, looking straight ahead",
            "Chin to chest", "cervical-flexion");
        AddAngle(defs, BodyGroup.Cervical, Movement.Extension, "Extension", 45m,
            "Goniometer", "Seated, looking straight ahead",
            "Head tilted back, looking up", "cervical-extension");
        AddAngle(defs, BodyGroup.Cervical, Movement.LateralFlexionLeft, "Lateral Flexion — Left", 45m,
            "Goniometer", "Seated, looking straight ahead",
            "Ear toward left shoulder", "cervical-lateral-flexion-left");
        AddAngle(defs, BodyGroup.Cervical, Movement.LateralFlexionRight, "Lateral Flexion — Right", 45m,
            "Goniometer", "Seated, looking straight ahead",
            "Ear toward right shoulder", "cervical-lateral-flexion-right");
        AddAngle(defs, BodyGroup.Cervical, Movement.RotationLeft, "Rotation — Left", 80m,
            "Goniometer", "Seated, looking straight ahead",
            "Turn head to look over left shoulder", "cervical-rotation-left");
        AddAngle(defs, BodyGroup.Cervical, Movement.RotationRight, "Rotation — Right", 80m,
            "Goniometer", "Seated, looking straight ahead",
            "Turn head to look over right shoulder", "cervical-rotation-right");

        // --- Shoulder ---
        AddAngle(defs, BodyGroup.Shoulder, Movement.Flexion, "Flexion", 180m,
            "Goniometer", "Seated or standing, arm at side",
            "Raise arm forward overhead", "shoulder-flexion");
        AddAngle(defs, BodyGroup.Shoulder, Movement.Extension, "Extension", 60m,
            "Goniometer", "Standing, arm at side",
            "Move arm backward behind body", "shoulder-extension");
        AddAngle(defs, BodyGroup.Shoulder, Movement.Abduction, "Abduction", 180m,
            "Goniometer", "Seated or standing, arm at side",
            "Raise arm out to the side overhead", "shoulder-abduction");
        AddAngle(defs, BodyGroup.Shoulder, Movement.Adduction, "Adduction", 45m,
            "Goniometer", "Seated, arm at side",
            "Move arm across body", "shoulder-adduction");
        AddAngle(defs, BodyGroup.Shoulder, Movement.InternalRotation, "Internal Rotation", 70m,
            "Goniometer", "Supine, shoulder 90° abducted, elbow 90° flexed",
            "Rotate forearm toward floor", "shoulder-internal-rotation");
        AddAngle(defs, BodyGroup.Shoulder, Movement.ExternalRotation, "External Rotation", 90m,
            "Goniometer", "Supine, shoulder 90° abducted, elbow 90° flexed",
            "Rotate forearm toward ceiling", "shoulder-external-rotation");
        AddAngle(defs, BodyGroup.Shoulder, Movement.HorizontalAbduction, "Horizontal Abduction", 45m,
            "Goniometer", "Seated, shoulder 90° flexed",
            "Move arm horizontally away from midline", "shoulder-horizontal-abduction");
        AddAngle(defs, BodyGroup.Shoulder, Movement.HorizontalAdduction, "Horizontal Adduction", 135m,
            "Goniometer", "Seated, shoulder 90° flexed",
            "Move arm horizontally across body", "shoulder-horizontal-adduction");

        // --- Elbow ---
        AddAngle(defs, BodyGroup.Elbow, Movement.Flexion, "Flexion", 150m,
            "Goniometer", "Seated, arm at side, forearm supinated",
            "Bend elbow fully", "elbow-flexion");
        AddAngle(defs, BodyGroup.Elbow, Movement.Extension, "Extension", 0m,
            "Goniometer", "Seated, arm at side",
            "Straighten elbow fully", "elbow-extension");
        AddAngle(defs, BodyGroup.Elbow, Movement.Pronation, "Pronation", 80m,
            "Goniometer", "Seated, elbow 90° flexed, thumb up",
            "Rotate forearm palm-down", "elbow-pronation");
        AddAngle(defs, BodyGroup.Elbow, Movement.Supination, "Supination", 80m,
            "Goniometer", "Seated, elbow 90° flexed, thumb up",
            "Rotate forearm palm-up", "elbow-supination");

        // --- Wrist ---
        AddAngle(defs, BodyGroup.Wrist, Movement.Flexion, "Flexion", 80m,
            "Goniometer", "Seated, forearm pronated on table",
            "Bend wrist downward", "wrist-flexion");
        AddAngle(defs, BodyGroup.Wrist, Movement.Extension, "Extension", 70m,
            "Goniometer", "Seated, forearm pronated on table",
            "Bend wrist upward", "wrist-extension");
        AddAngle(defs, BodyGroup.Wrist, Movement.RadialDeviation, "Radial Deviation", 20m,
            "Goniometer", "Seated, forearm pronated, wrist neutral",
            "Tilt hand toward thumb side", "wrist-radial-deviation");
        AddAngle(defs, BodyGroup.Wrist, Movement.UlnarDeviation, "Ulnar Deviation", 30m,
            "Goniometer", "Seated, forearm pronated, wrist neutral",
            "Tilt hand toward pinky side", "wrist-ulnar-deviation");

        // --- Hand (force-based + angle-based) ---
        AddForce(defs, BodyGroup.Hand, Movement.Grip, "Grip Strength", 100m,
            "Jamar Dynamometer", "Seated, elbow 90° flexed, forearm neutral",
            "Squeeze handle with maximum effort (3 trials, record best)", "hand-grip");
        AddForce(defs, BodyGroup.Hand, Movement.LateralPinch, "Lateral (Key) Pinch", 18m,
            "Pinch Gauge", "Seated, elbow 90° flexed",
            "Pinch key between thumb pad and lateral index finger", "hand-lateral-pinch");
        AddForce(defs, BodyGroup.Hand, Movement.TipPinch, "Tip Pinch", 14m,
            "Pinch Gauge", "Seated, elbow 90° flexed",
            "Pinch tip-to-tip between thumb and index finger", "hand-tip-pinch");
        AddForce(defs, BodyGroup.Hand, Movement.PalmarPinch, "Palmar (3-Jaw) Pinch", 16m,
            "Pinch Gauge", "Seated, elbow 90° flexed",
            "Pinch between thumb, index, and middle finger pads", "hand-palmar-pinch");
        AddAngle(defs, BodyGroup.Hand, Movement.FingerFlexion, "Finger Flexion (MCP)", null,
            "Goniometer (finger)", "Hand resting on table",
            "Bend fingers at MCP joints", "hand-finger-flexion");
        AddAngle(defs, BodyGroup.Hand, Movement.FingerExtension, "Finger Extension (MCP)", null,
            "Goniometer (finger)", "Hand resting on table",
            "Straighten fingers at MCP joints", "hand-finger-extension");
        AddAngle(defs, BodyGroup.Hand, Movement.ThumbOpposition, "Thumb Opposition", null,
            "Ruler (cm)", "Hand resting on table, palm up",
            "Touch thumb pad to base of 5th finger", "hand-thumb-opposition");

        // --- Hip ---
        AddAngle(defs, BodyGroup.Hip, Movement.Flexion, "Flexion", 120m,
            "Goniometer", "Supine",
            "Bring knee toward chest", "hip-flexion");
        AddAngle(defs, BodyGroup.Hip, Movement.Extension, "Extension", 30m,
            "Goniometer", "Prone",
            "Lift leg backward", "hip-extension");
        AddAngle(defs, BodyGroup.Hip, Movement.Abduction, "Abduction", 45m,
            "Goniometer", "Supine, legs together",
            "Move leg out to the side", "hip-abduction");
        AddAngle(defs, BodyGroup.Hip, Movement.Adduction, "Adduction", 30m,
            "Goniometer", "Supine",
            "Move leg across midline", "hip-adduction");
        AddAngle(defs, BodyGroup.Hip, Movement.InternalRotation, "Internal Rotation", 45m,
            "Goniometer", "Seated, knee 90° flexed",
            "Rotate lower leg outward (foot away from midline)", "hip-internal-rotation");
        AddAngle(defs, BodyGroup.Hip, Movement.ExternalRotation, "External Rotation", 45m,
            "Goniometer", "Seated, knee 90° flexed",
            "Rotate lower leg inward (foot toward midline)", "hip-external-rotation");

        // --- Knee ---
        AddAngle(defs, BodyGroup.Knee, Movement.Flexion, "Flexion", 135m,
            "Goniometer", "Supine or prone",
            "Bend knee fully, heel toward buttock", "knee-flexion");
        AddAngle(defs, BodyGroup.Knee, Movement.Extension, "Extension", 0m,
            "Goniometer", "Supine",
            "Straighten knee fully", "knee-extension");

        // --- Ankle ---
        AddAngle(defs, BodyGroup.Ankle, Movement.Dorsiflexion, "Dorsiflexion", 20m,
            "Goniometer", "Seated or supine, knee extended",
            "Pull foot/toes toward shin", "ankle-dorsiflexion");
        AddAngle(defs, BodyGroup.Ankle, Movement.PlantarFlexion, "Plantarflexion", 50m,
            "Goniometer", "Seated or supine",
            "Point foot/toes downward", "ankle-plantarflexion");
        AddAngle(defs, BodyGroup.Ankle, Movement.Inversion, "Inversion", 35m,
            "Goniometer", "Seated, foot off edge of table",
            "Turn sole of foot inward", "ankle-inversion");
        AddAngle(defs, BodyGroup.Ankle, Movement.Eversion, "Eversion", 15m,
            "Goniometer", "Seated, foot off edge of table",
            "Turn sole of foot outward", "ankle-eversion");

        // --- Foot ---
        AddAngle(defs, BodyGroup.Foot, Movement.ToeFlexion, "Toe Flexion", null,
            "Goniometer (finger)", "Seated",
            "Curl toes downward", "foot-toe-flexion");
        AddAngle(defs, BodyGroup.Foot, Movement.ToeExtension, "Toe Extension", null,
            "Goniometer (finger)", "Seated",
            "Extend toes upward", "foot-toe-extension");
        AddAngle(defs, BodyGroup.Foot, Movement.ToeAbduction, "Toe Abduction (Splay)", null,
            null, "Seated",
            "Spread toes apart", "foot-toe-abduction");
        AddAngle(defs, BodyGroup.Foot, Movement.ToeAdduction, "Toe Adduction", null,
            null, "Seated",
            "Squeeze toes together", "foot-toe-adduction");

        // --- Thoracic Spine ---
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.Flexion, "Flexion", 30m,
            "Inclinometer", "Seated",
            "Round upper back forward", "thoracic-flexion");
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.Extension, "Extension", 25m,
            "Inclinometer", "Seated or prone",
            "Arch upper back backward", "thoracic-extension");
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.RotationLeft, "Rotation — Left", 30m,
            "Inclinometer", "Seated, arms crossed",
            "Rotate trunk to the left", "thoracic-rotation-left");
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.RotationRight, "Rotation — Right", 30m,
            "Inclinometer", "Seated, arms crossed",
            "Rotate trunk to the right", "thoracic-rotation-right");
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.LateralFlexionLeft, "Lateral Flexion — Left", 25m,
            "Inclinometer", "Seated",
            "Bend trunk sideways to the left", "thoracic-lateral-flexion-left");
        AddAngle(defs, BodyGroup.ThoracicSpine, Movement.LateralFlexionRight, "Lateral Flexion — Right", 25m,
            "Inclinometer", "Seated",
            "Bend trunk sideways to the right", "thoracic-lateral-flexion-right");

        // --- Lumbar Spine ---
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.Flexion, "Flexion", 60m,
            "Inclinometer / tape (Schober)", "Standing",
            "Bend forward at the waist, reach toward toes", "lumbar-flexion");
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.Extension, "Extension", 25m,
            "Inclinometer", "Standing",
            "Lean backward", "lumbar-extension");
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.RotationLeft, "Rotation — Left", 30m,
            "Inclinometer", "Seated, arms crossed",
            "Rotate trunk to the left", "lumbar-rotation-left");
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.RotationRight, "Rotation — Right", 30m,
            "Inclinometer", "Seated, arms crossed",
            "Rotate trunk to the right", "lumbar-rotation-right");
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.LateralFlexionLeft, "Lateral Flexion — Left", 25m,
            "Inclinometer", "Standing",
            "Slide hand down left thigh", "lumbar-lateral-flexion-left");
        AddAngle(defs, BodyGroup.LumbarSpine, Movement.LateralFlexionRight, "Lateral Flexion — Right", 25m,
            "Inclinometer", "Standing",
            "Slide hand down right thigh", "lumbar-lateral-flexion-right");

        // --- TMJ ---
        AddDistance(defs, BodyGroup.TMJ, Movement.Opening, "Opening", 40m,
            "Ruler (mm)", "Seated, head supported",
            "Open mouth as wide as possible, measure incisor-to-incisor", "tmj-opening");
        AddDistance(defs, BodyGroup.TMJ, Movement.LateralExcursionLeft, "Lateral Excursion — Left", 10m,
            "Ruler (mm)", "Seated, head supported",
            "Slide lower jaw to the left", "tmj-lateral-left");
        AddDistance(defs, BodyGroup.TMJ, Movement.LateralExcursionRight, "Lateral Excursion — Right", 10m,
            "Ruler (mm)", "Seated, head supported",
            "Slide lower jaw to the right", "tmj-lateral-right");
        AddDistance(defs, BodyGroup.TMJ, Movement.Protrusion, "Protrusion", 8m,
            "Ruler (mm)", "Seated, head supported",
            "Push lower jaw straight forward", "tmj-protrusion");

        MovementDefs = defs;
    }

    private static void AddAngle(Dictionary<(BodyGroup, Movement), MovementDefinition> defs,
        BodyGroup bg, Movement m, string name, decimal? normal,
        string? tool, string? position, string? instruction, string? illustration)
    {
        defs[(bg, m)] = new MovementDefinition
        {
            Movement = m, DisplayName = name, MeasurementType = MeasurementType.Angle,
            Units = "degrees", NormalValue = normal, Tool = tool,
            PatientPosition = position, Instruction = instruction, IllustrationKey = illustration
        };
    }

    private static void AddForce(Dictionary<(BodyGroup, Movement), MovementDefinition> defs,
        BodyGroup bg, Movement m, string name, decimal? normal,
        string? tool, string? position, string? instruction, string? illustration)
    {
        defs[(bg, m)] = new MovementDefinition
        {
            Movement = m, DisplayName = name, MeasurementType = MeasurementType.Force,
            Units = "lbs", NormalValue = normal, Tool = tool,
            PatientPosition = position, Instruction = instruction, IllustrationKey = illustration
        };
    }

    private static void AddDistance(Dictionary<(BodyGroup, Movement), MovementDefinition> defs,
        BodyGroup bg, Movement m, string name, decimal? normal,
        string? tool, string? position, string? instruction, string? illustration)
    {
        defs[(bg, m)] = new MovementDefinition
        {
            Movement = m, DisplayName = name, MeasurementType = MeasurementType.Distance,
            Units = "mm", NormalValue = normal, Tool = tool,
            PatientPosition = position, Instruction = instruction, IllustrationKey = illustration
        };
    }

    /// <summary>
    /// Returns the standard movements for a given body group.
    /// </summary>
    public static IReadOnlyList<Movement> GetMovements(BodyGroup bodyGroup)
        => Movements[bodyGroup];

    /// <summary>
    /// Returns rich movement definitions for a body group, with measurement type,
    /// units, tools, positioning, instructions, and illustration keys.
    /// </summary>
    public static IReadOnlyList<MovementDefinition> GetMovementDefinitions(BodyGroup bodyGroup)
        => Movements[bodyGroup].Select(m => MovementDefs[(bodyGroup, m)]).ToList();

    /// <summary>
    /// Returns the movement definition for a specific body group + movement.
    /// </summary>
    public static MovementDefinition GetMovementDefinition(BodyGroup bodyGroup, Movement movement)
        => MovementDefs[(bodyGroup, movement)];

    /// <summary>
    /// Returns the normal ROM range in degrees for a body group + movement,
    /// or null if no reference range is defined (e.g., hand grip is force-based).
    /// </summary>
    public static decimal? GetNormalRomRange(BodyGroup bodyGroup, Movement movement)
        => NormalRomRanges.TryGetValue((bodyGroup, movement), out decimal range) ? range : null;

    /// <summary>
    /// Returns all body groups that have defined movements.
    /// </summary>
    public static IReadOnlyList<BodyGroup> GetAllBodyGroups()
        => Movements.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Parses an MMT grade string (e.g., "3+", "4-", "5") into a decimal value.
    /// Valid grades: 0, 1, 1+, 2-, 2, 2+, 3-, 3, 3+, 4-, 4, 4+, 5-, 5.
    /// Returns null if the input is not a valid grade.
    /// </summary>
    public static (decimal grade, string display)? ParseMmtGrade(string input)
    {
        string trimmed = input.Trim();

        if (trimmed.EndsWith('+'))
        {
            if (decimal.TryParse(trimmed[..^1], out decimal baseGrade) && baseGrade >= 1 && baseGrade <= 5)
                return (baseGrade + 0.33m, trimmed);
        }
        else if (trimmed.EndsWith('-'))
        {
            if (decimal.TryParse(trimmed[..^1], out decimal baseGrade) && baseGrade >= 2 && baseGrade <= 5)
                return (baseGrade - 0.33m, trimmed);
        }
        else if (decimal.TryParse(trimmed, out decimal grade) && grade >= 0 && grade <= 5)
        {
            return (grade, trimmed);
        }

        return null;
    }
}
