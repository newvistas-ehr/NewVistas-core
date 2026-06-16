// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NewVistas.ImageStorage;

/// <summary>
/// Structured result of parsing a DICOM file. Fields map directly onto
/// properties of <c>ImagingState</c> and are used by the ingestion pipeline
/// to populate the Orleans grain without re-reading the pixel bytes.
/// </summary>
public record ParsedDicom(
    string? StudyUid,
    string? SeriesUid,
    string? InstanceUid,
    string? Modality,
    string? BodyPart,
    string? TransferSyntax,
    int Width,
    int Height,
    int NumberOfFrames,
    string? Laterality,
    DateTime? AcquisitionDateTime,
    string? PatientOrientation);

/// <summary>
/// Wraps fo-dicom for header extraction and thumbnail rendering. Stateless
/// singleton — holds no per-request state and is safe to inject anywhere.
/// Consumers must ensure <c>AddImageSharpImaging()</c> has been called during
/// DI registration so <see cref="DicomImage"/> can render through ImageSharp.
/// </summary>
public sealed class DicomParsingService
{
    private readonly ILogger<DicomParsingService> _logger;

    public DicomParsingService(ILogger<DicomParsingService> logger)
    {
        _logger = logger;
    }

    public async Task<(ParsedDicom Parsed, DicomFile File)> ParseAsync(
        Stream seekableInput,
        CancellationToken cancellationToken = default)
    {
        if (!seekableInput.CanSeek)
            throw new ArgumentException("DICOM parse requires a seekable stream", nameof(seekableInput));

        seekableInput.Position = 0;
        DicomFile file = await DicomFile.OpenAsync(seekableInput);
        DicomDataset ds = file.Dataset;

        int width = ds.GetSingleValueOrDefault(DicomTag.Columns, 0);
        int height = ds.GetSingleValueOrDefault(DicomTag.Rows, 0);
        int frames = ds.GetSingleValueOrDefault(DicomTag.NumberOfFrames, 1);

        DateTime? acquired = null;
        string? date = ds.GetSingleValueOrDefault<string>(DicomTag.AcquisitionDate, null!);
        string? time = ds.GetSingleValueOrDefault<string>(DicomTag.AcquisitionTime, null!);
        if (!string.IsNullOrEmpty(date) && DateTime.TryParseExact(
                date + (time ?? "000000").Substring(0, Math.Min(6, (time ?? "000000").Length)),
                "yyyyMMddHHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out DateTime parsed))
        {
            acquired = parsed;
        }

        var result = new ParsedDicom(
            StudyUid: ds.GetSingleValueOrDefault<string>(DicomTag.StudyInstanceUID, null!),
            SeriesUid: ds.GetSingleValueOrDefault<string>(DicomTag.SeriesInstanceUID, null!),
            InstanceUid: ds.GetSingleValueOrDefault<string>(DicomTag.SOPInstanceUID, null!),
            Modality: ds.GetSingleValueOrDefault<string>(DicomTag.Modality, null!),
            BodyPart: ds.GetSingleValueOrDefault<string>(DicomTag.BodyPartExamined, null!),
            TransferSyntax: file.FileMetaInfo?.TransferSyntax?.UID?.UID,
            Width: width,
            Height: height,
            NumberOfFrames: frames,
            Laterality: ds.GetSingleValueOrDefault<string>(DicomTag.ImageLaterality, null!),
            AcquisitionDateTime: acquired,
            PatientOrientation: ds.GetSingleValueOrDefault<string>(DicomTag.PatientPosition, null!));

        _logger.LogInformation(
            "Parsed DICOM {StudyUid} modality={Modality} {Width}x{Height} frames={Frames}",
            result.StudyUid, result.Modality, result.Width, result.Height, result.NumberOfFrames);

        return (result, file);
    }

    /// <summary>
    /// Renders frame 0 of the given DICOM dataset to a PNG stream sized to
    /// <paramref name="maxDimension"/> on its longest edge, preserving aspect ratio.
    /// </summary>
    public async Task<Stream> RenderThumbnailPngAsync(
        DicomFile file,
        int maxDimension = 256,
        CancellationToken cancellationToken = default)
    {
        var dicomImage = new DicomImage(file.Dataset);
        IImage rendered = dicomImage.RenderImage(0);
        using Image<Bgra32> sharp = rendered.AsSharpImage();

        int w = sharp.Width;
        int h = sharp.Height;
        double scale = Math.Min(1.0, (double)maxDimension / Math.Max(w, h));
        int tw = Math.Max(1, (int)(w * scale));
        int th = Math.Max(1, (int)(h * scale));

        sharp.Mutate(ctx => ctx.Resize(tw, th));

        var output = new MemoryStream();
        await sharp.SaveAsPngAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }
}
