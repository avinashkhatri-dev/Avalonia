using System;
using Avalonia;

namespace ButtonTestApp;

class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        Console.WriteLine("🚀 ButtonTestApp starting...");
        Console.WriteLine($"   Args: {string.Join(" ", args)}");
        Console.WriteLine($"   Environment: {Environment.OSVersion}");
        Console.WriteLine($"   Current Directory: {Environment.CurrentDirectory}");
        
        // Force check if automation peer factory exists
        Console.WriteLine("🔍 Checking if LinuxAutomationPeerFactory is available...");
        try 
        {
            var factory = AvaloniaLocator.Current.GetService<Avalonia.Controls.Platform.IAutomationPeerFactory>();
            Console.WriteLine($"   Factory found: {factory?.GetType().Name ?? "NULL"}");
        }
        catch (Exception ex) 
        {
            Console.WriteLine($"   Factory check failed: {ex.Message}");
        }
        
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}