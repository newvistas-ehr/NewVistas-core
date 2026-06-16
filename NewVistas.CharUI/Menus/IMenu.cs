// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
