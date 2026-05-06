// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Order Set Grain — manages a single order set template.
/// Store: orderSetStore
/// </summary>
public class OrderSetGrain : Grain, IOrderSetGrain
{
    private readonly IPersistentState<OrderSetState> _state;
    private readonly ILogger<OrderSetGrain> _logger;

    public OrderSetGrain(
        [PersistentState("orderSetState", "orderSetStore")] IPersistentState<OrderSetState> state,
        ILogger<OrderSetGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_state.State.OrderSetId))
        {
            _state.State.OrderSetId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(ct);
    }

    public Task<OrderSetState> GetOrderSetAsync() => Task.FromResult(_state.State);

    public async Task UpdateOrderSetAsync(
        string name, string description, string category,
        string serviceSection, string createdBy)
    {
        _state.State.Name = name;
        _state.State.Description = description;
        _state.State.Category = category;
        _state.State.ServiceSection = serviceSection;
        _state.State.CreatedBy = createdBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTemplateAsync(OrderTemplate template)
    {
        int idx = _state.State.Items.FindIndex(t => t.TemplateId == template.TemplateId);
        if (idx >= 0)
            _state.State.Items[idx] = template;
        else
            _state.State.Items.Add(template);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTemplateAsync(string templateId)
    {
        _state.State.Items.RemoveAll(t => t.TemplateId == templateId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<OrderTemplate>> GetTemplatesAsync() =>
        Task.FromResult(_state.State.Items.OrderBy(t => t.Sequence).ToList());

    public async Task EnableAsync()
    {
        _state.State.IsActive = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisableAsync()
    {
        _state.State.IsActive = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Order Set Index Grain — singleton index for order set lookup.
/// Key: "ORDSET-INDEX"
/// Self-seeds demo order sets on first activation.
/// Store: orderSetIndexStore
/// </summary>
public class OrderSetIndexGrain : Grain, IOrderSetIndexGrain
{
    private readonly IPersistentState<OrderSetIndexState> _state;
    private readonly ILogger<OrderSetIndexGrain> _logger;

    public OrderSetIndexGrain(
        [PersistentState("orderSetIndexState", "orderSetIndexStore")] IPersistentState<OrderSetIndexState> state,
        ILogger<OrderSetIndexGrain> logger)
    {
        _state = state;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        if (_state.State.OrderSets.Count == 0)
            await SeedDemoOrderSetsAsync();
        await base.OnActivateAsync(ct);
    }

    public Task<List<OrderSetEntry>> GetAllOrderSetsAsync() =>
        Task.FromResult(_state.State.OrderSets.ToList());

    public Task<List<OrderSetEntry>> SearchOrderSetsAsync(string query)
    {
        string q = query.ToUpperInvariant();
        return Task.FromResult(_state.State.OrderSets
            .Where(os => os.Name.ToUpperInvariant().Contains(q)
                      || os.Category.ToUpperInvariant().Contains(q)
                      || os.ServiceSection.ToUpperInvariant().Contains(q))
            .ToList());
    }

    public async Task AddOrUpdateAsync(OrderSetEntry entry)
    {
        int idx = _state.State.OrderSets.FindIndex(os => os.OrderSetId == entry.OrderSetId);
        if (idx >= 0)
            _state.State.OrderSets[idx] = entry;
        else
            _state.State.OrderSets.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string orderSetId)
    {
        _state.State.OrderSets.RemoveAll(os => os.OrderSetId == orderSetId);
        await _state.WriteStateAsync();
    }

    private async Task SeedDemoOrderSetsAsync()
    {
        var demoSets = new (string Id, string Name, string Category, string Service, OrderTemplate[] Items)[]
        {
            ("ORDSET-ADM-MED", "ADMISSION ORDERS - MEDICINE", "ADMISSION", "MEDICINE", new[]
            {
                new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC WITH DIFFERENTIAL", Urgency = "ROUTINE", Sequence = 1 },
                new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BASIC METABOLIC PANEL", Urgency = "ROUTINE", Sequence = 2 },
                new OrderTemplate { TemplateId = "T3", OrderType = "Lab", OrderableItem = "URINALYSIS", Urgency = "ROUTINE", Sequence = 3 },
                new OrderTemplate { TemplateId = "T4", OrderType = "Radiology", OrderableItem = "CHEST XRAY PA & LAT", Urgency = "ROUTINE", Sequence = 4 },
                new OrderTemplate { TemplateId = "T5", OrderType = "Nursing", OrderableItem = "VITAL SIGNS Q4H", Instructions = "Every 4 hours", Urgency = "ROUTINE", Sequence = 5 },
                new OrderTemplate { TemplateId = "T6", OrderType = "Diet", OrderableItem = "REGULAR DIET", Urgency = "ROUTINE", Sequence = 6 },
                new OrderTemplate { TemplateId = "T7", OrderType = "Nursing", OrderableItem = "ACTIVITY - UP AD LIB", Urgency = "ROUTINE", Sequence = 7 },
            }),
            ("ORDSET-ADM-SURG", "ADMISSION ORDERS - SURGERY", "ADMISSION", "SURGERY", new[]
            {
                new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC WITH DIFFERENTIAL", Urgency = "ROUTINE", Sequence = 1 },
                new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BASIC METABOLIC PANEL", Urgency = "ROUTINE", Sequence = 2 },
                new OrderTemplate { TemplateId = "T3", OrderType = "Lab", OrderableItem = "PT/INR", Urgency = "ROUTINE", Sequence = 3 },
                new OrderTemplate { TemplateId = "T4", OrderType = "Lab", OrderableItem = "TYPE AND SCREEN", Urgency = "ROUTINE", Sequence = 4 },
                new OrderTemplate { TemplateId = "T5", OrderType = "Radiology", OrderableItem = "CHEST XRAY PA & LAT", Urgency = "ROUTINE", Sequence = 5 },
                new OrderTemplate { TemplateId = "T6", OrderType = "Nursing", OrderableItem = "NPO AFTER MIDNIGHT", Urgency = "ROUTINE", Sequence = 6 },
            }),
            ("ORDSET-CHF", "CHF PROTOCOL ORDERS", "PROTOCOL", "MEDICINE", new[]
            {
                new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "BNP (B-TYPE NATRIURETIC PEPTIDE)", Urgency = "ROUTINE", Sequence = 1 },
                new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BASIC METABOLIC PANEL", Urgency = "ROUTINE", Sequence = 2 },
                new OrderTemplate { TemplateId = "T3", OrderType = "Radiology", OrderableItem = "CHEST XRAY PA & LAT", Urgency = "ROUTINE", Sequence = 3 },
                new OrderTemplate { TemplateId = "T4", OrderType = "Nursing", OrderableItem = "DAILY WEIGHTS", Instructions = "Every morning before breakfast", Urgency = "ROUTINE", Sequence = 4 },
                new OrderTemplate { TemplateId = "T5", OrderType = "Nursing", OrderableItem = "FLUID RESTRICTION 1500ML/DAY", Urgency = "ROUTINE", Sequence = 5 },
                new OrderTemplate { TemplateId = "T6", OrderType = "Diet", OrderableItem = "LOW SODIUM DIET (2G)", Urgency = "ROUTINE", Sequence = 6 },
            }),
            ("ORDSET-DM", "DIABETES MANAGEMENT", "PROTOCOL", "MEDICINE", new[]
            {
                new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "HEMOGLOBIN A1C", Urgency = "ROUTINE", Sequence = 1 },
                new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BASIC METABOLIC PANEL", Urgency = "ROUTINE", Sequence = 2 },
                new OrderTemplate { TemplateId = "T3", OrderType = "Lab", OrderableItem = "LIPID PANEL", Urgency = "ROUTINE", Sequence = 3 },
                new OrderTemplate { TemplateId = "T4", OrderType = "Nursing", OrderableItem = "ACCUCHECK QID AC AND HS", Instructions = "Before meals and at bedtime", Urgency = "ROUTINE", Sequence = 4 },
                new OrderTemplate { TemplateId = "T5", OrderType = "Diet", OrderableItem = "DIABETIC DIET", Urgency = "ROUTINE", Sequence = 5 },
            }),
            ("ORDSET-POSTOP", "POST-OPERATIVE ORDERS", "DISCHARGE", "SURGERY", new[]
            {
                new OrderTemplate { TemplateId = "T1", OrderType = "Lab", OrderableItem = "CBC WITH DIFFERENTIAL", Urgency = "ROUTINE", Instructions = "AM draw post-op day 1", Sequence = 1 },
                new OrderTemplate { TemplateId = "T2", OrderType = "Lab", OrderableItem = "BASIC METABOLIC PANEL", Urgency = "ROUTINE", Instructions = "AM draw post-op day 1", Sequence = 2 },
                new OrderTemplate { TemplateId = "T3", OrderType = "Nursing", OrderableItem = "VITAL SIGNS Q1H X 4 THEN Q4H", Urgency = "ROUTINE", Sequence = 3 },
                new OrderTemplate { TemplateId = "T4", OrderType = "Nursing", OrderableItem = "I&O EVERY SHIFT", Urgency = "ROUTINE", Sequence = 4 },
                new OrderTemplate { TemplateId = "T5", OrderType = "Nursing", OrderableItem = "INCENTIVE SPIROMETRY Q1H WHILE AWAKE", Urgency = "ROUTINE", Sequence = 5 },
            }),
        };

        foreach (var (id, name, category, service, items) in demoSets)
        {
            // Create the order set grain
            var grain = GrainFactory.GetGrain<IOrderSetGrain>(id);
            await grain.UpdateOrderSetAsync(name, $"Standard {name.ToLower()} template", category, service, "SYSTEM");
            foreach (var item in items)
                await grain.AddTemplateAsync(item);

            // Register in index
            _state.State.OrderSets.Add(new OrderSetEntry
            {
                OrderSetId = id,
                Name = name,
                Category = category,
                ServiceSection = service,
                IsActive = true,
                ItemCount = items.Length
            });
        }

        await _state.WriteStateAsync();
        _logger.LogInformation("Seeded {Count} demo order sets", demoSets.Length);
    }
}
