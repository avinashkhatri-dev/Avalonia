using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Automation.Peers;
using Avalonia.FreeDesktop.Atspi;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Concrete implementation of D-Bus AT-SPI handlers for Avalonia controls
    /// </summary>
    internal class AtspiObjectHandler : OrgA11yAtspiAccessibleHandler
    {
        private readonly AtspiContext _context;
        private readonly Connection _connection;
        private readonly AutomationPeer _peer;
        private readonly AtspiRoot _root;

        public AtspiObjectHandler(AtspiContext context, Connection connection) : base()
        {
            _context = context;
            _connection = connection;
            
            // Get internal state via reflection if needed
            var contextType = typeof(AtspiContext);
            var peerField = contextType.GetField("_peer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rootField = contextType.GetField("_root", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            _peer = (AutomationPeer)peerField!.GetValue(context)!;
            _root = (AtspiRoot)rootField!.GetValue(context)!;
            
            // Initialize basic properties during construction (skip parent to avoid recursion)
            InitializeBasicProperties();
        }

        public override Connection Connection => _connection;

        // Override Parent property to provide lazy evaluation and avoid recursion
        public new (string?, ObjectPath) Parent
        {
            get
            {
                // Calculate parent reference lazily when requested
                Console.WriteLine($"[AtspiObjectHandler] Parent property accessed for {_peer.GetType().Name}");
                return GetParentReference();
            }
            set
            {
                // Allow setting during initialization
                base.Parent = value;
            }
        }

        private void InitializeBasicProperties()
        {
            Name = _peer.GetName();
            Description = _peer.GetHelpText();
            Parent = (null, default(ObjectPath)); // Defer parent setup to avoid recursion
            ChildCount = _peer.GetChildren().Count;
            Locale = "en_US";
            AccessibleId = _context.ObjectPath.ToString();
            HelpText = _peer.GetHelpText();
        }

        private void UpdateProperties()
        {
            Name = _peer.GetName();
            Description = _peer.GetHelpText();
            // Parent will be calculated lazily when needed
            ChildCount = _peer.GetChildren().Count;
            Locale = "en_US";
            AccessibleId = _context.ObjectPath.ToString();
            HelpText = _peer.GetHelpText();
        }

        private (string?, ObjectPath) GetParentReference()
        {
            try
            {
                var parent = _peer.GetParent();
                if (parent != null)
                {
                    // Check if we already have this parent in the context cache to avoid infinite loops
                    var parentContext = _root.TryGetExistingAutomationContext(parent);
                    if (parentContext != null)
                    {
                        return (_root.LocalName, parentContext.ObjectPath);
                    }
                    else
                    {
                        // If parent context doesn't exist, point to root to avoid recursion
                        return (_root.LocalName, new ObjectPath("/org/a11y/atspi/accessible/root"));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiObjectHandler] Error getting parent reference: {e.Message}");
            }
            return (null, default(ObjectPath));
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetChildAtIndexAsync(Message request, int index)
        {
            var children = _peer.GetChildren();
            if (index >= 0 && index < children.Count)
            {
                var child = children[index];
                var childContext = _root.GetOrCreateAutomationContext(child);
                if (childContext != null)
                {
                    return (_root.LocalName ?? "", childContext.ObjectPath);
                }
            }
            return ("", default(ObjectPath));
        }

        protected override async ValueTask<(string, ObjectPath)[]> OnGetChildrenAsync(Message request)
        {
            var children = new List<(string, ObjectPath)>();
            var childPeers = _peer.GetChildren();
            
            foreach (var childPeer in childPeers)
            {
                var childContext = _root.GetOrCreateAutomationContext(childPeer);
                if (childContext != null)
                {
                    children.Add((_root.LocalName ?? "", childContext.ObjectPath));
                }
            }
            return children.ToArray();
        }

        protected override async ValueTask<int> OnGetIndexInParentAsync(Message request)
        {
            var parent = _peer.GetParent();
            if (parent != null)
            {
                var siblings = parent.GetChildren();
                // Convert to list to use IndexOf
                for (int i = 0; i < siblings.Count; i++)
                {
                    if (siblings[i] == _peer)
                        return i;
                }
            }
            return -1;
        }

        protected override async ValueTask<(uint, (string, ObjectPath)[])[]> OnGetRelationSetAsync(Message request)
        {
            // No relations for now
            return Array.Empty<(uint, (string, ObjectPath)[])>();
        }

        protected override async ValueTask<uint> OnGetRoleAsync(Message request)
        {
            // Map Avalonia control types to AT-SPI roles
            // For now, use simple role mapping based on peer type
            var peerType = _peer.GetType().Name;
            return peerType switch
            {
                "ButtonAutomationPeer" => 42, // ATSPI_ROLE_PUSH_BUTTON
                "TextAutomationPeer" => 42,   // ATSPI_ROLE_TEXT
                "WindowAutomationPeer" => 68, // ATSPI_ROLE_WINDOW
                _ => 29 // ATSPI_ROLE_PANEL (default)
            };
        }

        protected override async ValueTask<string> OnGetRoleNameAsync(Message request)
        {
            var role = await OnGetRoleAsync(request);
            return role switch
            {
                42 => "push button",
                68 => "window",
                29 => "panel",
                _ => "invalid"
            };
        }

        protected override async ValueTask<string> OnGetLocalizedRoleNameAsync(Message request)
        {
            return await OnGetRoleNameAsync(request);
        }

        protected override async ValueTask<uint[]> OnGetStateAsync(Message request)
        {
            var states = new List<uint>();
            
            states.Add(1); // ATSPI_STATE_ENABLED
            states.Add(56); // ATSPI_STATE_VISIBLE
            
            if (_peer.IsEnabled())
                states.Add(34); // ATSPI_STATE_SENSITIVE
            
            if (_peer.HasKeyboardFocus())
                states.Add(12); // ATSPI_STATE_FOCUSED
            
            return states.ToArray();
        }

        protected override async ValueTask<Dictionary<string, string>> OnGetAttributesAsync(Message request)
        {
            return new Dictionary<string, string>();
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetApplicationAsync(Message request)
        {
            // Return reference to the root application object
            return (_root.LocalName ?? "", new ObjectPath("/org/a11y/atspi/accessible/root"));
        }

        protected override async ValueTask<string[]> OnGetInterfacesAsync(Message request)
        {
            return new[]
            {
                "org.a11y.atspi.Accessible",
                "org.a11y.atspi.Component"
            };
        }
    }
}