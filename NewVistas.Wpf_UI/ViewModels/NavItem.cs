// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
