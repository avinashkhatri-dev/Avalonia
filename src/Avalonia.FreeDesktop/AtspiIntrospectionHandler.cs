using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Implements D-Bus introspection support for AT-SPI objects
    /// This allows accessibility tools like accerciser to discover and enumerate our objects
    /// </summary>
    internal class AtspiIntrospectionHandler : OrgFreedesktopDBusIntrospectableHandler
    {
        private readonly Connection _connection;
        private readonly string _introspectionXml;

        public AtspiIntrospectionHandler(Connection connection, string introspectionXml) : base()
        {
            _connection = connection;
            _introspectionXml = introspectionXml;
            
            Console.WriteLine($"[AtspiIntrospectionHandler] 🔧 Created introspection handler");
            Console.WriteLine($"[AtspiIntrospectionHandler] 📝 XML length: {introspectionXml.Length} characters");
            Console.WriteLine($"[AtspiIntrospectionHandler] 🔌 Connection: {connection?.GetType().Name ?? "null"}");
        }

        public override Connection Connection => _connection;

        protected override async ValueTask<string> OnIntrospectAsync(Message request)
        {
            Console.WriteLine($"[AtspiIntrospectionHandler] 🔍 Introspect method called!");
            Console.WriteLine($"[AtspiIntrospectionHandler] Request path: {request.PathAsString}");
            Console.WriteLine($"[AtspiIntrospectionHandler] Request sender: {request.SenderAsString}");
            Console.WriteLine($"[AtspiIntrospectionHandler] Returning XML length: {_introspectionXml.Length} characters");
            
            // Log the first few lines of the XML for verification
            var xmlLines = _introspectionXml.Split('\n');
            Console.WriteLine($"[AtspiIntrospectionHandler] XML preview: {xmlLines[0]}");
            if (xmlLines.Length > 1) Console.WriteLine($"[AtspiIntrospectionHandler] XML preview: {xmlLines[1]}");
            
            return _introspectionXml;
        }
    }
}