using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Linux AT-SPI automation peer wrapper for accessibility support.
    /// This class wraps Avalonia AutomationPeer objects to provide AT-SPI interface implementations.
    /// </summary>
    internal class AtSpiAutomationPeer
    {
        private static readonly ConditionalWeakTable<AutomationPeer, AtSpiAutomationPeer> s_wrappers = new();
        private readonly AutomationPeer _inner;
        private ObjectPath? _objectPath;
        private bool _isRegistered;

        private AtSpiAutomationPeer(AutomationPeer inner)
        {
            _inner = inner;
            _inner.ChildrenChanged += OnChildrenChanged;
            
            if (inner is IRootProvider root)
                root.FocusChanged += OnFocusChanged;
        }

        ~AtSpiAutomationPeer()
        {
            Cleanup();
        }

        public AutomationPeer InnerPeer => _inner;
        public ObjectPath? ObjectPath => _objectPath;
        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// Gets the AT-SPI accessible name for this peer.
        /// </summary>
        public string Name => _inner.GetName() ?? string.Empty;

        /// <summary>
        /// Gets the AT-SPI accessible description for this peer.
        /// </summary>
        public string Description => _inner.GetHelpText() ?? string.Empty;

        /// <summary>
        /// Gets the AT-SPI role for this peer based on the automation control type.
        /// </summary>
        public uint Role => MapControlTypeToAtSpiRole(_inner.GetAutomationControlType());

        /// <summary>
        /// Gets the child automation peers as AT-SPI peers.
        /// </summary>
        public IReadOnlyList<AtSpiAutomationPeer> Children
        {
            get
            {
                var children = _inner.GetChildren();
                return children?.Select(Wrap).Where(p => p != null).ToList() ?? new List<AtSpiAutomationPeer>();
            }
        }

        /// <summary>
        /// Gets the parent automation peer as an AT-SPI peer.
        /// </summary>
        public AtSpiAutomationPeer? Parent => Wrap(_inner.GetParent());

        /// <summary>
        /// Registers this peer with the AT-SPI bus and creates the necessary D-Bus objects.
        /// </summary>
        public void RegisterWithAtSpi()
        {
            if (_isRegistered)
                return;

            try
            {
                // Generate unique object path for this peer
                _objectPath = new ObjectPath($"/org/a11y/atspi/accessible/{GetHashCode():X}");
                
                // TODO: Register D-Bus object for AT-SPI interfaces
                // This would involve:
                // 1. Creating D-Bus object implementing AT-SPI interfaces
                // 2. Registering object with the D-Bus connection
                // 3. Notifying AT-SPI registry of the new accessible object

                _isRegistered = true;
            }
            catch (Exception)
            {
                // Log error but don't throw - accessibility should not break the application
                _isRegistered = false;
            }
        }

        /// <summary>
        /// Unregisters this peer from AT-SPI and cleans up D-Bus resources.
        /// </summary>
        public void UnregisterFromAtSpi()
        {
            if (!_isRegistered)
                return;

            try
            {
                // TODO: Unregister D-Bus objects and notify AT-SPI registry
                _isRegistered = false;
                _objectPath = null;
            }
            catch (Exception)
            {
                // Log error but continue cleanup
            }
        }

        /// <summary>
        /// Wraps an AutomationPeer in an AtSpiAutomationPeer, reusing existing wrappers.
        /// </summary>
        [return: NotNullIfNotNull("peer")]
        public static AtSpiAutomationPeer? Wrap(AutomationPeer? peer)
        {
            return peer is null ? null : s_wrappers.GetValue(peer, x => new AtSpiAutomationPeer(peer));
        }

        private void OnChildrenChanged(object? sender, EventArgs e)
        {
            if (_isRegistered)
            {
                // TODO: Notify AT-SPI clients about children changes
                // This would involve emitting D-Bus signals
            }
        }

        private void OnFocusChanged(object? sender, EventArgs e)
        {
            if (_isRegistered)
            {
                // TODO: Notify AT-SPI clients about focus changes
                // This would involve emitting D-Bus signals
            }
        }

        private void Cleanup()
        {
            _inner.ChildrenChanged -= OnChildrenChanged;
            
            if (_inner is IRootProvider root)
                root.FocusChanged -= OnFocusChanged;
                
            UnregisterFromAtSpi();
        }

        /// <summary>
        /// Maps Avalonia AutomationControlType to AT-SPI role constants.
        /// Based on AT-SPI specification role definitions.
        /// </summary>
        private static uint MapControlTypeToAtSpiRole(AutomationControlType controlType)
        {
            // AT-SPI role constants (these would typically come from AT-SPI headers)
            // Using placeholder values - actual values should match AT-SPI specification
            return controlType switch
            {
                AutomationControlType.Button => 26, // ATSPI_ROLE_PUSH_BUTTON
                AutomationControlType.CheckBox => 25, // ATSPI_ROLE_CHECK_BOX
                AutomationControlType.ComboBox => 28, // ATSPI_ROLE_COMBO_BOX
                AutomationControlType.Edit => 42, // ATSPI_ROLE_TEXT
                AutomationControlType.Hyperlink => 36, // ATSPI_ROLE_LINK
                AutomationControlType.Image => 34, // ATSPI_ROLE_IMAGE
                AutomationControlType.List => 33, // ATSPI_ROLE_LIST
                AutomationControlType.ListItem => 32, // ATSPI_ROLE_LIST_ITEM
                AutomationControlType.Menu => 35, // ATSPI_ROLE_MENU
                AutomationControlType.MenuBar => 33, // ATSPI_ROLE_MENU_BAR
                AutomationControlType.MenuItem => 34, // ATSPI_ROLE_MENU_ITEM
                AutomationControlType.ProgressBar => 38, // ATSPI_ROLE_PROGRESS_BAR
                AutomationControlType.RadioButton => 39, // ATSPI_ROLE_RADIO_BUTTON
                AutomationControlType.ScrollBar => 40, // ATSPI_ROLE_SCROLL_BAR
                AutomationControlType.Slider => 41, // ATSPI_ROLE_SLIDER
                AutomationControlType.Spinner => 41, // ATSPI_ROLE_SPIN_BUTTON
                AutomationControlType.StatusBar => 42, // ATSPI_ROLE_STATUS_BAR
                AutomationControlType.Tab => 43, // ATSPI_ROLE_PAGE_TAB
                AutomationControlType.TabItem => 43, // ATSPI_ROLE_PAGE_TAB
                AutomationControlType.Text => 42, // ATSPI_ROLE_TEXT
                AutomationControlType.ToolBar => 44, // ATSPI_ROLE_TOOL_BAR
                AutomationControlType.ToolTip => 45, // ATSPI_ROLE_TOOL_TIP
                AutomationControlType.Tree => 46, // ATSPI_ROLE_TREE
                AutomationControlType.TreeItem => 47, // ATSPI_ROLE_TREE_ITEM
                AutomationControlType.Custom => 0, // ATSPI_ROLE_UNKNOWN
                AutomationControlType.Group => 29, // ATSPI_ROLE_FILLER
                AutomationControlType.Thumb => 41, // ATSPI_ROLE_SLIDER
                AutomationControlType.DataGrid => 46, // ATSPI_ROLE_TREE_TABLE
                AutomationControlType.DataItem => 47, // ATSPI_ROLE_TREE_ITEM
                AutomationControlType.Document => 48, // ATSPI_ROLE_DOCUMENT_FRAME
                AutomationControlType.SplitButton => 26, // ATSPI_ROLE_PUSH_BUTTON
                AutomationControlType.Window => 49, // ATSPI_ROLE_WINDOW
                AutomationControlType.Pane => 29, // ATSPI_ROLE_FILLER
                AutomationControlType.Header => 36, // ATSPI_ROLE_HEADER
                AutomationControlType.HeaderItem => 22, // ATSPI_ROLE_COLUMN_HEADER
                AutomationControlType.Table => 46, // ATSPI_ROLE_TABLE
                AutomationControlType.TitleBar => 48, // ATSPI_ROLE_FRAME
                AutomationControlType.Separator => 41, // ATSPI_ROLE_SEPARATOR
                _ => 0 // ATSPI_ROLE_UNKNOWN
            };
        }
    }
}