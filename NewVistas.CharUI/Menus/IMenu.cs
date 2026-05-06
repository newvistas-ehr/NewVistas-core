// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.CharUI.Core;

namespace NewVistas.CharUI.Menus;

/// <summary>
/// Interface for all VistA-style terminal menus.
/// </summary>
public interface IMenu
{
    string Title { get; }
    Task RunAsync(MenuContext ctx);
}
