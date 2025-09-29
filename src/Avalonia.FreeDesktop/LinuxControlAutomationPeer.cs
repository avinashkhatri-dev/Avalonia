using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Linux-specific automation peer that extends ControlAutomationPeer to provide AT-SPI support.
    /// This class wraps a ControlAutomationPeer and adds Linux AT-SPI functionality.
    /// </summary>
    internal class LinuxControlAutomationPeer : ControlAutomationPeer
    {
        private AtSpiAutomationPeer? _atSpiPeer;

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
                // Create and register the AT-SPI wrapper
                _atSpiPeer = AtSpiAutomationPeer.Wrap(this);
                _atSpiPeer?.RegisterWithAtSpi();
            }
            catch (Exception)
            {
                // Accessibility should not break the application
                // Log the error in a real implementation
                _atSpiPeer = null;
            }
        }

        /// <summary>
        /// Cleans up AT-SPI resources when the automation peer is destroyed.
        /// </summary>
        ~LinuxControlAutomationPeer()
        {
            _atSpiPeer?.UnregisterFromAtSpi();
        }

        /// <summary>
        /// Gets the AT-SPI wrapper for this automation peer.
        /// </summary>
        internal AtSpiAutomationPeer? AtSpiPeer => _atSpiPeer;
    }
}