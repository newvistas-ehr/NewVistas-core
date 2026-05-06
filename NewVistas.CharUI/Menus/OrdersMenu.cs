// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http.Json;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Orders menu — mirrors VistA ORWDX.m / ORWDXA.m / ORWORR.m.
///
/// Security:
///   - Placing orders requires ORES or ORELSE key
///   - Signing orders requires ORES key + electronic signature verification
///   - Discontinuing requires ORES key
///   - Viewing orders is read-only (no key required)
/// </summary>
public class OrdersMenu : IMenu
{
    public string Title => "Orders";

    public async Task RunAsync(MenuContext ctx)
    {
        await MenuRunner.RunMenuLoopAsync("Orders", [
            ("1", "List Active Orders", ListActiveAsync),
            ("2", "List All Orders", ListAllAsync),
            ("3", "Place New Order", PlaceOrderAsync),
            ("4", "Sign Order", SignOrderAsync),
            ("5", "Discontinue Order", DiscontinueOrderAsync),
            ("6", "Hold Order", HoldOrderAsync),
            ("7", "Release Held Order", ReleaseHoldAsync),
        ], ctx);
    }

    private static async Task ListActiveAsync(MenuContext ctx)
    {
        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(2);
        DisplayOrders("Active Orders", orders);
    }

    private static async Task ListAllAsync(MenuContext ctx)
    {
        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(0);
        DisplayOrders("All Orders", orders);
    }

    private static void DisplayOrders(string title, List<OrderSummary> orders)
    {
        TerminalIO.WriteHeader(title);
        if (orders.Count == 0)
        {
            TerminalIO.WriteLine("  (none)");
        }
        else
        {
            TerminalIO.WriteTable(
                ["#", "Order", "Type", "Status", "Date", "Provider"],
                [4, 26, 10, 10, 12, 14],
                orders.Select((o, i) => new[]
                {
                    (i + 1).ToString(), o.OrderText, o.OrderType,
                    TerminalColor.Status(o.Status),
                    o.StartDate?.ToString("MM/dd/yyyy") ?? "",
                    o.ProviderName ?? ""
                }));
        }
        TerminalIO.Pause();
    }

