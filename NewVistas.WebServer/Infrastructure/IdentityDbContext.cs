// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NewVistas.WebServer.Infrastructure;

/// <summary>
/// ASP.NET Core Identity database context.
/// Uses EF Core InMemory for development, SQL Server for production.
/// </summary>
public class NewVistasIdentityDbContext : IdentityDbContext<NewVistasUser>
{
    public NewVistasIdentityDbContext(DbContextOptions<NewVistasIdentityDbContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// Extended Identity user with a link to the NewPersonGrain.
/// The UserId here becomes the grain key suffix in "USER:{Id}".
/// </summary>
public class NewVistasUser : IdentityUser
{
    /// <summary>
    /// Display name in VistA format (LAST,FIRST MI).
    /// Synced with NewPersonGrain on registration.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
