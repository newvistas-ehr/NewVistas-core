// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.FunctionalTests;

/// <summary>
/// Radiology report fixtures for the finding-extraction tests.
/// </summary>
public static class RadiologyTestData
{
    /// <summary>
    /// A synthetic cervical-spine MRI report modeling the canonical missed-finding scenario:
    /// mild central canal stenosis at C5-C6, but moderate-to-severe LEFT neural foraminal
    /// stenosis at the same level — the material finding a surgeon must not skip past.
    /// </summary>
    public const string SyntheticCervicalReport =
        "MRI CERVICAL SPINE WITHOUT CONTRAST.\n" +
        "\n" +
        "IMPRESSION:\n" +
        "At C4-C5 there is no significant central canal or neural foraminal stenosis.\n" +
        "At C5-C6 there is mild central canal stenosis.\n" +
        "At C5-C6 there is moderate to severe left neural foraminal stenosis.\n" +
        "At C5-C6 there is mild right neural foraminal stenosis.\n" +
        "At C6-C7 there is minimal central canal stenosis.";

    /// <summary>
    /// Drop a real radiology report here to exercise extraction over it. Leave empty and the
    /// real-report test is skipped. (Paste between the verbatim string literals.)
    /// </summary>
    public const string RealReport = "";
}
