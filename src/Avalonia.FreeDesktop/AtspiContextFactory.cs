using System;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.FreeDesktop.Atspi;
using Avalonia.Threading;

namespace Avalonia.FreeDesktop
{
    internal static class AtspiContextFactory
    {
        public static AtspiContext Create(AtspiRoot root, AutomationPeer peer)
        {
            Dispatcher.UIThread.VerifyAccess();

            // If this is a LinuxControlAutomationPeer wrapper, use the wrapped peer's control type
            AutomationPeer actualPeer = peer;
            if (peer is LinuxControlAutomationPeer linuxPeer && linuxPeer.WrappedPeer != null)
            {
                actualPeer = linuxPeer.WrappedPeer;
                Console.WriteLine($"🔧 AtspiContextFactory: Using wrapped peer {actualPeer.GetType().Name} instead of {peer.GetType().Name}");
            }

            var controlType = actualPeer.GetAutomationControlType();
            Console.WriteLine($"🔧 AtspiContextFactory: Creating context for {actualPeer.GetType().Name} with ControlType: {controlType}");
            
            var role = controlType switch
            {
                AutomationControlType.Button => AtspiRole.ATSPI_ROLE_PUSH_BUTTON,
                AutomationControlType.CheckBox => AtspiRole.ATSPI_ROLE_CHECK_BOX,
                AutomationControlType.RadioButton => AtspiRole.ATSPI_ROLE_RADIO_BUTTON,
                AutomationControlType.ComboBox => AtspiRole.ATSPI_ROLE_COMBO_BOX,
                AutomationControlType.ComboBoxItem => AtspiRole.ATSPI_ROLE_LIST_ITEM,
                AutomationControlType.Edit => AtspiRole.ATSPI_ROLE_TEXT,
                AutomationControlType.Text => AtspiRole.ATSPI_ROLE_LABEL,
                AutomationControlType.Image => AtspiRole.ATSPI_ROLE_IMAGE,
                AutomationControlType.List => AtspiRole.ATSPI_ROLE_LIST,
                AutomationControlType.ListItem => AtspiRole.ATSPI_ROLE_LIST_ITEM,
                AutomationControlType.Menu => AtspiRole.ATSPI_ROLE_MENU,
                AutomationControlType.MenuBar => AtspiRole.ATSPI_ROLE_MENU_BAR,
                AutomationControlType.MenuItem => AtspiRole.ATSPI_ROLE_MENU_ITEM,
                AutomationControlType.Tab => AtspiRole.ATSPI_ROLE_PAGE_TAB_LIST,
                AutomationControlType.TabItem => AtspiRole.ATSPI_ROLE_PAGE_TAB,
                AutomationControlType.ScrollBar => AtspiRole.ATSPI_ROLE_SCROLL_BAR,
                AutomationControlType.Slider => AtspiRole.ATSPI_ROLE_SLIDER,
                AutomationControlType.ProgressBar => AtspiRole.ATSPI_ROLE_PROGRESS_BAR,
                AutomationControlType.Spinner => AtspiRole.ATSPI_ROLE_SPIN_BUTTON,
                AutomationControlType.StatusBar => AtspiRole.ATSPI_ROLE_STATUS_BAR,
                AutomationControlType.ToolBar => AtspiRole.ATSPI_ROLE_TOOL_BAR,
                AutomationControlType.ToolTip => AtspiRole.ATSPI_ROLE_TOOL_TIP,
                AutomationControlType.Tree => AtspiRole.ATSPI_ROLE_TREE,
                AutomationControlType.TreeItem => AtspiRole.ATSPI_ROLE_TREE_ITEM,
                AutomationControlType.Group => AtspiRole.ATSPI_ROLE_PANEL,
                AutomationControlType.Thumb => AtspiRole.ATSPI_ROLE_SLIDER,
                AutomationControlType.DataGrid => AtspiRole.ATSPI_ROLE_TABLE,
                AutomationControlType.DataItem => AtspiRole.ATSPI_ROLE_TABLE_CELL,
                AutomationControlType.Document => AtspiRole.ATSPI_ROLE_DOCUMENT_FRAME,
                AutomationControlType.Window => AtspiRole.ATSPI_ROLE_WINDOW,
                AutomationControlType.Pane => AtspiRole.ATSPI_ROLE_PANEL,
                AutomationControlType.Header => AtspiRole.ATSPI_ROLE_COLUMN_HEADER,
                AutomationControlType.HeaderItem => AtspiRole.ATSPI_ROLE_COLUMN_HEADER,
                AutomationControlType.Table => AtspiRole.ATSPI_ROLE_TABLE,
                AutomationControlType.TitleBar => AtspiRole.ATSPI_ROLE_TITLE_BAR,
                AutomationControlType.Separator => AtspiRole.ATSPI_ROLE_SEPARATOR,
                AutomationControlType.Custom => AtspiRole.ATSPI_ROLE_UNKNOWN,
                AutomationControlType.Calendar => AtspiRole.ATSPI_ROLE_CALENDAR,
                AutomationControlType.Hyperlink => AtspiRole.ATSPI_ROLE_LINK,
                AutomationControlType.SplitButton => AtspiRole.ATSPI_ROLE_PUSH_BUTTON,
                _ => AtspiRole.ATSPI_ROLE_UNKNOWN
            };

            Console.WriteLine($"🎯 AtspiContextFactory: Mapped {controlType} -> {role}");
            return new AtspiContext(root, peer, role);
        }
    }
}