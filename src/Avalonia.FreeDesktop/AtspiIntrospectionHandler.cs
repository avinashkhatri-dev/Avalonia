using System;
using System.Text.RegularExpressions;
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
            Console.WriteLine($"[AtspiIntrospectionHandler] Original XML length: {_introspectionXml.Length} characters");
            
            // Remove xmlns:xsi attributes that cause busctl tree parsing errors
            string cleanedXml = _introspectionXml;
            
            // Remove xmlns:xsi namespace declarations
            cleanedXml = cleanedXml.Replace(" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "");
            
            // Remove any xsi:type attributes 
            cleanedXml = System.Text.RegularExpressions.Regex.Replace(cleanedXml, @"\s+xsi:type=""[^""]*""", "");
            
            // Remove any other xsi: attributes
            cleanedXml = System.Text.RegularExpressions.Regex.Replace(cleanedXml, @"\s+xsi:[^=]+=""[^""]*""", "");
            
            Console.WriteLine($"[AtspiIntrospectionHandler] Cleaned XML length: {cleanedXml.Length} characters");
            
            // Log the first few lines of the cleaned XML for verification
            var xmlLines = cleanedXml.Split('\n');
            Console.WriteLine($"[AtspiIntrospectionHandler] Cleaned XML preview: {xmlLines[0]}");
            if (xmlLines.Length > 1) Console.WriteLine($"[AtspiIntrospectionHandler] Cleaned XML preview: {xmlLines[1]}");
            
            return cleanedXml;
        }
    }
}