using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;

namespace SldFileViewer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        Services = serviceCollection.BuildServiceProvider();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // If file path passed via command line argument, open it immediately
        if (e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0]))
        {
            var mainVm = Services.GetRequiredService<MainViewModel>();
            _ = mainVm.LoadDocumentAsync(e.Args[0]);
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core Services
        services.AddSingleton<ISwDocumentService, SwDocumentService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IExportService, ExportService>();

        // ViewModels
        services.AddSingleton<DocumentTreeViewModel>();
        services.AddSingleton<SketchCanvasViewModel>();
        services.AddSingleton<CustomPropsViewModel>();
        services.AddSingleton<EquationsViewModel>();
        services.AddSingleton<BomViewModel>();
        services.AddSingleton<MeshViewerViewModel>();
        services.AddSingleton<ImagePreviewViewModel>();
        services.AddSingleton<FeatureDetailViewModel>();
        services.AddSingleton<MainViewModel>();

        // Main Window
        services.AddSingleton<MainWindow>();
    }
}
