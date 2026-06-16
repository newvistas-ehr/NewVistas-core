// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Consult Grain Interface based on VistA REQUEST/CONSULTATION file (#123)
/// </summary>
public interface IConsultGrain : IGrainWithStringKey
{
    Task<GrainStates.ConsultState> GetConsultAsync();

    Task RequestConsultAsync(
        string patientId,
        string toService,
        string? toServiceId,
        string? fromService,
        string? fromServiceId,
        string urgency,
        string? requestingProviderId,
        string? requestingProviderName,
        string? attentionProviderId,
        string? attentionProviderName,
        string? reasonForRequest,
        string? provisionalDiagnosis,
        string? orderId,
        string? locationId,
        string? locationName);

    Task AcceptAsync();
    Task ScheduleAsync();
    Task CompleteAsync(DateTime completedDateTime, string? resultDocumentId);
    Task CancelAsync(string? comments);
    Task DiscontinueAsync(string? comments);

    Task AddTrackingCommentAsync(string authorId, string authorName, string commentText, string? actionTaken);
    Task AcceptWithDetailsAsync(string acceptedById, string acceptedByName);
    Task ScheduleWithDetailsAsync(DateTime scheduledDateTime, string? clinicId, string? clinicName);
    Task SetConsultTypeAsync(string consultType);
    Task SetClinicalHistoryAsync(string clinicalHistory);
    Task SetFollowUpRecommendationAsync(string recommendation);
    Task SetConsultingProviderAsync(string providerId, string providerName);
    Task MarkInterfacilityAsync(string externalFacilityId, string externalFacilityName);
    Task<List<GrainStates.ConsultTrackingComment>> GetTrackingCommentsAsync();
}
