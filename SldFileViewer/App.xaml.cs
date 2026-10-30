using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SldFileViewer.Services;
using SldFileViewer.ViewModels;

namespace SldFileViewer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        MessageBox.Show($"发生未预期的界面异常:\n{e.Exception.Message}\n\n详细信息已写入 crash.log", 
                        "SldFileViewer 异常", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // Prevent application from crashing abruptly!
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogException("CurrentDomain_UnhandledException", ex);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        LogException("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static void LogException(string source, Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex}\n----------------------------------------\n";
            File.AppendAllText(logPath, entry);
        }
        catch { }
    }

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
