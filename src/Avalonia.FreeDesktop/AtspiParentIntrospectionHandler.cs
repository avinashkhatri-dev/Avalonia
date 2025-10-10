using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Handles introspection for the parent AT-SPI path to expose child objects
    /// This allows busctl tree to discover all child AT-SPI objects under /org/a11y/atspi/accessible/
    /// </summary>
    internal class AtspiParentIntrospectionHandler : OrgFreedesktopDBusIntrospectableHandler
    {
        private readonly Connection _connection;
        private readonly List<string> _childPaths;

        public AtspiParentIntrospectionHandler(Connection connection, List<string> childPaths) : base()
        {
            _connection = connection;
            _childPaths = childPaths ?? new List<string>();
            
            Console.WriteLine($"[AtspiParentIntrospectionHandler] 🔧 Created parent introspection handler");
            Console.WriteLine($"[AtspiParentIntrospectionHandler] 📝 Child paths: {string.Join(", ", _childPaths)}");
        }

        public override Connection Connection => _connection;

        protected override async ValueTask<string> OnIntrospectAsync(Message request)
        {
            Console.WriteLine($"[AtspiParentIntrospectionHandler] 🔍 Parent introspect method called!");
            Console.WriteLine($"[AtspiParentIntrospectionHandler] Request path: {request.PathAsString}");
            Console.WriteLine($"[AtspiParentIntrospectionHandler] Request sender: {request.SenderAsString}");
            
            // Create XML that lists all child nodes
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<node>");
            
            // Add standard D-Bus interfaces
            sb.AppendLine("  <interface name=\"org.freedesktop.DBus.Introspectable\">");
            sb.AppendLine("    <method name=\"Introspect\">");
            sb.AppendLine("      <arg name=\"data\" direction=\"out\" type=\"s\"/>");
            sb.AppendLine("    </method>");
            sb.AppendLine("  </interface>");
            
            // Add child nodes
            foreach (var childPath in _childPaths)
            {
                // Extract just the node name from the full path
                var nodeName = childPath.Split('/').Last();
                if (!string.IsNullOrEmpty(nodeName))
                {
                    sb.AppendLine($"  <node name=\"{nodeName}\"/>");
                }
            }
            
            sb.AppendLine("</node>");
            
            var xml = sb.ToString();
            Console.WriteLine($"[AtspiParentIntrospectionHandler] Generated XML length: {xml.Length} characters");
            Console.WriteLine($"[AtspiParentIntrospectionHandler] Generated XML: {xml}");
            
            return xml;
        }
        
        public void AddChildPath(string childPath)
        {
            if (!_childPaths.Contains(childPath))
            {
                _childPaths.Add(childPath);
                Console.WriteLine($"[AtspiParentIntrospectionHandler] Added child path: {childPath}");
            }
        }
    }
}