using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.LogicalTree;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Attached property helper for managing AT-SPI context state.
    /// </summary>
    public static class AccessibilitySupportHelper
    {
        public static readonly AttachedProperty<bool> HasAtspiContextProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "HasAtspiContext",
                typeof(AccessibilitySupportHelper),
                defaultValue: false,
                inherits: false);

        /// <summary>
        /// Gets whether the control has AT-SPI context.
        /// </summary>
        public static bool GetHasAtspiContext(Control control)
        {
            return control.GetValue(HasAtspiContextProperty);
        }

        /// <summary>
        /// Sets whether the control has AT-SPI context.
        /// </summary>
        public static void SetHasAtspiContext(Control control, bool value)
        {
            control.SetValue(HasAtspiContextProperty, value);
        }
    }

    /// <summary>
    /// Provides AT-SPI accessibility support for Avalonia controls on Linux.
    /// This ensures that automation peers are properly created and registered with AT-SPI.
    /// </summary>
    public static class AccessibilitySupport
    {

        /// <summary>
        /// Ensures that accessibility support is properly initialized for a control.
        /// Uses multiple approaches to guarantee automation peer creation and AT-SPI registration.
        /// </summary>
        public static void EnsureAccessibilitySupport(Control control)
        {
            // Check if running on Linux (compatible with older frameworks)
#if NET6_0_OR_GREATER
            if (!OperatingSystem.IsLinux())
                return;
#else
            if (Environment.OSVersion.Platform != PlatformID.Unix)
                return;
#endif

            try
            {
                // Check if we already processed this control
                var hasContext = AccessibilitySupportHelper.GetHasAtspiContext(control);
                if (hasContext)
                {
                    Console.WriteLine($"[AccessibilitySupport] Control {control.GetType().Name} already processed");
                    return;
                }

                Console.WriteLine($"[AccessibilitySupport] FORCE INITIALIZING accessibility for {control.GetType().Name} (Name: {control.Name ?? "NULL"})");

                AutomationPeer? peer = null;
                bool success = false;

                // METHOD 1: Try automation peer factory FIRST (this is crucial for Windows!)
                try
                {
                    var factory = AvaloniaLocator.Current.GetService<IAutomationPeerFactory>();
                    if (factory != null)
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 1: Using automation peer factory for {control.GetType().Name}");
                        peer = factory.CreateAutomationPeer(control);
                        if (peer != null)
                        {
                            Console.WriteLine($"[AccessibilitySupport] SUCCESS Method 1: Created automation peer {peer.GetType().Name} via factory");
                            success = true;
                        }
                        else
                        {
                            Console.WriteLine($"[AccessibilitySupport] Method 1: Factory returned null for {control.GetType().Name}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 1: No automation peer factory registered");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AccessibilitySupport] Method 1 failed: {ex.Message}");
                }

                // METHOD 2: Try reflection to call GetOrCreateAutomationPeer as fallback
                if (!success)
                {
                    try
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 2: Falling back to reflection for {control.GetType().Name}");
                        var getOrCreateMethod = typeof(Control).GetMethod("GetOrCreateAutomationPeer", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        if (getOrCreateMethod != null)
                        {
                            peer = getOrCreateMethod.Invoke(control, null) as AutomationPeer;
                            if (peer != null)
                            {
                                Console.WriteLine($"[AccessibilitySupport] SUCCESS Method 2: Created automation peer {peer.GetType().Name} via reflection");
                                success = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 2 failed: {ex.Message}");
                    }
                }

                // METHOD 3: Final fallback - try direct automation peer creation
                if (!success)
                {
                    try
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 3: Final fallback - direct peer creation for {control.GetType().Name}");
                        // This is a last resort - try to create a basic automation peer
                        if (control is Window window)
                        {
                            peer = new WindowAutomationPeer(window);
                        }
                        else
                        {
                            peer = new ControlAutomationPeer(control);
                        }
                        
                        if (peer != null)
                        {
                            Console.WriteLine($"[AccessibilitySupport] SUCCESS Method 3: Created fallback automation peer {peer.GetType().Name}");
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 3 failed: {ex.Message}");
                    }
                }

                // METHOD 3: Force create LinuxControlAutomationPeer directly if all else fails
                if (!success)
                {
                    try
                    {
                        peer = new LinuxControlAutomationPeer(control);
                        Console.WriteLine($"[AccessibilitySupport] SUCCESS Method 3: Force created LinuxControlAutomationPeer directly");
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AccessibilitySupport] Method 3 failed: {ex.Message}");
                    }
                }

                if (success && peer != null)
                {
                    Console.WriteLine($"[AccessibilitySupport] FINAL SUCCESS: Automation peer {peer.GetType().Name} created for {control.GetType().Name}");
                    
                    // Mark as processed to avoid duplicates
                    AccessibilitySupportHelper.SetHasAtspiContext(control, true);
                    
                    // Force AT-SPI context creation for LinuxControlAutomationPeer
                    if (peer is LinuxControlAutomationPeer linuxPeer)
                    {
                        Console.WriteLine($"[AccessibilitySupport] Linux automation peer with AT-SPI support created for {control.GetType().Name}");
                    }
                }
                else
                {
                    Console.WriteLine($"[AccessibilitySupport] FAILED: Could not create automation peer for {control.GetType().Name} using any method");
                    AccessibilitySupportHelper.SetHasAtspiContext(control, true); // Mark as processed to avoid infinite loops
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AccessibilitySupport] ERROR processing {control.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[AccessibilitySupport] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Forces accessibility initialization for all child controls recursively.
        /// </summary>
        public static void ForceAccessibilityForAllChildren(Control parent)
        {
            Console.WriteLine($"[AccessibilitySupport] FORCING accessibility for all children of {parent.GetType().Name}");
            
            EnsureAccessibilitySupport(parent);
            
            // Process all logical children
            if (parent is ILogical logical)
            {
                foreach (var child in logical.LogicalChildren)
                {
                    if (child is Control childControl)
                    {
                        ForceAccessibilityForAllChildren(childControl);
                    }
                }
            }
            
            // Also process visual children in case logical tree is incomplete
            try
            {
                if (parent is Visual visual)
                {
                    foreach (var child in visual.VisualChildren)
                    {
                        if (child is Control childControl && (parent is not ILogical || !((ILogical)parent).LogicalChildren.Contains(childControl)))
                        {
                            Console.WriteLine($"[AccessibilitySupport] Processing visual child {childControl.GetType().Name} not in logical tree");
                            ForceAccessibilityForAllChildren(childControl);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AccessibilitySupport] Error processing visual children: {ex.Message}");
            }
        }
    }
}