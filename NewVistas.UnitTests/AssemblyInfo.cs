// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;

// Run test fixtures in parallel — safe because each fixture uses unique grain IDs (Guid.NewGuid).
[assembly: Parallelizable(ParallelScope.Fixtures)]
