// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.ImageStorage;
using NUnit.Framework;
using Orleans.TestingHost;
using SixLabors.ImageSharp;

namespace NewVistas.FunctionalTests;

/// <summary>
/// End-to-end tests for the imaging ingestion pipeline — the NewVistas.ImageStorage
/// library wired against the SharedCluster. Exercises the filesystem provider
/// (on-prem default) against real fo-dicom parsing of DICOM fixtures that ship
/// in the vendored <c>Imaging/fo-dicom/</c> tree.
///
/// Covers:
///   • DICOM upload: parse → thumbnail → blob write → grain write
///   • Non-DICOM raster upload: thumbnail → blob write → grain write
///   • Signed-URI mint + verify round-trip for the filesystem provider
///   • Compensating blob delete on grain-write failure
/// </summary>
[TestFixture]
public class ImagingStoragePipelineTests
{
    private TestCluster _cluster = null!;
    private ServiceProvider _services = null!;
    private string _tempRoot = null!;

    // Resolved services — same instances used by the BlazorWeb / WebServer hosts,
    // built through the AddImageStorage DI extension so fo-dicom's global static
    // ServiceProvider is initialized with ImageSharp rendering.
    private IImageIngestionService _ingestion = null!;
    private IImageBlobStorageService _blobs = null!;
    private FileSystemImageBlobStorageService _fsBlobs = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;

        _tempRoot = Path.Combine(Path.GetTempPath(), $"NewVistasImagingTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ImageStorage:Provider"] = "Filesystem",
                ["ImageStorage:Filesystem:RootPath"] = _tempRoot,
                ["ImageStorage:Filesystem:SigningKey"] = "unit-test-signing-key",
                ["ImageStorage:Filesystem:SignedLinkLifetimeMinutes"] = "10",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        // Point the ingestion service's IGrainFactory at the test cluster.
        services.AddSingleton<IGrainFactory>(_cluster.GrainFactory);
        services.AddImageStorage(config);
        _services = services.BuildServiceProvider();

        _ingestion = _services.GetRequiredService<IImageIngestionService>();
        _blobs = _services.GetRequiredService<IImageBlobStorageService>();
        _fsBlobs = _services.GetRequiredService<FileSystemImageBlobStorageService>();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _services?.Dispose();
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch (IOException) { /* best-effort cleanup */ }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a path to one of the DICOM fixtures shipped inside the vendored
    /// fo-dicom tree. Walks up from the test binary's directory until it finds
    /// the Imaging folder, so the path works regardless of where the test host
    /// puts the compiled DLL.
    /// </summary>
    private static string DicomFixture(string name)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Imaging")))
            dir = dir.Parent;

        if (dir is null)
            throw new DirectoryNotFoundException("Could not locate the repo-root Imaging folder from test base dir.");

