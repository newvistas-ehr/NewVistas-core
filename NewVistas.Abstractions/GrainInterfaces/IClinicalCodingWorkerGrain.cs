// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Stateless-worker grain that runs the clinical-coding pipeline for one piece of note text:
/// extract claims (possibly a slow, external model call), verify every quote against the
/// note, then resolve claims to ICD-10 candidates through the site's own index. Same
/// isolation pattern as the radiology-extraction worker; grain key is a fixed constant.
/// </summary>
public interface IClinicalCodingWorkerGrain : IGrainWithStringKey
{
    Task<NoteCodingSuggestions> SuggestForTextAsync(string noteText);
}
