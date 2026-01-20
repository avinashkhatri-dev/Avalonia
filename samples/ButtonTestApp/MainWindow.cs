using System;
using Avalonia.Controls;

namespace ButtonTestApp;

public class MainWindow : Window
{
    public MainWindow()
    {
        Console.WriteLine("🏠 MainWindow constructor called");
        
        Title = "Button Test App";
        Width = 300;
        Height = 200;
        
        Console.WriteLine("🔧 Creating button content...");
        
        // Create a single button - nothing else
        var button = new Button 
        { 
            Content = "Test Button",
            Width = 120,
            Height = 40,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        Console.WriteLine($"📋 Button created: {button.GetType().Name}");
        
        // Set the button directly as window content
        Content = button;
        
        Console.WriteLine("✅ MainWindow setup complete");
        
        // Hook into the Opened event to ensure proper AT-SPI initialization after window is fully shown
        this.Opened += (sender, e) =>
        {
            Console.WriteLine("� Window Opened event - Starting AT-SPI initialization...");
            
            // Force window automation peer creation through normal Avalonia pathway
            try
            {
                // This should trigger the LinuxWindowAutomationPeer creation
                var windowMethod = this.GetType().GetMethod("OnCreateAutomationPeer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var windowPeer = windowMethod?.Invoke(this, null);
                Console.WriteLine($"📋 Window automation peer: {windowPeer?.GetType().Name ?? "NULL"}");
                
                // Give it time to establish AT-SPI root
                System.Threading.Thread.Sleep(1000);
                
                // Now force button automation peer creation
                var buttonMethod = button.GetType().GetMethod("OnCreateAutomationPeer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var buttonPeer = buttonMethod?.Invoke(button, null);
                Console.WriteLine($"📋 Button automation peer: {buttonPeer?.GetType().Name ?? "NULL"}");
                
                // Force the LinuxWindowAutomationPeer to reinitialize children if it exists
                if (windowPeer != null && windowPeer.GetType().Name == "LinuxWindowAutomationPeer")
                {
                    Console.WriteLine("� Forcing child control reinitialization...");
                    var reinitMethod = windowPeer.GetType().GetMethod("ForceReinitializeChildControls", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    reinitMethod?.Invoke(windowPeer, new object[] { this });
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AT-SPI initialization failed: {ex.Message}");
            }
        };
    }
}