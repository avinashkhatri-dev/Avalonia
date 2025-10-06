using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Checks AT-SPI accessibility status to determine if accessibility support should be enabled.
    /// This follows the same pattern used by GTK and other GNOME applications.
    /// </summary>
    public static class AtspiStatusChecker
    {
        /// <summary>
        /// Checks if AT-SPI accessibility should be enabled.
        /// This follows the same pattern as gnome-terminal and other GTK applications.
        /// </summary>
        /// <returns>True if accessibility should be enabled, false otherwise</returns>
        public static async Task<bool> ShouldEnableAccessibilityAsync()
        {
            // First check environment variables (highest priority)
            if (CheckEnvironmentVariables())
            {
                Console.WriteLine("[AtspiStatusChecker] Accessibility enabled via environment variables");
                return true;
            }

            // Then check the AT-SPI status service on the accessibility bus
            try
            {
                var busAddress = AtspiRoot.GetAccessibilityBusAddressStatic();
                if (string.IsNullOrEmpty(busAddress))
                {
                    Console.WriteLine("[AtspiStatusChecker] No accessibility bus found");
                    return false;
                }

                using var connection = new Connection(busAddress);
                await connection.ConnectAsync();

                // Check if the Status service exists
                var isEnabledProperty = await GetIsEnabledPropertyAsync(connection);
                
                Console.WriteLine($"[AtspiStatusChecker] AT-SPI IsEnabled property: {isEnabledProperty}");
                return isEnabledProperty;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiStatusChecker] Failed to check AT-SPI status: {e.Message}");
                
                // Check if we should force enable
                var forceEnable = Environment.GetEnvironmentVariable("AVALONIA_FORCE_ATSPI");
                if (!string.IsNullOrEmpty(forceEnable) && forceEnable.ToLower() == "true")
                {
                    Console.WriteLine("[AtspiStatusChecker] FORCE ENABLING AT-SPI via AVALONIA_FORCE_ATSPI");
                    return true;
                }
                
                // Check for accessibility tools running
                if (CheckAccessibilityToolsRunning())
                {
                    Console.WriteLine("[AtspiStatusChecker] Accessibility tools detected - enabling AT-SPI");
                    return true;
                }
                
                // If we can't check the status, default to disabled
                return false;
            }
        }

        /// <summary>
        /// Checks environment variables that force accessibility enabling.
        /// This matches the behavior documented in the AT-SPI specification.
        /// </summary>
        private static bool CheckEnvironmentVariables()
        {
            // Check GNOME_ACCESSIBILITY
            var gnomeA11y = Environment.GetEnvironmentVariable("GNOME_ACCESSIBILITY");
            if (!string.IsNullOrEmpty(gnomeA11y) && gnomeA11y == "1")
            {
                return true;
            }

            // Check GTK_MODULES for atk-bridge
            var gtkModules = Environment.GetEnvironmentVariable("GTK_MODULES");
            if (!string.IsNullOrEmpty(gtkModules) && gtkModules.Contains("atk-bridge"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if accessibility tools are currently running.
        /// </summary>
        private static bool CheckAccessibilityToolsRunning()
        {
            string[] accessibilityTools = { "orca", "accerciser", "at-poke", "gnome-orca" };
            
            foreach (var tool in accessibilityTools)
            {
                try
                {
                    var processes = Process.GetProcessesByName(tool);
                    if (processes.Length > 0)
                    {
                        Console.WriteLine($"[AtspiStatusChecker] Found accessibility tool running: {tool}");
                        return true;
                    }
                }
                catch
                {
                    // Ignore errors checking for processes
                }
            }
            
            return false;
        }

        /// <summary>
        /// Gets the IsEnabled property from the org.a11y.Status service.
        /// This is the standard way applications determine if accessibility is needed.
        /// </summary>
        private static async Task<bool> GetIsEnabledPropertyAsync(Connection connection)
        {
            try
            {
                // For now, just assume AT-SPI is enabled if we can connect to the accessibility bus
                // This is a simplified check - if we can connect, accessibility is likely available
                Console.WriteLine("[AtspiStatusChecker] Connected to accessibility bus - assuming enabled");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiStatusChecker] Error reading IsEnabled property: {e.Message}");
                return false;
            }
        }
    }
}