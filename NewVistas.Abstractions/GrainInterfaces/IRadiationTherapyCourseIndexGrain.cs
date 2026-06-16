// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of all radiation therapy courses.
/// Grain key pattern: "RT-COURSE-IDX:{patientId}"
/// </summary>
public interface IRadiationTherapyCourseIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all RT course summaries for this patient, ordered by start date descending.</summary>
    Task<List<RtCourseIndexEntry>> GetAllCoursesAsync();

    /// <summary>Returns active and on-hold RT courses for this patient.</summary>
    Task<List<RtCourseIndexEntry>> GetActiveCoursesAsync();

    /// <summary>Returns completed RT courses for this patient.</summary>
    Task<List<RtCourseIndexEntry>> GetCompletedCoursesAsync();

    /// <summary>Adds or updates a course entry in this index.</summary>
    Task UpsertCourseAsync(RtCourseIndexEntry entry);

    /// <summary>Removes a course entry from this index. Idempotent.</summary>
    Task RemoveCourseAsync(string courseId);
}
