// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Azure.Identity;
using Azure.Storage.Blobs;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NewVistas.ImageStorage;

public static class ImageStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the imaging pipeline services. Reads the <c>ImageStorage</c>
    /// configuration section to decide which storage provider to wire in.
    /// Both BlazorWeb and WebServer call this during DI setup.
    /// </summary>
    public static IServiceCollection AddImageStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ImageStorageOptions>(configuration.GetSection("ImageStorage"));

        var options = new ImageStorageOptions();
        configuration.GetSection("ImageStorage").Bind(options);

        switch (options.Provider)
        {
            case ImageStorageProvider.AzureBlob:
                services.AddSingleton(sp =>
                {
                    var azureOpts = options.AzureBlob;
                    if (!string.IsNullOrEmpty(azureOpts.ConnectionString))
                        return new BlobServiceClient(azureOpts.ConnectionString);
                    if (!string.IsNullOrEmpty(azureOpts.AccountUrl))
                        return new BlobServiceClient(new Uri(azureOpts.AccountUrl), new DefaultAzureCredential());
                    throw new InvalidOperationException(
                        "ImageStorage:AzureBlob requires either ConnectionString or AccountUrl.");
                });
                services.AddSingleton<IImageBlobStorageService, AzureImageBlobStorageService>();
                break;

            case ImageStorageProvider.Filesystem:
            default:
                services.AddSingleton<FileSystemImageBlobStorageService>();
                services.AddSingleton<IImageBlobStorageService>(sp =>
                    sp.GetRequiredService<FileSystemImageBlobStorageService>());
                break;
        }

        // fo-dicom uses a process-wide static ServiceProvider (see Setup.cs)
        // that is NOT backed by the host DI container — so we must wire
        // ImageSharp rendering via DicomSetupBuilder. Without this, DicomImage
        // .RenderImage falls back to RawImageManager and the cast to
        // ImageSharpImage returns null → NRE during thumbnail generation.
        // The call is idempotent; subsequent invocations replace the host.
        new DicomSetupBuilder()
            .RegisterServices(s => s.AddImageManager<ImageSharpImageManager>())
            .Build();

        services.AddSingleton<DicomParsingService>();
        services.AddSingleton<RasterImageService>();
        services.AddSingleton<IImageIngestionService, ImageIngestionService>();

        return services;
    }
}
