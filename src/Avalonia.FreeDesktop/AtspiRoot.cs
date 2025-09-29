using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Platform;
using Avalonia.FreeDesktop.Atspi;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Threading;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// The root Application in the AT-SPI automation tree.
    /// </summary>
    /// <remarks>
    /// When using AT-SPI there is a single AT-SPI root object for the application. Its children are the application's
    /// open windows.
    /// </remarks>
    public class AtspiRoot : IAccessible, IApplication
    {
        private const string RootPath = "/org/a11y/atspi/accessible/root";
        private const string AtspiVersion = "2.1";

        // TODO: Not sure where to store this shared instance.
        private static AtspiRoot? _instance;
        private static bool _instanceInitialized;
        private static Connection? _connection;

        private readonly List<Child> _children = new List<Child>();
        private readonly Dictionary<AutomationPeer, AtspiContext> _contexts = new();
        private AtspiCache? _cache;
        private AccessibleProperties? _accessibleProperties;
        private ApplicationProperties? _applicationProperties;
        private string? _busName;

        public AtspiRoot()
        {
            Attributes = new Dictionary<string, string> { { "toolkit", "Avalonia" } };
        }

        public ObjectPath ObjectPath => RootPath;
        public ObjectReference ApplicationPath => new ObjectReference(_busName ?? LocalName, ObjectPath);
        public IDictionary<string, string> Attributes { get; }
        public string LocalName => _busName ?? ":1.1";

        /// <summary>
        /// Gets the current AT-SPI root instance, if initialized.
        /// </summary>
        public static AtspiRoot? Current => _instance;

        public static AtspiRoot? RegisterRoot(Func<AutomationPeer> peerGetter)
        {
            if (!_instanceInitialized)
            {
                _instance = new AtspiRoot();
                _instanceInitialized = true;
                
                // Initialize D-Bus connection
                _ = Task.Run(() => _instance.InitializeDBusAsync());
            }

            _instance?._children.Add(new Child(peerGetter));
            return _instance;
        }

        internal AtspiContext CreateAutomationContext(AutomationPeer peer)
        {
            var result = AtspiContextFactory.Create(this, peer);
            _contexts[peer] = result;
            _cache?.Add(result);
            System.Diagnostics.Debug.WriteLine($"Created {result.ObjectPath} for {peer}");
            return result;
        }

        private async Task InitializeDBusAsync()
        {
            try
            {
                // Connect to session bus
                var address = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
                if (string.IsNullOrEmpty(address))
                {
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "DBUS_SESSION_BUS_ADDRESS not set");
                    return;
                }

                _connection = new Connection(address);
                await _connection.ConnectAsync();
                
                // Get our unique name - for now use a simple identifier
                _busName = ":1.1"; // Simplified - in real implementation this would be proper unique name
                
                // Register with AT-SPI registry (if available)
                await RegisterWithAtspiAsync();
                
                // Set up our properties for AT-SPI after D-Bus is connected
                Register();
                
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI D-Bus connection established: {BusName}", _busName);
            }
            catch (Exception e)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "Failed to initialize AT-SPI D-Bus: {Error}", e);
            }
        }

        private async Task RegisterWithAtspiAsync()
        {
            try
            {
                if (_connection == null || string.IsNullOrEmpty(_busName))
                    return;

                // Try to register with the AT-SPI registry if it exists
                // This is simplified - in real implementation we'd check if the registry exists first
                var appRef = new ObjectReference(_busName, RootPath);
                
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "Prepared AT-SPI application reference");
            }
            catch (Exception e)
            {
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to register with AT-SPI registry: {Error}", e);
            }
        }

        private void Register()
        {
            try
            {
                // Set up our properties for AT-SPI
                _accessibleProperties = new AccessibleProperties
                {
                    Name = Application.Current?.Name ?? "Unnamed",
                    Description = string.Empty,
                    Locale = CultureInfo.CurrentCulture.Name,
                    ChildCount = _children.Count,
                    AccessibleId = string.Empty,
                };

                _applicationProperties = new ApplicationProperties
                {
                    Id  = 0,
                    Version = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()!.Location).FileVersion ?? "0.0.0.0",
                    AtspiVersion = AtspiVersion,
                    ToolkitName = "Avalonia",
                };

                // Create the cache.
                _cache = new AtspiCache(this);

                System.Diagnostics.Debug.WriteLine("Set up AtspiRoot");
            }
            catch (Exception e)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "Error setting up AT-SPI: {Error}", e);
            }
        }

        async Task<ObjectReference> IAccessible.GetChildAtIndexAsync(int index)
        {
            var child = _children[index];
            var peer = child.Peer;

            if (peer is null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => child.CreatePeer());
                peer = child.Peer!;
            }

            if (!_contexts.TryGetValue(peer, out var context))
            {
                // Create context if it doesn't exist
                context = CreateAutomationContext(peer);
            }

            return new ObjectReference(LocalName, context.ObjectPath);
        }

        async Task<ObjectReference[]> IAccessible.GetChildrenAsync()
        {
            var result = new ObjectReference[_children.Count];

            for (var i = 0; i < _children.Count; ++i)
            {
                result[i] = await ((IAccessible)this).GetChildAtIndexAsync(i);
            }

            return result;
        }

        Task<int> IAccessible.GetIndexInParentAsync() => Task.FromResult(-1);

        Task<(uint, ObjectReference[])[]> IAccessible.GetRelationSetAsync()
        {
            return Task.FromResult(Array.Empty<(uint, ObjectReference[])>());            
        }

        Task<uint> IAccessible.GetRoleAsync() => Task.FromResult((uint)AtspiRole.ATSPI_ROLE_APPLICATION);
        Task<string> IAccessible.GetRoleNameAsync() => Task.FromResult("application");
        Task<string> IAccessible.GetLocalizedRoleNameAsync() => Task.FromResult("application");
        Task<uint[]> IAccessible.GetStateAsync() => Task.FromResult(new uint[] { 0, 0 });
        Task<ObjectReference> IAccessible.GetApplicationAsync() => Task.FromResult(ApplicationPath);
        Task<IDictionary<string, string>> IAccessible.GetAttributesAsync() => Task.FromResult(Attributes);

        Task<string> IApplication.GetApplicationBusAddressAsync() => Task.FromResult(":1.0");
        Task<string> IApplication.GetLocaleAsync(uint lcType) => Task.FromResult(CultureInfo.CurrentCulture.Name);

        Task IApplication.RegisterEventListenerAsync(string Event)
        {
            throw new NotImplementedException();
        }

        Task IApplication.DeregisterEventListenerAsync(string Event)
        {
            throw new NotImplementedException();
        }

        Task<object?> IApplication.GetAsync(string prop)
        {
            return Task.FromResult<object?>(prop switch
            {
                nameof(ApplicationProperties.ToolkitName) => _applicationProperties!.ToolkitName,
                nameof(ApplicationProperties.Version) => _applicationProperties!.Version,
                nameof(ApplicationProperties.AtspiVersion) => _applicationProperties!.AtspiVersion,
                nameof(ApplicationProperties.Id) => _applicationProperties!.Id,
                _ => null,
            });
        }

        Task<ApplicationProperties> IApplication.GetAllAsync() => Task.FromResult(_applicationProperties!);

        Task IApplication.SetAsync(string prop, object val)
        {
            switch (prop)
            {
                case nameof(ApplicationProperties.Id):
                    _applicationProperties!.Id = (int)val;
                    break;
            }

            return Task.CompletedTask;
        }

        Task<object?> IAccessible.GetAsync(string prop)
        {
            return Task.FromResult<object?>(prop switch
            {
                nameof(AccessibleProperties.Name) => _accessibleProperties!.Name,
                nameof(AccessibleProperties.Description) => _accessibleProperties!.Description,
                nameof(AccessibleProperties.Parent) => _accessibleProperties!.Parent,
                nameof(AccessibleProperties.ChildCount) => _accessibleProperties!.ChildCount,
                nameof(AccessibleProperties.Locale) => _accessibleProperties!.Locale,
                nameof(AccessibleProperties.AccessibleId) => _accessibleProperties!.AccessibleId,
                _ => null,
            });
        }

        Task<AccessibleProperties> IAccessible.GetAllAsync() => Task.FromResult(_accessibleProperties!);

        Task IAccessible.SetAsync(string prop, object val)
        {
            throw new NotImplementedException();
        }

        private class Child
        {
            private readonly Func<AutomationPeer> _peerGetter;
            public Child(Func<AutomationPeer> peerGetter) => _peerGetter = peerGetter;
            public AutomationPeer? Peer  {  get;  private set;  }

            public void CreatePeer()
            {
                Dispatcher.UIThread.VerifyAccess();
                Peer = _peerGetter();
            }
        }
    }
}