// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Bulletin Grain Interface based on VistA BULLETIN file (#3.6).
/// Represents an event-triggered automatic notification that sends to a mail group.
/// Key: "BULLETIN:{bulletinName}". MUMPS references: XMBBULL.m, XMB.m
/// </summary>
public interface IBulletinGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete bulletin state.
    /// </summary>
    Task<GrainStates.BulletinState> GetBulletinAsync();

    /// <summary>
    /// Creates a new bulletin definition.
    /// </summary>
    Task CreateBulletinAsync(
        string bulletinName,
        string description,
        string associatedMailGroup,
        string triggerEvent,
        string? messageTemplate);

    /// <summary>
    /// Fires the bulletin — increments FireCount and sets LastFiredDateTime.
    /// </summary>
    Task FireBulletinAsync();

    /// <summary>
    /// Deactivates the bulletin.
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Activates the bulletin.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Updates the message template for this bulletin.
    /// </summary>
    Task UpdateTemplateAsync(string messageTemplate);
}
