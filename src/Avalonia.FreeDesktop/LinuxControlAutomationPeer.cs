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

        public LinuxControlAutomationPeer(Control control) : base(control)
        {
            // Initialize AT-SPI support immediately since we can't override CreatePlatformImpl
            InitializeAtSpiSupport();
        }

        /// <summary>
        /// Initializes the AT-SPI support for this automation peer.
        /// Called during construction since we can't override the internal CreatePlatformImpl method.
        /// </summary>
        private void InitializeAtSpiSupport()
        {
            try
            {
                // Ensure the AT-SPI root is initialized
                if (AtspiRoot.Current == null)
                {
                    // Register the root with a simple factory - just use this peer as root for now
                    AtspiRoot.RegisterRoot(() => this);
                }

                // Create the AT-SPI context using the global root instance
                _atspiContext = AtspiRoot.Current?.CreateAutomationContext(this);
            }
            catch (Exception)
            {
                // Accessibility should not break the application
                // Log the error in a real implementation
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