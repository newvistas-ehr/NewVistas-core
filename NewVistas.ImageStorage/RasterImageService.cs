// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace NewVistas.ImageStorage;

/// <summary>
/// Non-DICOM raster (JPG/PNG) thumbnail generation. Used for the
/// <c>PHOTO</c> imaging object type — wound photos, clinical captures, etc.
/// Relies on ImageSharp, which is already pulled in transitively via
/// fo-dicom.Imaging.ImageSharp.
/// </summary>
public sealed class RasterImageService
{
    public async Task<(Stream Thumbnail, int OriginalWidth, int OriginalHeight)> GenerateThumbnailAsync(
        Stream seekableInput,
        int maxDimension = 256,
        CancellationToken cancellationToken = default)
    {
        if (!seekableInput.CanSeek)
            throw new ArgumentException("Raster thumbnail requires a seekable stream", nameof(seekableInput));

        seekableInput.Position = 0;
        using Image image = await Image.LoadAsync(seekableInput, cancellationToken);

        int w = image.Width;
        int h = image.Height;
        double scale = Math.Min(1.0, (double)maxDimension / Math.Max(w, h));
        int tw = Math.Max(1, (int)(w * scale));
        int th = Math.Max(1, (int)(h * scale));

        using Image clone = image.Clone(ctx => ctx.Resize(tw, th));

        var output = new MemoryStream();
        await clone.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;

        seekableInput.Position = 0;
        return (output, w, h);
    }
}