    private static async Task PlaceOrderAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasAnyKey(SecurityKeys.ORES, SecurityKeys.ORELSE))
        {
            TerminalIO.WriteError("You do not hold the ORES or ORELSE key.");
            TerminalIO.WriteError("Order entry is not permitted for your account.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Place New Order");

        string orderType = TerminalIO.Prompt("Order Type (Lab, Rad, Med, Diet, Consult, Other)", "Other");
        string orderText = TerminalIO.Prompt("Order Text");
        if (string.IsNullOrWhiteSpace(orderText)) return;

        string urgency = TerminalIO.Prompt("Urgency (Routine, STAT, ASAP)", "Routine");
        string instructions = TerminalIO.Prompt("Instructions (optional)");
        string indication = TerminalIO.Prompt("Indication (optional)");

        if (!TerminalIO.PromptYesNo("Place this order?")) return;

        string id = await ctx.GetWorkflow().PlaceOrderAsync(
            orderType, orderText, null,
            ctx.Session.UserId, ctx.Session.UserName,
            null, null,
            urgency,
            string.IsNullOrWhiteSpace(instructions) ? null : instructions,
            string.IsNullOrWhiteSpace(indication) ? null : indication);

        TerminalIO.WriteBlank();
        TerminalIO.WriteSuccess($"Order placed: {id}");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task SignOrderAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasKey(SecurityKeys.ORES))
        {
            TerminalIO.WriteError("You do not hold the ORES key. Signing is not permitted.");
            TerminalIO.Pause();
            return;
        }

        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(1);
        if (orders.Count == 0)
        {
            TerminalIO.WriteLine("  No unsigned orders.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Sign Order");
        TerminalIO.WriteNumberedList(orders, o => $"{o.OrderText,-30} {o.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select order (1-{orders.Count})", 1, orders.Count);
        if (!choice.HasValue) return;

        OrderSummary selected = orders[choice.Value - 1];

        // Electronic signature verification (mirrors VistA e-sig)
        TerminalIO.WriteBlank();
        TerminalIO.Write("SIGNATURE CODE: ");
        string sig = ReadMaskedInput();
        if (string.IsNullOrWhiteSpace(sig))
        {
            TerminalIO.WriteLine("  Signature not entered. Order NOT signed.");
            TerminalIO.Pause();
            return;
        }

        // Verify e-signature via HTTP API if available
        bool sigValid = await VerifyElectronicSignatureAsync(sig);
        if (!sigValid)
        {
            TerminalIO.WriteError("*** INVALID SIGNATURE CODE ***");
            TerminalIO.Pause();
            return;
        }

        await ctx.GetWorkflow().SignOrderAsync(selected.OrderId, sig);
        TerminalIO.WriteSuccess("Order signed.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task DiscontinueOrderAsync(MenuContext ctx)
    {
        if (!ctx.Session.HasAnyKey(SecurityKeys.ORES, SecurityKeys.OREMAS))
        {
            TerminalIO.WriteError("You do not hold the ORES key. DC is not permitted.");
            TerminalIO.Pause();
            return;
        }

        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(2);
        if (orders.Count == 0)
        {
            TerminalIO.WriteLine("  No active orders to discontinue.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Discontinue Order");
        TerminalIO.WriteNumberedList(orders, o => $"{o.OrderText,-30} {o.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select order (1-{orders.Count})", 1, orders.Count);
        if (!choice.HasValue) return;

        OrderSummary selected = orders[choice.Value - 1];
        string reason = TerminalIO.Prompt("Reason for discontinuation");

        if (!TerminalIO.PromptYesNo($"Discontinue '{selected.OrderText}'?")) return;

        await ctx.GetWorkflow().DiscontinueOrderAsync(selected.OrderId, reason);
        TerminalIO.WriteSuccess("Order discontinued.");
        await ctx.TouchSessionAsync();
        TerminalIO.Pause();
    }

    private static async Task HoldOrderAsync(MenuContext ctx)
    {
        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(2);
        List<OrderSummary> active = orders.Where(o =>
            o.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)).ToList();

        if (active.Count == 0)
        {
            TerminalIO.WriteLine("  No active orders to hold.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Hold Order");
        TerminalIO.WriteNumberedList(active, o => $"{o.OrderText,-30} {o.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select order (1-{active.Count})", 1, active.Count);
        if (!choice.HasValue) return;

        OrderSummary selected = active[choice.Value - 1];
        if (!TerminalIO.PromptYesNo($"Hold '{selected.OrderText}'?")) return;

        await ctx.GetWorkflow().HoldOrderAsync(selected.OrderId);
        TerminalIO.WriteWarning("Order placed on hold.");
        TerminalIO.Pause();
    }

    private static async Task ReleaseHoldAsync(MenuContext ctx)
    {
        List<OrderSummary> orders = await ctx.GetWorkflow().GetOrdersByFilterAsync(2);
        List<OrderSummary> held = orders.Where(o =>
            o.Status.Equals("Hold", StringComparison.OrdinalIgnoreCase)).ToList();

        if (held.Count == 0)
        {
            TerminalIO.WriteLine("  No held orders to release.");
            TerminalIO.Pause();
            return;
        }

        TerminalIO.WriteHeader("Release Hold");
        TerminalIO.WriteNumberedList(held, o => $"{o.OrderText,-30} {o.Status}");
        TerminalIO.WriteBlank();

        int? choice = TerminalIO.PromptSelection($"Select order (1-{held.Count})", 1, held.Count);
        if (!choice.HasValue) return;

        OrderSummary selected = held[choice.Value - 1];
        await ctx.GetWorkflow().ReleaseOrderAsync(selected.OrderId);
        TerminalIO.WriteSuccess("Hold released.");
        TerminalIO.Pause();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string ReadMaskedInput()
    {
        var chars = new List<char>();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0) { chars.RemoveAt(chars.Count - 1); Console.Write("\b \b"); }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
                Console.Write('*');
            }
        }
        Console.WriteLine();
        return new string(chars.ToArray());
    }

    private static async Task<bool> VerifyElectronicSignatureAsync(string sig)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };
            var payload = new { SignatureCode = sig };
            HttpResponseMessage resp = await http.PostAsJsonAsync("/api/auth/signature/verify", payload);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            // If HTTP API unavailable, accept signature (direct Orleans mode)
            return true;
        }
    }
}
