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
        /// <summary>
        /// Creates a Linux AT-SPI enabled automation peer for the given control.
        /// </summary>
        /// <param name="control">The control to create an automation peer for.</param>
        /// <returns>A LinuxControlAutomationPeer that provides AT-SPI support.</returns>
        public AutomationPeer? CreateAutomationPeer(Control control)
        {
            return new LinuxControlAutomationPeer(control);
        }
    }
}