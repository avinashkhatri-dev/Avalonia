using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;
using Avalonia.FreeDesktop.Atspi;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus method handler for AT-SPI Application interface methods.
    /// This handles application-level AT-SPI functionality.
    /// </summary>
    internal class AtspiApplicationMethodHandler : OrgA11yAtspiApplicationHandler
    {
        private readonly IApplication _application;
        private readonly Connection _connection;

        internal AtspiApplicationMethodHandler(IApplication application, Connection connection) : base()
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiApplicationMethodHandler] Created for application object");
        }

        public override Connection Connection => _connection;

        /// <summary>
        /// Gets the list of interfaces supported by this application.
        /// </summary>
        protected override async ValueTask<string[]> OnGetApplicationInterfacesAsync(Message request)
        {
            // Return the list of AT-SPI interfaces this application supports
            var interfaces = new[] { "org.a11y.atspi.Application", "org.a11y.atspi.Accessible" };
            Console.WriteLine($"[AtspiApplicationMethodHandler] GetApplicationInterfaces() -> [{string.Join(", ", interfaces)}]");
            return interfaces;
        }
    }
}