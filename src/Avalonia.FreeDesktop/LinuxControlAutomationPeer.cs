using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Linux-specific automation peer that extends ControlAutomationPeer to provide AT-SPI support.
    /// This class wraps a ControlAutomationPeer and adds Linux AT-SPI functionality.
    /// </summary>
    internal class LinuxControlAutomationPeer : ControlAutomationPeer
    {
        private AtspiContext? _atspiContext;
        private readonly AutomationPeer? _wrappedPeer;

        public LinuxControlAutomationPeer(Control control) : base(control)
        {
            // Initialize AT-SPI support immediately since we can't override CreatePlatformImpl
            InitializeAtSpiSupport();
        }
        
        /// <summary>
        /// Constructor that wraps an existing automation peer with AT-SPI support.
        /// </summary>
        public LinuxControlAutomationPeer(Control control, AutomationPeer wrappedPeer) : base(control)
        {
            _wrappedPeer = wrappedPeer;
            Console.WriteLine($"LinuxControlAutomationPeer wrapping {wrappedPeer.GetType().Name} for {control.GetType().Name}");
            // Initialize AT-SPI support immediately since we can't override CreatePlatformImpl
            InitializeAtSpiSupport();
        }

        // Delegate important methods to the wrapped peer if available
        public new string? GetName() => _wrappedPeer?.GetName() ?? base.GetName();
        public new AutomationControlType GetAutomationControlType() => _wrappedPeer?.GetAutomationControlType() ?? base.GetAutomationControlType();
        public new string? GetClassName() => _wrappedPeer?.GetClassName() ?? base.GetClassName();
        public new string? GetAutomationId() => _wrappedPeer?.GetAutomationId() ?? base.GetAutomationId();

        /// <summary>
        /// Gets the wrapped automation peer, if any.
        /// </summary>
        internal AutomationPeer? WrappedPeer => _wrappedPeer;

        /// <summary>
        /// Initializes the AT-SPI support for this automation peer.
        /// Called during construction since we can't override the internal CreatePlatformImpl method.
        /// </summary>
        private void InitializeAtSpiSupport()
        {
            try
            {
                Console.WriteLine($"=== InitializeAtSpiSupport for {Owner?.GetType().Name ?? "Unknown"} ===");
                
                // Check if AT-SPI is disabled via environment variable
                if (Environment.GetEnvironmentVariable("AVALONIA_DISABLE_ATSPI") == "1")
                {
                    Console.WriteLine("AT-SPI disabled via environment variable");
                    return;
                }

                // Check if we're in a D-Bus environment
                var dbusAddress = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
                if (string.IsNullOrEmpty(dbusAddress))
                {
                    Console.WriteLine("No D-Bus session available - AT-SPI won't work");
                    return;
                }

                Console.WriteLine($"D-Bus session available: {dbusAddress}");

                // The LinuxWindowAutomationPeer handles AT-SPI root registration for windows
                // Child controls just need to connect to the existing root
                var isTopLevel = Owner is Window;
                Console.WriteLine($"Is top-level window: {isTopLevel}");
                
                if (isTopLevel)
                {
                    Console.WriteLine("⚠️  LinuxControlAutomationPeer should not be used for Windows - use LinuxWindowAutomationPeer instead");
                }

                // Create AT-SPI context for this control using the shared root
                if (AtspiRoot.Current != null)
                {
                    Console.WriteLine("✅ AT-SPI root available, creating context for control...");
                    _atspiContext = AtspiRoot.Current.CreateAutomationContext(this);
                    Console.WriteLine($"✅ AT-SPI context created: {_atspiContext != null}");
                }
                else
                {
                    Console.WriteLine("❌ No AT-SPI root available - window should create it first");
                    Console.WriteLine("🔄 Will retry AT-SPI initialization in 2 seconds...");
                    
                    // Retry after a delay - the window might create the root soon
                    System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                    {
                        try
                        {
                            Console.WriteLine($"🔄 Retrying AT-SPI initialization for {Owner?.GetType().Name ?? "Unknown"}...");
                            if (AtspiRoot.Current != null && _atspiContext == null)
                            {
                                Console.WriteLine("✅ AT-SPI root now available on retry!");
                                _atspiContext = AtspiRoot.Current.CreateAutomationContext(this);
                                Console.WriteLine($"✅ AT-SPI context created on retry: {_atspiContext != null}");
                            }
                            else if (AtspiRoot.Current == null)
                            {
                                Console.WriteLine("❌ AT-SPI root still not available after retry");
                            }
                            else
                            {
                                Console.WriteLine("ℹ️  AT-SPI context already exists, no retry needed");
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Console.WriteLine($"❌ AT-SPI retry failed: {retryEx.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // Accessibility should not break the application
                // Log the error but continue gracefully
                Console.WriteLine($"AT-SPI initialization failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                _atspiContext = null;
            }
        }

        /// <summary>
        /// Cleans up AT-SPI resources when the automation peer is destroyed.
        /// </summary>
        ~LinuxControlAutomationPeer()
        {
            _atspiContext = null;
        }

        /// <summary>
        /// Gets the AT-SPI context for this automation peer.
        /// </summary>
        internal AtspiContext? AtspiContext => _atspiContext;
    }
}