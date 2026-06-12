// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Direct Address Grain — manages a registered Direct address.
/// §170.315(h)(1) — Direct Project.
///
/// Grain Key: "DIRECT-ADDR:{directAddress}"
/// </summary>
public interface IDirectAddressGrain : IGrainWithStringKey
{
    Task SaveAddressAsync(DirectAddressState address);
    Task<DirectAddressState> GetAddressAsync();
    Task SetActiveAsync(bool isActive);
    Task UpdateCertificateAsync(string thumbprint, string subject, DateTime expiration);
}

/// <summary>
/// Direct Address Index Grain — registry of all Direct addresses.
/// Grain Key: "DIRECT-ADDR-INDEX"
/// </summary>
public interface IDirectAddressIndexGrain : IGrainWithStringKey
{
    Task AddAddressAsync(DirectAddressSummary summary);
    Task RemoveAddressAsync(string directAddress);
    Task<List<DirectAddressSummary>> GetAllAddressesAsync();
    Task<List<DirectAddressSummary>> GetActiveAddressesAsync();
}

/// <summary>
/// Direct Message Grain — manages a single Direct message (C-CDA exchange).
/// §170.315(h)(1) — Applicability Statement for Secure Health Transport (§170.202(a)(2)).
///
/// Grain Key: "DIRECT-MSG:{messageId}"
/// </summary>
public interface IDirectMessageGrain : IGrainWithStringKey
{
    Task CreateMessageAsync(
        string direction,
        string fromAddress,
        string toAddress,
        string subject,
        string patientId,
        string? patientName,
        string documentType,
        string ccdaContent,
        string? sentBy);

    Task MarkSendingAsync();
    Task MarkSentAsync(bool isEncrypted, bool isSigned, string? signingCertThumbprint, string? encryptionCertThumbprint);
    Task MarkDeliveredAsync();
    Task MarkFailedAsync(string reason);

    /// <summary>Record MDN per §170.202(e)(1) — Delivery Notification in Direct.</summary>
    Task RecordMdnAsync(string mdnStatus, string? mdnContent);

    Task<DirectMessageState> GetMessageAsync();
}

/// <summary>
/// Direct Message Index Grain — listing of all Direct messages.
/// Grain Key: "DIRECT-MSG-INDEX"
/// </summary>
public interface IDirectMessageIndexGrain : IGrainWithStringKey
{
    Task AddMessageAsync(DirectMessageSummary summary);
    Task UpdateStatusAsync(string messageId, string status);
    Task UpdateMdnStatusAsync(string messageId, string mdnStatus);
    Task<List<DirectMessageSummary>> GetAllMessagesAsync();
    Task<List<DirectMessageSummary>> GetOutboundMessagesAsync();
    Task<List<DirectMessageSummary>> GetInboundMessagesAsync();
    Task<List<DirectMessageSummary>> GetMessagesByPatientAsync(string patientId, int maxResults = 50);
    Task<List<DirectMessageSummary>> GetMessagesByStatusAsync(string status);
    Task<List<DirectMessageSummary>> GetPendingDeliveryAsync();
}

/// <summary>
/// Direct C-CDA Generator Grain — generates a Consolidated CDA document for a patient.
/// Produces a Continuity of Care Document (CCD) conforming to C-CDA R2.1.
///
/// Grain Key: "DIRECT-CCDA-GEN:{patientId}"
/// </summary>
public interface IDirectCcdaGeneratorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generate a C-CDA CCD document from the patient's current clinical data.
    /// </summary>
    Task<string> GenerateCcdAsync(string documentType);
}
