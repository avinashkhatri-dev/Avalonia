using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.FreeDesktop.Atspi;
using Avalonia.Platform;
using Avalonia.Threading;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// A node in the AT-SPI UI automation tree.
    /// </summary>
    /// <remarks>
    /// This class provides AT-SPI support for automation peers on Linux.
    /// </remarks>
    public class AtspiContext : IAccessible, IComponent
    {
        private static uint _id;
        internal readonly AtspiRoot _root;
        internal readonly AutomationPeer _peer;
        private readonly AtspiRole _role;
        
        // Properties exposed via org.freedesktop.DBus.Properties interface
        public string Name 
        {
            get
            {
                try
                {
                    Console.WriteLine($"[AtspiContext.Name] Getting name for {ObjectPath}");
                    Console.WriteLine($"[AtspiContext.Name] _peer type: {_peer.GetType().Name}");
                    Console.WriteLine($"[AtspiContext.Name] _peer full type: {_peer.GetType().FullName}");
                    
                    // D-Bus calls come from background thread, but Avalonia properties require UI thread
                    var name = Dispatcher.UIThread.Invoke(() => {
                        Console.WriteLine($"[AtspiContext.Name] On UI thread, calling _peer.GetName()");
                        var result = _peer.GetName();
                        Console.WriteLine($"[AtspiContext.Name] _peer.GetName() returned: '{result ?? "NULL"}'");
                        return result;
                    }) ?? "";
                    
                    Console.WriteLine($"[AtspiContext] Property 'Name' accessed for {ObjectPath}: '{name}'");
                    return name;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiContext] ERROR getting Name for {ObjectPath}: {e}");
                    return "";
                }
            }
        }
        
        public string Description 
        {
            get
            {
                try
                {
                    var desc = Dispatcher.UIThread.Invoke(() => _peer.GetHelpText()) ?? "";
                    Console.WriteLine($"[AtspiContext] Property 'Description' accessed for {ObjectPath}: '{desc}'");
                    return desc;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiContext] ERROR getting Description for {ObjectPath}: {e}");
                    return "";
                }
            }
        }
        
        public ObjectReference Parent
        {
            get
            {
                try
                {
                    Console.WriteLine($"[AtspiContext] Property 'Parent' accessed for {ObjectPath}");
                    var parentPeer = Dispatcher.UIThread.Invoke(() => _peer.GetParent());
                    if (parentPeer != null)
                    {
                        var parentContext = _root.GetOrCreateAutomationContext(parentPeer);
                        if (parentContext != null)
                        {
                            Console.WriteLine($"[AtspiContext] Parent found: {_root.LocalName}:{parentContext.ObjectPath}");
                            return new ObjectReference(_root.LocalName, parentContext.ObjectPath);
                        }
                    }
                    
                    // Default to root application object
                    Console.WriteLine($"[AtspiContext] Parent defaulting to root: {_root.LocalName}:{_root.ObjectPath}");
                    return _root.ApplicationPath;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiContext] ERROR getting Parent for {ObjectPath}: {e}");
                    Console.WriteLine($"[AtspiContext] Stack trace: {e.StackTrace}");
                    // Return root as fallback
                    return _root.ApplicationPath;
                }
            }
        }
        
        public int ChildCount 
        {
            get
            {
                try
                {
                    var count = Dispatcher.UIThread.Invoke(() => _peer.GetChildren()?.Count ?? 0);
                    Console.WriteLine($"[AtspiContext] Property 'ChildCount' accessed for {ObjectPath}: {count}");
                    return count;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiContext] ERROR getting ChildCount for {ObjectPath}: {e}");
                    return 0;
                }
            }
        }
        
        public string Locale 
        {
            get
            {
                try
                {
                    var locale = System.Globalization.CultureInfo.CurrentCulture.Name;
                    Console.WriteLine($"[AtspiContext] Property 'Locale' accessed for {ObjectPath}: '{locale}'");
                    return locale;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiContext] ERROR getting Locale for {ObjectPath}: {e}");
                    return "en-US";
                }
            }
        }
        
        public AtspiContext(AtspiRoot root, AutomationPeer peer, AtspiRole role)
        {
            _root = root;
            _peer = peer;
            _role = role;
            ObjectPath = new ObjectPath("/org/a11y/atspi/accessible/" + ++_id);
            
            // Debug logging for object lifecycle
            var peerType = peer.GetType().Name;
            var peerName = peer.GetName() ?? "No name";
            Console.WriteLine($"🔧 AtspiContext created: {peerType} '{peerName}' -> {ObjectPath}");
            Console.WriteLine($"   Role: {role}, ID: {_id}");
            Console.WriteLine($"🔧 AT-SPI: Created context {ObjectPath} for {peerType} '{peerName}'");
        }

        public ObjectPath ObjectPath { get; }

        public CacheItem ToCacheItem()
        {
            return new CacheItem(
                new ObjectReference(_root.LocalName, ObjectPath),
                _root.ApplicationPath,
                _root.ApplicationPath,
                new ObjectReference[0],
                new[] { "org.a11y.atspi.Accessible", "org.a11y.atspi.Component" },
                _peer.GetName() ?? "",
                (uint)_role,
                _peer.GetHelpText() ?? "",
                new[] { 0, 0 });
        }

        Task<ObjectReference> IAccessible.GetChildAtIndexAsync(int Index)
        {
            try
            {
                var children = _peer.GetChildren();
                if (Index >= 0 && Index < children.Count)
                {
                    var childPeer = children[Index];
                    var childContext = _root.GetOrCreateAutomationContext(childPeer);
                    if (childContext != null)
                    {
                        return Task.FromResult(new ObjectReference(_root.LocalName, childContext.ObjectPath));
                    }
                }
            }
            catch (Exception e)
            {
                // Log error but don't crash
                Console.WriteLine($"Error getting child at index {Index}: {e.Message}");
            }
            
            // Return invalid reference if child not found
            return Task.FromResult(new ObjectReference("", new ObjectPath("/org/a11y/atspi/null")));
        }

        Task<ObjectReference[]> IAccessible.GetChildrenAsync()
        {
            try
            {
                Console.WriteLine($"🔍 AT-SPI GetChildren called for: {_peer.GetType().Name} '{_peer.GetName() ?? "No name"}'");
                var children = _peer.GetChildren();
                Console.WriteLine($"AT-SPI GetChildren: Found {children.Count} children for {_peer.GetType().Name}");
                
                var result = new List<ObjectReference>();
                
                foreach (var childPeer in children)
                {
                    Console.WriteLine($"  🔄 Processing child: {childPeer.GetType().Name} - {childPeer.GetName() ?? "No name"}");
                    var childContext = _root.GetOrCreateAutomationContext(childPeer);
                    if (childContext != null)
                    {
                        result.Add(new ObjectReference(_root.LocalName, childContext.ObjectPath));
                        Console.WriteLine($"    ✅ Created context: {childContext.ObjectPath} for {childPeer.GetType().Name}");
                    }
                    else
                    {
                        Console.WriteLine($"    ❌ Failed to create context for {childPeer.GetType().Name}");
                    }
                }
                
                Console.WriteLine($"AT-SPI GetChildren: Returning {result.Count} child references");
                return Task.FromResult(result.ToArray());
            }
            catch (Exception e)
            {
                // Log error but don't crash
                Console.WriteLine($"Error getting children: {e.Message}");
                return Task.FromResult(Array.Empty<ObjectReference>());
            }
        }

        Task<int> IAccessible.GetIndexInParentAsync()
        {
            try
            {
                var parent = _peer.GetParent();
                if (parent != null)
                {
                    var siblings = parent.GetChildren();
                    for (int i = 0; i < siblings.Count; i++)
                    {
                        if (siblings[i] == _peer)
                        {
                            return Task.FromResult(i);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error getting index in parent: {e.Message}");
            }
            
            return Task.FromResult(-1);
        }

        Task<(uint, ObjectReference[])[]> IAccessible.GetRelationSetAsync()
        {
            // Return empty relations for now
            // In a full implementation, this would return relationships like LABELLED_BY, MEMBER_OF, etc.
            return Task.FromResult(Array.Empty<(uint, ObjectReference[])>());
        }

        Task<uint> IAccessible.GetRoleAsync() => Task.FromResult((uint)_role);
        Task<string> IAccessible.GetRoleNameAsync() => Task.FromResult(GetRoleName(_role));
        Task<string> IAccessible.GetLocalizedRoleNameAsync() => Task.FromResult(GetLocalizedRoleName(_role));
        Task<uint[]> IAccessible.GetStateAsync() => Task.FromResult(GetAccessibleStates());
        Task<IDictionary<string, string>> IAccessible.GetAttributesAsync() => Task.FromResult(_root.Attributes);
        Task<ObjectReference> IAccessible.GetApplicationAsync() => Task.FromResult(_root.ApplicationPath);

        // IComponent implementation - Required for accerciser hover/highlighting
        Task<bool> IComponent.ContainsAsync(int x, int y, uint coord_type)
        {
            try
            {
                var bounds = GetControlBounds();
                if (bounds.HasValue)
                {
                    var (left, top, width, height) = bounds.Value;
                    return Task.FromResult(x >= left && x < left + width && y >= top && y < top + height);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in ContainsAsync: {e.Message}");
            }
            return Task.FromResult(false);
        }

        Task<ObjectReference> IComponent.GetAccessibleAtPointAsync(int x, int y, uint coord_type)
        {
            try
            {
                // Check if point is within this control
                var bounds = GetControlBounds();
                if (bounds.HasValue)
                {
                    var (left, top, width, height) = bounds.Value;
                    if (x >= left && x < left + width && y >= top && y < top + height)
                    {
                        // Check children first
                        var children = _peer.GetChildren();
                        foreach (var child in children)
                        {
                            var childContext = _root.GetOrCreateAutomationContext(child);
                            if (childContext != null)
                            {
                                var childBounds = GetControlBounds(child);
                                if (childBounds.HasValue)
                                {
                                    var (cleft, ctop, cwidth, cheight) = childBounds.Value;
                                    if (x >= cleft && x < cleft + cwidth && y >= ctop && y < ctop + cheight)
                                    {
                                        return Task.FromResult(new ObjectReference(_root.LocalName, childContext.ObjectPath));
                                    }
                                }
                            }
                        }
                        
                        // If no child contains the point, return this control
                        return Task.FromResult(new ObjectReference(_root.LocalName, ObjectPath));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GetAccessibleAtPointAsync: {e.Message}");
            }
            
            return Task.FromResult(new ObjectReference("", new ObjectPath("/org/a11y/atspi/null")));
        }

        Task<(int, int, int, int)> IComponent.GetExtentsAsync(uint coord_type)
        {
            try
            {
                var bounds = GetControlBounds();
                if (bounds.HasValue)
                {
                    return Task.FromResult(bounds.Value);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GetExtentsAsync: {e.Message}");
            }
            return Task.FromResult((0, 0, 0, 0));
        }

        Task<(int, int)> IComponent.GetPositionAsync(uint coord_type)
        {
            try
            {
                var bounds = GetControlBounds();
                if (bounds.HasValue)
                {
                    var (left, top, _, _) = bounds.Value;
                    return Task.FromResult((left, top));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GetPositionAsync: {e.Message}");
            }
            return Task.FromResult((0, 0));
        }

        Task<(int, int)> IComponent.GetSizeAsync()
        {
            try
            {
                var bounds = GetControlBounds();
                if (bounds.HasValue)
                {
                    var (_, _, width, height) = bounds.Value;
                    return Task.FromResult((width, height));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GetSizeAsync: {e.Message}");
            }
            return Task.FromResult((0, 0));
        }

        Task<uint> IComponent.GetLayerAsync() => Task.FromResult(0u); // Layer 0 = widget layer
        Task<short> IComponent.GetMDIZOrderAsync() => Task.FromResult((short)0);
        Task<double> IComponent.GetAlphaAsync() => Task.FromResult(1.0); // Fully opaque

        Task<bool> IComponent.GrabFocusAsync()
        {
            try
            {
                // Try to focus the control if it's focusable
                if (_peer is IInvokeProvider invokeProvider)
                {
                    invokeProvider.Invoke();
                    return Task.FromResult(true);
                }
                
                // For text controls, try to set focus
                var automationId = _peer.GetAutomationId();
                if (!string.IsNullOrEmpty(automationId) && automationId.Contains("Text"))
                {
                    return Task.FromResult(true); // Assume focus succeeded
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in GrabFocusAsync: {e.Message}");
            }
            return Task.FromResult(false);
        }

        // These methods are for setting position/size - not typically used by accerciser
        Task<bool> IComponent.SetExtentsAsync(int x, int y, int width, int height, uint coord_type) => Task.FromResult(false);
        Task<bool> IComponent.SetPositionAsync(int x, int y, uint coord_type) => Task.FromResult(false);
        Task<bool> IComponent.SetSizeAsync(int width, int height) => Task.FromResult(false);
        Task IComponent.ScrollToAsync(uint type) => Task.CompletedTask;
        Task<bool> IComponent.ScrollToPointAsync(uint coord_type, int x, int y) => Task.FromResult(false);

        private (int left, int top, int width, int height)? GetControlBounds(AutomationPeer? peer = null)
        {
            try
            {
                var targetPeer = peer ?? _peer;
                
                // Try to get bounds from the automation peer
                // Simplified implementation - for now just return default bounds
                // TODO: Implement proper bounds detection from visual elements
                
                var element = GetVisualElement(targetPeer);
                if (element != null)
                {
                    var bounds = element.Bounds;
                    var position = element.PointToScreen(new Avalonia.Point(0, 0));
                    return ((int)position.X, (int)position.Y, (int)bounds.Width, (int)bounds.Height);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error getting control bounds: {e.Message}");
            }
            
            // Default fallback bounds
            return (100, 100, 200, 30);
        }

        private Avalonia.Visual? GetVisualElement(AutomationPeer peer)
        {
            try
            {
                // Use reflection to get the associated visual element from the peer
                var field = peer.GetType().GetField("_owner", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field?.GetValue(peer) is Avalonia.Visual visual)
                {
                    return visual;
                }
            }
            catch
            {
                // Ignore reflection errors
            }
            return null;
        }

        /// <summary>
        /// Gets the proper AT-SPI role name for the given role.
        /// </summary>
        private static string GetRoleName(AtspiRole role)
        {
            return role switch
            {
                AtspiRole.ATSPI_ROLE_APPLICATION => "application",
                AtspiRole.ATSPI_ROLE_WINDOW => "window",
                AtspiRole.ATSPI_ROLE_FRAME => "frame",
                AtspiRole.ATSPI_ROLE_DIALOG => "dialog",
                AtspiRole.ATSPI_ROLE_PANEL => "panel",
                AtspiRole.ATSPI_ROLE_PUSH_BUTTON => "push button",
                AtspiRole.ATSPI_ROLE_TOGGLE_BUTTON => "toggle button",
                AtspiRole.ATSPI_ROLE_CHECK_BOX => "check box",
                AtspiRole.ATSPI_ROLE_RADIO_BUTTON => "radio button",
                AtspiRole.ATSPI_ROLE_ENTRY => "entry",
                AtspiRole.ATSPI_ROLE_TEXT => "text",
                AtspiRole.ATSPI_ROLE_LABEL => "label",
                AtspiRole.ATSPI_ROLE_LIST => "list",
                AtspiRole.ATSPI_ROLE_LIST_ITEM => "list item",
                AtspiRole.ATSPI_ROLE_MENU_BAR => "menu bar",
                AtspiRole.ATSPI_ROLE_MENU => "menu",
                AtspiRole.ATSPI_ROLE_MENU_ITEM => "menu item",
                AtspiRole.ATSPI_ROLE_COMBO_BOX => "combo box",
                AtspiRole.ATSPI_ROLE_SCROLL_BAR => "scroll bar",
                AtspiRole.ATSPI_ROLE_SLIDER => "slider",
                AtspiRole.ATSPI_ROLE_PROGRESS_BAR => "progress bar",
                AtspiRole.ATSPI_ROLE_TABLE => "table",
                AtspiRole.ATSPI_ROLE_TABLE_CELL => "table cell",
                AtspiRole.ATSPI_ROLE_TABLE_COLUMN_HEADER => "column header",
                AtspiRole.ATSPI_ROLE_TABLE_ROW_HEADER => "row header",
                AtspiRole.ATSPI_ROLE_TREE => "tree",
                AtspiRole.ATSPI_ROLE_TREE_ITEM => "tree item",
                AtspiRole.ATSPI_ROLE_TREE_TABLE => "tree table",
                AtspiRole.ATSPI_ROLE_DOCUMENT_TEXT => "document",
                AtspiRole.ATSPI_ROLE_PARAGRAPH => "paragraph",
                AtspiRole.ATSPI_ROLE_SECTION => "section",
                AtspiRole.ATSPI_ROLE_REDUNDANT_OBJECT => "redundant object",
                AtspiRole.ATSPI_ROLE_FORM => "form",
                AtspiRole.ATSPI_ROLE_LINK => "link",
                AtspiRole.ATSPI_ROLE_INPUT_METHOD_WINDOW => "input method window",
                AtspiRole.ATSPI_ROLE_TABLE_ROW => "table row",
                AtspiRole.ATSPI_ROLE_PASSWORD_TEXT => "password text",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Gets the localized AT-SPI role name for the given role.
        /// </summary>
        private static string GetLocalizedRoleName(AtspiRole role)
        {
            // For now, return the same as GetRoleName
            // In a full implementation, this would return localized strings
            return GetRoleName(role);
        }

        /// <summary>
        /// Gets the AT-SPI states for this accessible object.
        /// </summary>
        private uint[] GetAccessibleStates()
        {
            var states = new List<uint>();

            try
            {
                // Get the control/visual element to check its state
                var visual = GetVisualElement(_peer);
                if (visual is Control control)
                {
                    // Add appropriate states based on control properties
                    states.Add((uint)AtspiState.Enabled); // Most controls are enabled by default

                    if (control.IsVisible)
                        states.Add((uint)AtspiState.Visible);

                    if (control.IsEffectivelyVisible)
                        states.Add((uint)AtspiState.Showing);

                    if (control.IsFocused)
                        states.Add((uint)AtspiState.Focused);

                    if (control.Focusable)
                        states.Add((uint)AtspiState.Focusable);

                    // Add role-specific states
                    switch (_role)
                    {
                        case AtspiRole.ATSPI_ROLE_PUSH_BUTTON:
                        case AtspiRole.ATSPI_ROLE_TOGGLE_BUTTON:
                            states.Add((uint)AtspiState.Sensitive);
                            break;
                        case AtspiRole.ATSPI_ROLE_ENTRY:
                            states.Add((uint)AtspiState.Editable);
                            states.Add((uint)AtspiState.Sensitive);
                            break;
                        case AtspiRole.ATSPI_ROLE_CHECK_BOX:
                        case AtspiRole.ATSPI_ROLE_RADIO_BUTTON:
                            // TODO: Check if checked state can be determined
                            states.Add((uint)AtspiState.Sensitive);
                            break;
                    }
                }
                else
                {
                    // Default states for non-control elements
                    states.Add((uint)AtspiState.Enabled);
                    states.Add((uint)AtspiState.Visible);
                    states.Add((uint)AtspiState.Showing);
                }

                Console.WriteLine($"[AtspiContext] States for {_role}: [{string.Join(", ", states)}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiContext] Error getting states: {ex.Message}");
                // Fallback to basic states
                states.Add((uint)AtspiState.Enabled);
                states.Add((uint)AtspiState.Visible);
            }

            // AT-SPI uses a uint[2] array for states (64-bit total)
            var stateArray = new uint[2];
            
            // Pack states into the array
            foreach (var state in states)
            {
                if (state < 32)
                    stateArray[0] |= (uint)(1 << (int)state);
                else if (state < 64)
                    stateArray[1] |= (uint)(1 << (int)(state - 32));
            }

            return stateArray;
        }
    }

    /// <summary>
    /// AT-SPI states for accessible objects based on freedesktop.org specification.
    /// </summary>
    public enum AtspiState : uint
    {
        Invalid = 0,
        Active = 1,
        Armed = 2,
        Busy = 3,
        Checked = 4,
        Collapsed = 5,
        Defunct = 6,
        Editable = 7,
        Enabled = 8,
        Expandable = 9,
        Expanded = 10,
        Focusable = 11,
        Focused = 12,
        HasTooltip = 13,
        Horizontal = 14,
        Iconified = 15,
        Modal = 16,
        MultiLine = 17,
        Multiselectable = 18,
        Opaque = 19,
        Pressed = 20,
        Resizable = 21,
        Selectable = 22,
        Selected = 23,
        Sensitive = 24,
        Showing = 25,
        SingleLine = 26,
        Stale = 27,
        Transient = 28,
        Vertical = 29,
        Visible = 30,
        ManagesDescendants = 31,
        Indeterminate = 32,
        Required = 33,
        Truncated = 34,
        Animated = 35,
        InvalidEntry = 36,
        SupportsAutocompletion = 37,
        SelectableText = 38,
        IsDefault = 39,
        Visited = 40,
        Checkable = 41,
        HasPopup = 42,
        ReadOnly = 43
    }
}