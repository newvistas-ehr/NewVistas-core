// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NewVistas.WpfDelphiUI.Services;
using NewVistas.WpfDelphiUI.ViewModels;

namespace NewVistas.WpfDelphiUI;

/// <summary>
/// Application entry point. Builds a Generic Host for DI, then shows MainWindow.
/// No Orleans client — this app calls the NewVistas REST API via HttpClient.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Shared singletons
                services.AddSingleton<PatientContext>();
                services.AddSingleton<AuthService>();

                // HttpClient for the NewVistas REST API
                services.AddHttpClient<ApiClient>(c =>
                {
                    c.BaseAddress = new Uri("https://localhost:7127/");
                    c.Timeout     = TimeSpan.FromSeconds(30);
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                });

                // Shell ViewModel (singleton — keeps tab state alive)
                services.AddSingleton<MainViewModel>();

                // Patient selection dialog VM (transient — fresh each open)
                services.AddTransient<PatientSelectionViewModel>();

                // Chart tab ViewModels (singletons — one instance per session,
                // they self-reload when PatientContext.PatientId changes)
                services.AddSingleton<CoverSheetViewModel>();
                services.AddSingleton<ProblemsViewModel>();
                services.AddSingleton<MedicationsViewModel>();
                services.AddSingleton<OrdersViewModel>();
                services.AddSingleton<NotesViewModel>();
                services.AddSingleton<ConsultsViewModel>();
                services.AddSingleton<LabsViewModel>();
                services.AddSingleton<VitalsViewModel>();
                services.AddSingleton<SurgeryViewModel>();
                services.AddSingleton<ReportsViewModel>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = new MainWindow
        {
            DataContext = _host.Services.GetRequiredService<MainViewModel>()
        };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