        return Path.Combine(
            dir.FullName,
            "Imaging", "fo-dicom", "fo-dicom",
            "Tests", "FO-DICOM.Benchmark", "Data",
            name);
    }

    private static async Task<Stream> GeneratePngAsync(int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        var ms = new MemoryStream();
        await image.SaveAsPngAsync(ms);
        ms.Position = 0;
        return ms;
    }

    // ─── 1. DICOM ingestion end-to-end ────────────────────────────────────────

    [Test]
    public async Task ImagingPipeline_CanIngestDicomCtFile()
    {
        // Arrange — small CT fixture from the vendored fo-dicom benchmark data.
        string fixturePath = DicomFixture("ct.dcm");
        Assume.That(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await using FileStream fixtureStream = File.OpenRead(fixturePath);

        var request = new ImageIngestionRequest(
            PatientId: patientId,
            ObjectType: "CT",
            FileName: "ct.dcm",
            ContentType: "application/dicom",
            Content: fixtureStream,
            ProcedureDescription: "CT Test Fixture",
            LocationName: "Test Lab",
            Comments: "Ingested by ImagingPipeline_CanIngestDicomCtFile",
            CapturedByName: "Test Runner");

        // Act
        ImageIngestionResult result = await _ingestion.IngestAsync(request);

        // Assert — result echoes parsed metadata
        Assert.That(result.ImageId, Does.StartWith("IMG-"));
        Assert.That(result.OriginalBlobPath, Does.Contain(patientId));
        Assert.That(result.OriginalBlobPath, Does.EndWith("original.dcm"));
        Assert.That(result.ThumbnailBlobPath, Is.Not.Null);
        Assert.That(result.ThumbnailBlobPath, Does.EndWith("thumb.png"));
        Assert.That(result.Width, Is.GreaterThan(0));
        Assert.That(result.Height, Is.GreaterThan(0));
        Assert.That(result.Modality, Is.EqualTo("CT"));

        // Assert — files exist on disk under the filesystem root
        string originalPath = Path.Combine(_tempRoot, result.OriginalBlobPath.Replace('/', Path.DirectorySeparatorChar));
        string thumbPath = Path.Combine(_tempRoot, result.ThumbnailBlobPath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(originalPath), Is.True, $"Original not written to {originalPath}");
        Assert.That(File.Exists(thumbPath), Is.True, $"Thumbnail not written to {thumbPath}");
        Assert.That(new FileInfo(thumbPath).Length, Is.GreaterThan(0), "Thumbnail file is empty");

        // Assert — thumbnail starts with the PNG magic bytes
        byte[] thumbHeader = new byte[8];
        await using (FileStream fs = File.OpenRead(thumbPath))
        {
            await fs.ReadExactlyAsync(thumbHeader);
        }
        Assert.That(thumbHeader[0], Is.EqualTo(0x89));
        Assert.That(thumbHeader[1], Is.EqualTo((byte)'P'));
        Assert.That(thumbHeader[2], Is.EqualTo((byte)'N'));
        Assert.That(thumbHeader[3], Is.EqualTo((byte)'G'));

        // Assert — grain state persisted correctly
        IImagingGrain grain = _cluster.GrainFactory.GetGrain<IImagingGrain>(result.ImageId);
        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ObjectType, Is.EqualTo("CT"));
        Assert.That(state.Modality, Is.EqualTo("CT"));
        Assert.That(state.ImageUrl, Is.EqualTo(result.OriginalBlobPath));
        Assert.That(state.ThumbnailUrl, Is.EqualTo(result.ThumbnailBlobPath));
        Assert.That(state.ImageWidth, Is.EqualTo(result.Width));
        Assert.That(state.ImageHeight, Is.EqualTo(result.Height));
        Assert.That(state.Status, Is.EqualTo("VIEWABLE"));
        Assert.That(state.DicomStudyUid, Is.Not.Null.And.Not.Empty);
        Assert.That(state.DicomSeriesUid, Is.Not.Null.And.Not.Empty);
        Assert.That(state.DicomInstanceUid, Is.Not.Null.And.Not.Empty);

        // Assert — patient index was updated so the workflow grain's GetImagesAsync can find it
        IPatientWorkflowGrain workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        List<ImagingSummary> summaries = await workflow.GetImagesAsync(100);
        Assert.That(summaries, Has.Some.Matches<ImagingSummary>(s => s.ImageId == result.ImageId));
    }

    // ─── 2. DICOM ingestion — MR modality ─────────────────────────────────────

    [Test]
    public async Task ImagingPipeline_CanIngestDicomMrFile()
    {
        // Arrange — MR fixture (different modality to verify the parser branches
        // don't special-case CT and ensure transfer syntax is captured).
        string fixturePath = DicomFixture("mr.dcm");
        Assume.That(File.Exists(fixturePath), $"Test fixture not found: {fixturePath}");

        string patientId = $"PAT-{Guid.NewGuid():N}";
        await using FileStream fixtureStream = File.OpenRead(fixturePath);

        var request = new ImageIngestionRequest(
            PatientId: patientId,
            ObjectType: "MRI",
            FileName: "mr.dcm",
            ContentType: "application/dicom",
            Content: fixtureStream);

        // Act
        ImageIngestionResult result = await _ingestion.IngestAsync(request);

        // Assert
        Assert.That(result.Modality, Is.EqualTo("MR"));
        Assert.That(result.Width, Is.GreaterThan(0));
        Assert.That(result.Height, Is.GreaterThan(0));

        IImagingGrain grain = _cluster.GrainFactory.GetGrain<IImagingGrain>(result.ImageId);
        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.Modality, Is.EqualTo("MR"));
        Assert.That(state.TransferSyntax, Is.Not.Null.And.Not.Empty,
            "TransferSyntax should be populated from DicomFile.FileMetaInfo");
    }

    // ─── 3. Non-DICOM raster ingestion ────────────────────────────────────────

    [Test]
    public async Task ImagingPipeline_CanIngestRasterPhoto()
    {
        // Arrange — synthetic PNG, simulates a wound-photo capture.
        string patientId = $"PAT-{Guid.NewGuid():N}";
        await using Stream png = await GeneratePngAsync(800, 600);

        var request = new ImageIngestionRequest(
            PatientId: patientId,
            ObjectType: "PHOTO",
            FileName: "wound.png",
            ContentType: "image/png",
            Content: png,
            ProcedureDescription: "Wound Check");

        // Act
        ImageIngestionResult result = await _ingestion.IngestAsync(request);

        // Assert — no DICOM parsing, but dimensions and blob paths still populated
        Assert.That(result.ImageId, Does.StartWith("IMG-"));
        Assert.That(result.Width, Is.EqualTo(800));
        Assert.That(result.Height, Is.EqualTo(600));
        Assert.That(result.Modality, Is.Null, "PHOTO uploads should not carry a DICOM modality");
        Assert.That(result.OriginalBlobPath, Does.EndWith("original.png"));
        Assert.That(result.ThumbnailBlobPath, Does.EndWith("thumb.png"));

        // Files on disk
        string originalPath = Path.Combine(_tempRoot, result.OriginalBlobPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(originalPath), Is.True);

        // Grain state — no DICOM UIDs, but Width/Height from the raster service
        IImagingGrain grain = _cluster.GrainFactory.GetGrain<IImagingGrain>(result.ImageId);
        ImagingState state = await grain.GetImageAsync();
        Assert.That(state.ObjectType, Is.EqualTo("PHOTO"));
        Assert.That(state.DicomStudyUid, Is.Null.Or.Empty);
        Assert.That(state.ImageWidth, Is.EqualTo(800));
        Assert.That(state.ImageHeight, Is.EqualTo(600));
    }

    // ─── 4. Signed-URI round-trip (filesystem provider) ───────────────────────

    [Test]
    public async Task FileSystemSignedUri_VerifiesValidTokens_AndRejectsTampered()
    {
        // Arrange — upload a small blob we can reference by path.
        string patientId = $"PAT-{Guid.NewGuid():N}";
        await using Stream png = await GeneratePngAsync(100, 100);

        var request = new ImageIngestionRequest(
            patientId, "PHOTO", "probe.png", "image/png", png);
        ImageIngestionResult result = await _ingestion.IngestAsync(request);

        // Act — mint a signed URI and extract the token segment.
        Uri signed = _blobs.GetReadSasUri(result.OriginalBlobPath, TimeSpan.FromMinutes(5));
        string path = signed.IsAbsoluteUri ? signed.AbsolutePath : signed.ToString();
        string token = path.Substring(path.LastIndexOf('/') + 1);

        // Assert — valid token resolves back to the original blob path
        string? resolved = _fsBlobs.VerifySignedToken(token);
        Assert.That(resolved, Is.EqualTo(result.OriginalBlobPath));

        // Assert — flipping a character in the middle of the token breaks the HMAC
        char[] tampered = token.ToCharArray();
        int mid = tampered.Length / 2;
        tampered[mid] = tampered[mid] == 'A' ? 'B' : 'A';
        Assert.That(_fsBlobs.VerifySignedToken(new string(tampered)), Is.Null,
            "Tampered token must fail HMAC verification");

        // Assert — garbage input returns null, not an exception
        Assert.That(_fsBlobs.VerifySignedToken("not-a-real-token"), Is.Null);
        Assert.That(_fsBlobs.VerifySignedToken(string.Empty), Is.Null);
    }

    // ─── 5. Compensating delete on grain-write failure ────────────────────────
    // The ingestion service's try/catch cleans up blobs when the grain call
    // fails. We can't easily induce a grain failure mid-pipeline without
    // mocking the grain, so instead we verify the contract directly on the
    // blob service: DeleteAsync must be safe on a missing blob (so it works
    // as a compensating action) and must actually remove existing files.

    [Test]
    public async Task FileSystemBlobs_DeleteAsync_RemovesExistingBlob()
    {
        string patientId = $"PAT-{Guid.NewGuid():N}";
        await using Stream png = await GeneratePngAsync(32, 32);

        var uploaded = await _blobs.UploadAsync(
            patientId, "IMG-probe", "original.png", png, "image/png");

        string onDisk = Path.Combine(_tempRoot, uploaded.BlobPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.That(File.Exists(onDisk), Is.True, "Blob should exist before delete");

        await _blobs.DeleteAsync(uploaded.BlobPath);
        Assert.That(File.Exists(onDisk), Is.False, "Blob should be gone after delete");
    }

    [Test]
    public async Task FileSystemBlobs_DeleteAsync_IsSafeOnMissingBlob()
    {
        // Compensating delete must not throw on a path that was never written.
        Assert.DoesNotThrowAsync(async () =>
            await _blobs.DeleteAsync("never/uploaded/original.dcm"));
    }
}
