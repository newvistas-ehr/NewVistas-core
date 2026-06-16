// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Security;

/// <summary>
/// Cluster-wide audit filter — logs actions on protected grain methods after successful execution.
///
/// ONC §170.315(d)(2) — auditable events and tamper-resistance.
/// ONC §170.315(d)(10) — auditing actions on health information.
///
/// Runs AFTER the method completes successfully. If the method throws, no audit event
/// is created (there was no successful action to record).
///
/// Uses the [AuditAction] attribute on grain interface methods to determine:
///   - Domain (ORDERS, LABS, NOTES, etc.)
///   - Action (CREATE, UPDATE, SIGN, etc.)
///   - EntityType (ORDER, PROBLEM, NOTE, etc.)
///
/// Automatically captures from context:
///   - PatientId: extracted from the grain key (IPatientWorkflowGrain keyed by patient ID)
///   - UserId/UserName: read from RequestContext (set by middleware)
///   - EntityId: if the method returns a string, uses it as the entity ID (e.g., PlaceOrderAsync returns orderId)
///
/// VistA equivalent: XUSEC LOG routine writing to AUDIT file #1.1.
/// </summary>
public class AuditCallFilter : IIncomingGrainCallFilter
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AuditCallFilter> _logger;

    private static readonly ConcurrentDictionary<Type, bool> _auditedInterfaceCache = new();
    private static readonly ConcurrentDictionary<(Type, string), AuditActionAttribute?> _attributeCache = new();

    public AuditCallFilter(IGrainFactory grainFactory, ILogger<AuditCallFilter> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        // 1. Fast-path: skip interfaces with zero [AuditAction] methods
        Type? interfaceType = context.InterfaceMethod.DeclaringType;
        if (interfaceType == null || !IsAuditedInterface(interfaceType))
        {
            await context.Invoke();
            return;
        }

        // 2. Check if this specific method has [AuditAction]
        AuditActionAttribute? audit = GetAuditAttribute(context.InterfaceMethod);
        if (audit == null)
        {
            await context.Invoke();
            return;
        }

        // 2a. Clinical writes are recorded by the per-patient clinical event
        //     stream grain (causal events with hash chain) — skip this filter so
        //     we don't double-record. View/access methods (Action="READ") still
        //     run through here.
        if (audit.IsClinicalWrite)
        {
            await context.Invoke();
            return;
        }

        // 3. Execute the grain method first — only audit on success
        await context.Invoke();

        // 4. Fire-and-forget audit logging (don't slow down the response)
        try
        {
            string? userId = RequestContext.Get(RequestContextKeys.UserId) as string;
            string? userName = RequestContext.Get(RequestContextKeys.UserName) as string;

            // Extract patientId from the grain key (workflow grains are keyed by patient ID)
            string patientId = context.TargetId.ToString();
            // TargetId.ToString() may include type prefix — extract the key portion
            int slashIdx = patientId.IndexOf('/');
            if (slashIdx >= 0) patientId = patientId[(slashIdx + 1)..];

            // If the method returned a string, use it as entity ID (e.g., order ID, problem ID)
            string entityId = "";
            if (context.Result is string resultStr)
                entityId = resultStr;

            string entityType = audit.EntityType ?? audit.Domain;
            string details = $"{audit.Action} {entityType} via {context.InterfaceMethod.Name}";

            // §170.315(d)(2) hash chain: get the previous event's hash from the patient index
            IPatientAuditIndexGrain indexGrain = _grainFactory.GetGrain<IPatientAuditIndexGrain>(patientId);
            List<AuditEventSummary> recent = await indexGrain.GetRecentEventsAsync(1);
            string previousHash = recent.Count > 0 && !string.IsNullOrEmpty(recent[0].EventHash)
                ? recent[0].EventHash
                : IAuditEventGrain.GenesisHash;

            // Create immutable audit event grain with hash chain
            string eventId = $"AUDIT-{Guid.NewGuid()}";
            IAuditEventGrain eventGrain = _grainFactory.GetGrain<IAuditEventGrain>(eventId);
            await eventGrain.RecordAsync(
                patientId: patientId,
                domain: audit.Domain,
                action: audit.Action,
                entityType: entityType,
                entityId: entityId,
                userId: userId,
                userName: userName,
                locationId: null,
                locationName: null,
                details: details,
                oldValue: null,
                newValue: null,
                previousEventHash: previousHash);

            // Read back the computed event hash for the index
            AuditEventState recorded = await eventGrain.GetEventAsync();

            // Add to patient audit index for fast listing
            await indexGrain.AddEventAsync(new AuditEventSummary
            {
                EventId = eventId,
                Domain = audit.Domain,
                Action = audit.Action,
                EntityType = entityType,
                EntityId = entityId,
                UserName = userName,
                Details = details,
                Timestamp = recorded.Timestamp,
                EventHash = recorded.EventHash
            });
        }
        catch (Exception ex)
        {
            // Audit logging must never fail the clinical operation
            _logger.LogError(ex,
                "Failed to log audit event for {Method} — clinical operation succeeded but audit record was not written",
                context.InterfaceMethod.Name);
        }
    }

    private static bool IsAuditedInterface(Type interfaceType)
    {
        return _auditedInterfaceCache.GetOrAdd(interfaceType, static type =>
        {
            foreach (MethodInfo method in type.GetMethods())
                if (method.GetCustomAttribute<AuditActionAttribute>() != null)
                    return true;
            foreach (Type iface in type.GetInterfaces())
                foreach (MethodInfo method in iface.GetMethods())
                    if (method.GetCustomAttribute<AuditActionAttribute>() != null)
                        return true;
            return false;
        });
    }

    private static AuditActionAttribute? GetAuditAttribute(MethodInfo method)
    {
        Type declaringType = method.DeclaringType!;
        var key = (declaringType, method.Name);
        return _attributeCache.GetOrAdd(key, _ =>
            method.GetCustomAttribute<AuditActionAttribute>());
    }
}
