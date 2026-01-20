using Avalonia.Automation.Peers;

#nullable enable

namespace Avalonia.Controls.Platform
{
    /// <summary>
    /// Interface for creating platform-specific automation peers.
    /// This allows different platforms to provide their own automation implementations
    /// for accessibility frameworks like Windows UIA, macOS Accessibility API, or Linux AT-SPI.
    /// </summary>
    public interface IAutomationPeerFactory
    {
        /// <summary>
        /// Creates a platform-specific automation peer for the given control.
        /// Returns null if the platform doesn't provide custom automation peers,
        /// in which case the default ControlAutomationPeer will be used.
        /// </summary>
        /// <param name="control">The control to create an automation peer for.</param>
        /// <returns>An automation peer or null if using default implementation.</returns>
        AutomationPeer? CreateAutomationPeer(Control control);
    }
}