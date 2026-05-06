// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Grain representing a single PT measurement session for one body group.
/// Key format: "PTSESSION:{patientId}:{bodyGroup}:{side}:{yyyyMMddHHmmss}"
/// </summary>
public interface IPTSessionGrain : IGrainWithStringKey
{
    /// <summary>Returns the full session state.</summary>
    Task<PTSessionState> GetSessionAsync();

    /// <summary>Records a complete session with all measurements.</summary>
    Task RecordSessionAsync(
        string patientId,
        BodyGroup bodyGroup,
        DateTime sessionDate,
        string? therapistId,
        string? therapistName,
        string? locationId,
        string? locationName,
        Laterality side,
        List<RomMeasurement> romMeasurements,
        List<StrengthMeasurement> strengthMeasurements,
        string? notes);

    /// <summary>Adds a single ROM measurement to an existing session.</summary>
    Task AddRomMeasurementAsync(RomMeasurement measurement);

    /// <summary>Adds a single strength measurement to an existing session.</summary>
    Task AddStrengthMeasurementAsync(StrengthMeasurement measurement);

    /// <summary>Updates the session notes.</summary>
    Task UpdateNotesAsync(string notes);

    /// <summary>Adds an exercise log entry to this session.</summary>
    Task AddExerciseLogAsync(ClinicExerciseLog exercise);

    /// <summary>Links this session to a PT referral (can be called after initial recording).</summary>
    Task SetReferralAsync(string? referralId);
}
