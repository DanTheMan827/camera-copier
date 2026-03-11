using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DanTheMan827.CameraCopier.Configuration;
using DanTheMan827.CameraCopier.Services;

namespace DanTheMan827.CameraCopier;

/// <summary>
/// Entry point for the Camera Copier application.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.Configure<AppSettings>(context.Configuration);

                services.AddSingleton<IProcessingQueue, ProcessingQueue>();
                services.AddSingleton<IProcessedHashStore, ProcessedHashStore>();
                services.AddSingleton<IHashService, HashService>();
                services.AddSingleton<IMetadataService, MetadataService>();
                services.AddSingleton<ICopyService, CopyService>();
                services.AddSingleton<IFileProcessor, FileProcessor>();
                services.AddSingleton<IFolderScanner, FolderScanner>();

                services.AddHostedService<FileWatcherService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            })
            .Build();

        await host.RunAsync();
    }
}
