// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Shared JSON serializer options for the federation wire format. Used by the
/// outbound HTTP transport when serializing <c>InboundFederationBatch</c> and
/// reading back <c>InboundApplyResult</c>; matches what ASP.NET Core's
/// default controller options produce on the receiving side.
///
/// <see cref="JsonSerializerDefaults.Web"/> gives camelCase property names
/// and case-insensitive deserialization, so a controller returning
/// <c>{"total":3,"applied":3,"errors":0}</c> round-trips into a record with
/// <c>Total</c>/<c>Applied</c>/<c>Errors</c> properties cleanly.
/// </summary>
public static class FederationJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web);
}
