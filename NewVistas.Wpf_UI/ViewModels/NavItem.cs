// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Security;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// A single navigation item in the sidebar.
/// MenuArea controls visibility based on the logged-in user's security keys.
/// </summary>
public record NavItem(string Icon, string Label, Func<object> CreateViewModel, MenuArea Area = MenuArea.General);

/// <summary>
/// A grouped section of nav items (e.g., "Clinical", "Pharmacy").
/// </summary>
public record NavSection(string Title, List<NavItem> Items);
