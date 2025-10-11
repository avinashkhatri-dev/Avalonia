using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Handles D-Bus introspection for AT-SPI objects
    /// This allows accessibility tools to discover our objects
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
            Console.WriteLine($"[AtspiIntrospectionHandler] 📝 XML length: {introspectionXml?.Length ?? 0} characters");
        }

        public override Connection Connection => _connection;

        protected override async ValueTask<string> OnIntrospectAsync(Message request)
        {
            Console.WriteLine("[AtspiIntrospectionHandler] 🔍 INTROSPECT METHOD CALLED!");
            Console.WriteLine($"[AtspiIntrospectionHandler] Request path: {request.PathAsString}");
            Console.WriteLine($"[AtspiIntrospectionHandler] Request sender: {request.SenderAsString}");

            var cleanXml = _introspectionXml;
            if (!string.IsNullOrEmpty(cleanXml))
            {
                Console.WriteLine($"[AtspiIntrospectionHandler] Original XML length: {cleanXml.Length} characters");
                Console.WriteLine($"[AtspiIntrospectionHandler] First 200 chars: {cleanXml.Substring(0, Math.Min(200, cleanXml.Length))}");

                // Comprehensive xmlns:xsi cleaning - multiple patterns to catch all variations
                cleanXml = Regex.Replace(cleanXml, @"\s+xmlns:xsi\s*=\s*""[^""]*""", "", RegexOptions.IgnoreCase);
                cleanXml = Regex.Replace(cleanXml, @"\s+xmlns:xsi\s*=\s*'[^']*'", "", RegexOptions.IgnoreCase);
                cleanXml = Regex.Replace(cleanXml, @"\s+xsi:[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*""[^""]*""", "", RegexOptions.IgnoreCase);
                cleanXml = Regex.Replace(cleanXml, @"\s+xsi:[a-zA-Z_][a-zA-Z0-9_]*\s*=\s*'[^']*'", "", RegexOptions.IgnoreCase);

                // Additional cleanup - remove any remaining xsi: references
                cleanXml = Regex.Replace(cleanXml, @"\s+xsi:\w+", "", RegexOptions.IgnoreCase);

                // Log the cleaned XML for verification
                Console.WriteLine("[AtspiIntrospectionHandler] XML after cleaning:");
                Console.WriteLine(cleanXml);

                Console.WriteLine($"[AtspiIntrospectionHandler] Cleaned XML length: {cleanXml.Length} characters");
                Console.WriteLine($"[AtspiIntrospectionHandler] Cleaned first 200 chars: {cleanXml.Substring(0, Math.Min(200, cleanXml.Length))}");
            }
            else
            {
                Console.WriteLine("[AtspiIntrospectionHandler] Warning: introspection XML is null or empty");
                cleanXml = "<?xml version=\"1.0\"?>\n<node>\n</node>";
            }

            Console.WriteLine($"[AtspiIntrospectionHandler] Generated XML length: {cleanXml.Length} characters");
            Console.WriteLine("[AtspiIntrospectionHandler] Returning cleaned XML");

            return cleanXml;
        }
    }
}
