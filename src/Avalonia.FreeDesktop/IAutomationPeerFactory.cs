using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Linux AT-SPI automation peer factory for creating Linux-specific automation peers.
    /// </summary>
    public class LinuxAutomationPeerFactory : IAutomationPeerFactory
    {
        public LinuxAutomationPeerFactory()
        {
            Console.WriteLine("🚀 LinuxAutomationPeerFactory created - Factory is now available!");
            Console.WriteLine($"   Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            Console.WriteLine($"   Time: {DateTime.Now}");
        }
        /// <summary>
        /// Creates a Linux AT-SPI enabled automation peer for the given control.
        /// </summary>
        /// <param name="control">The control to create an automation peer for.</param>
        /// <returns>A LinuxControlAutomationPeer that provides AT-SPI support.</returns>
        public AutomationPeer? CreateAutomationPeer(Control control)
        {
            Console.WriteLine($"=== LinuxAutomationPeerFactory.CreateAutomationPeer called for {control.GetType().Name} (Name: {control.Name ?? "NULL"}) ===");
            Console.WriteLine($"  - Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            Console.WriteLine($"  - IsVisible: {control.IsVisible}");
            Console.WriteLine($"  - Parent: {control.Parent?.GetType().Name ?? "NULL"}");
            Console.WriteLine($"  - Factory call stack:");
            Console.WriteLine($"    {Environment.StackTrace}");
            
            // Special handling for Window controls - they need to be AT-SPI roots
            if (control is Window window)
            {
                Console.WriteLine($"🎯 WINDOW DETECTED: Creating Linux window automation peer for: {window.GetType().Name}");
                Console.WriteLine($"   - Window Title: {window.Title ?? "NULL"}");
                Console.WriteLine($"   - Window IsVisible: {window.IsVisible}");
                Console.WriteLine($"   - Current Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                
                // First, let the window create its own specific automation peer
                AutomationPeer? windowSpecificPeer = null;
                
                try
                {
                    var onCreateMethod = window.GetType().GetMethod("OnCreateAutomationPeer", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (onCreateMethod != null)
                    {
                        Console.WriteLine("📞 Calling OnCreateAutomationPeer via reflection...");
                        // Temporarily set the factory to null to prevent infinite recursion
                        using (AvaloniaLocator.EnterScope())
                        {
                            AvaloniaLocator.CurrentMutable.Bind<IAutomationPeerFactory>().ToConstant((IAutomationPeerFactory?)null);
                            windowSpecificPeer = (AutomationPeer?)onCreateMethod.Invoke(window, null);
                        }
                        Console.WriteLine($"📋 OnCreateAutomationPeer returned: {windowSpecificPeer?.GetType().Name ?? "NULL"}");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  OnCreateAutomationPeer method not found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Failed to get window-specific peer: {ex.Message}");
                }
                
                // Create LinuxWindowAutomationPeer with AT-SPI root support
                if (windowSpecificPeer != null)
                {
                    Console.WriteLine($"🔧 Creating LinuxWindowAutomationPeer wrapping {windowSpecificPeer.GetType().Name}");
                    var linuxPeer = new LinuxWindowAutomationPeer(window, windowSpecificPeer);
                    Console.WriteLine($"✅ LinuxWindowAutomationPeer created successfully");
                    return linuxPeer;
                }
                else
                {
                    Console.WriteLine("🔧 Creating LinuxWindowAutomationPeer without wrapped peer");
                    var linuxPeer = new LinuxWindowAutomationPeer(window);
                    Console.WriteLine($"✅ LinuxWindowAutomationPeer created successfully");
                    return linuxPeer;
                }
            }
            
            // For non-window controls, use the existing logic
            Console.WriteLine($"Creating Linux control automation peer for: {control.GetType().Name}");
            
            // First, let the control create its own specific automation peer
            AutomationPeer? controlSpecificPeer = null;
            
            // Use reflection to call the protected OnCreateAutomationPeer method
            try
            {
                var onCreateMethod = control.GetType().GetMethod("OnCreateAutomationPeer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (onCreateMethod != null)
                {
                    // Temporarily set the factory to null to prevent infinite recursion
                    var locator = AvaloniaLocator.Current;
                    var originalFactory = locator.GetService<IAutomationPeerFactory>();
                    
                    // Create a new locator scope without the factory
                    using (AvaloniaLocator.EnterScope())
                    {
                        AvaloniaLocator.CurrentMutable.Bind<IAutomationPeerFactory>().ToConstant((IAutomationPeerFactory?)null);
                        controlSpecificPeer = (AutomationPeer?)onCreateMethod.Invoke(control, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get control-specific peer: {ex.Message}");
            }
            
            // If we got a control-specific peer, wrap it with Linux AT-SPI support
            if (controlSpecificPeer != null)
            {
                Console.WriteLine($"Wrapping {controlSpecificPeer.GetType().Name} with Linux AT-SPI support");
                return new LinuxControlAutomationPeer(control, controlSpecificPeer);
            }
            
            // Fallback to creating a basic Linux automation peer
            Console.WriteLine($"Creating basic LinuxControlAutomationPeer for {control.GetType().Name}");
            return new LinuxControlAutomationPeer(control);
        }
    }
}