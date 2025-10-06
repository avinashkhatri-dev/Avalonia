using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.LogicalTree;
using Avalonia.Platform;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Linux-specific window automation peer that provides AT-SPI root functionality.
    /// This peer is responsible for registering the entire application with the AT-SPI system.
    /// </summary>
    internal class LinuxWindowAutomationPeer : WindowAutomationPeer
    {
        private readonly AutomationPeer? _wrappedPeer;

        public LinuxWindowAutomationPeer(Window owner) : base(owner)
        {
            Console.WriteLine($"=== LinuxWindowAutomationPeer created for {owner.GetType().Name} ===");
            InitializeAtSpiRoot();
        }
        
        /// <summary>
        /// Constructor that wraps an existing window automation peer with AT-SPI support.
        /// </summary>
        public LinuxWindowAutomationPeer(Window owner, AutomationPeer wrappedPeer) : base(owner)
        {
            _wrappedPeer = wrappedPeer;
            Console.WriteLine($"=== LinuxWindowAutomationPeer wrapping {wrappedPeer.GetType().Name} for {owner.GetType().Name} ===");
            InitializeAtSpiRoot();
        }

        /// <summary>
        /// Initializes the AT-SPI root for this window, which registers the entire application.
        /// </summary>
        private void InitializeAtSpiRoot()
        {
            try
            {
                Console.WriteLine("=== Initializing AT-SPI Root for Window ===");
                
                // Get the Window instance
                if (Owner is not Window window)
                {
                    Console.WriteLine("❌ Owner is not a Window, cannot create AT-SPI root");
                    return;
                }

                Console.WriteLine($"✅ Window owner confirmed: {window.GetType().Name}, Title: {window.Title ?? "NULL"}");
                
                // Check current AT-SPI status
                var currentRoot = AtspiRoot.Current;
                Console.WriteLine($"🔍 Current AtspiRoot state: {(currentRoot != null ? "EXISTS" : "NULL")}");
                
                // Register this window with the AT-SPI system
                // The AtspiRoot.RegisterRoot method handles D-Bus connection and registration
                Console.WriteLine("🚀 Calling AtspiRoot.RegisterRoot...");
                var atspiRoot = AtspiRoot.RegisterRoot(() => this);
                
                if (atspiRoot != null)
                {
                    Console.WriteLine($"✅ Window successfully registered with AT-SPI root - Connected: {atspiRoot.IsConnected}");
                    Console.WriteLine($"   - Bus Name: {atspiRoot.LocalName}");
                    Console.WriteLine($"   - Object Path: {atspiRoot.ObjectPath}");
                }
                else
                {
                    Console.WriteLine("❌ AT-SPI root registration returned null");
                }
                
                // Force a delay and double-check
                Console.WriteLine("⏳ Waiting 2 seconds for AT-SPI registration to complete...");
                System.Threading.Thread.Sleep(2000);
                
                var finalRoot = AtspiRoot.Current;
                if (finalRoot != null)
                {
                    Console.WriteLine($"✅ Final check - AtspiRoot.Current exists: Connected={finalRoot.IsConnected}");
                    
                    // NOW that we have AT-SPI root, force re-initialization of child controls
                    Console.WriteLine("🔄 Re-initializing child controls now that AT-SPI root exists...");
                    if (Owner is Window windowOwner)
                    {
                        ForceReinitializeChildControls(windowOwner);
                    }
                }
                else
                {
                    Console.WriteLine("❌ Final check - AtspiRoot.Current is still null");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in InitializeAtSpiRoot: {ex.Message}");
                Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Forces re-initialization of child controls now that AT-SPI root exists.
        /// </summary>
        private void ForceReinitializeChildControls(Window window)
        {
            try
            {
                Console.WriteLine("🔄 Starting comprehensive AT-SPI initialization for all controls...");
                
                // Get the content of the window
                if (window.Content is Control rootControl)
                {
                    Console.WriteLine($"🔄 Re-initializing AT-SPI for window content: {rootControl.GetType().Name}");
                    ForceReinitializeControl(rootControl);
                }
                
                // Also set up monitoring for future control additions
                SetupControlMonitoring(window);
                
                Console.WriteLine("✅ Comprehensive AT-SPI initialization completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error re-initializing child controls: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sets up monitoring for when new controls are added to the visual tree.
        /// </summary>
        private void SetupControlMonitoring(Window window)
        {
            try
            {
                Console.WriteLine("🔧 Setting up control monitoring for dynamic AT-SPI registration...");
                
                // Monitor logical tree changes
                window.LogicalChildren.CollectionChanged += (sender, e) =>
                {
                    if (e.NewItems != null)
                    {
                        foreach (var newItem in e.NewItems)
                        {
                            if (newItem is Control newControl)
                            {
                                Console.WriteLine($"🆕 New control added: {newControl.GetType().Name} - Creating automation peer");
                                ForceReinitializeControl(newControl);
                            }
                        }
                    }
                };
                
                Console.WriteLine("✅ Control monitoring setup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error setting up control monitoring: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Recursively re-initializes AT-SPI for a control and its children.
        /// </summary>
        private void ForceReinitializeControl(Control control)
        {
            try
            {
                Console.WriteLine($"🔄 Force creating automation peer for {control.GetType().Name}");
                
                // Use our factory-first AccessibilitySupport system
                AccessibilitySupport.EnsureAccessibilitySupport(control);
                
                // Also try the direct method as backup
                var createMethod = typeof(Control).GetMethod("GetOrCreateAutomationPeer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (createMethod != null)
                {
                    var peer = createMethod.Invoke(control, null) as AutomationPeer;
                    Console.WriteLine($"✅ Created automation peer for {control.GetType().Name}: {peer?.GetType().Name ?? "NULL"}");
                    
                    // If we got a peer, make sure it creates its platform implementation
                    if (peer != null)
                    {
                        try
                        {
                            // Force platform implementation creation
                            var createPlatformMethod = typeof(AutomationPeer).GetMethod("CreatePlatformImpl", 
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            createPlatformMethod?.Invoke(peer, null);
                            Console.WriteLine($"✅ Platform implementation created for {control.GetType().Name}");
                        }
                        catch (Exception platformEx)
                        {
                            Console.WriteLine($"⚠️  Platform implementation creation failed for {control.GetType().Name}: {platformEx.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"❌ GetOrCreateAutomationPeer method not found for {control.GetType().Name}");
                }
                
                // Recursively process child controls
                foreach (var child in control.GetLogicalChildren().OfType<Control>())
                {
                    ForceReinitializeControl(child);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error re-initializing control {control.GetType().Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Override GetNameCore to delegate to wrapped peer if available, or use base implementation.
        /// </summary>
        protected override string? GetNameCore()
        {
            // Try to get name from wrapped peer first
            if (_wrappedPeer != null)
            {
                try
                {
                    return _wrappedPeer.GetName();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error getting name from wrapped peer: {ex.Message}");
                }
            }
            
            // Fall back to base implementation
            return base.GetNameCore();
        }
    }
}