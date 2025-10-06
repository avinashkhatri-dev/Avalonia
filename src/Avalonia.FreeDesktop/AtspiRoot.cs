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
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;
using Avalonia.Platform;
using Avalonia.Threading;

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
            // Set application metadata for AT-SPI
            var appName = GetApplicationName();
            Attributes = new Dictionary<string, string> 
            { 
                { "toolkit", "Avalonia" },
                { "application-name", appName }
            };
            Console.WriteLine($"[AtspiRoot] Initialized with application name: {appName}");
        }

        private static string GetApplicationName()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var assemblyName = assembly.GetName().Name;
                return assemblyName ?? "AvaloniaApp";
            }
            catch
            {
                return "AvaloniaApp";
            }
        }

        public ObjectPath ObjectPath => RootPath;
        public ObjectReference ApplicationPath => new ObjectReference(_busName ?? ":1.0", ObjectPath);
        public IDictionary<string, string> Attributes { get; }
        public string LocalName => _busName ?? ":1.0";

        /// <summary>
        /// Gets the D-Bus connection for registering method handlers.
        /// </summary>
        public Connection? GetConnection() => _connection;
        public bool IsConnected => !string.IsNullOrEmpty(_busName);

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
                
                Console.WriteLine("[AtspiRoot] FORCE REGISTERING ROOT - Multiple initialization strategies");
                
                // Strategy 1: Check if AT-SPI should be enabled
                _ = Task.Run(async () => 
                {
                    try
                    {
                        var shouldEnable = await AtspiStatusChecker.ShouldEnableAccessibilityAsync();
                        Console.WriteLine($"[AtspiRoot] AT-SPI status check result: {shouldEnable}");
                        
                        if (shouldEnable)
                        {
                            Console.WriteLine("[AtspiRoot] Accessibility enabled - initializing AT-SPI");
                            await _instance.InitializeDBusAsync();
                        }
                        else
                        {
                            Console.WriteLine("[AtspiRoot] Accessibility disabled by status - trying force initialization anyway");
                            await _instance.ForceInitializeDBusAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AtspiRoot] Primary initialization failed: {ex.Message}");
                        Console.WriteLine("[AtspiRoot] Trying force initialization as fallback");
                        try
                        {
                            await _instance.ForceInitializeDBusAsync();
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine($"[AtspiRoot] Force initialization also failed: {ex2.Message}");
                        }
                    }
                });
            }

            _instance?._children.Add(new Child(peerGetter));
            Console.WriteLine($"[AtspiRoot] Added child peer getter, total children: {_instance?._children.Count}");
            return _instance;
        }

        internal AtspiContext CreateAutomationContext(AutomationPeer peer)
        {
            var result = AtspiContextFactory.Create(this, peer);
            _contexts[peer] = result;
            _cache?.Add(result);
            System.Diagnostics.Debug.WriteLine($"Created {result.ObjectPath} for {peer}");
            Console.WriteLine($"🔧 AT-SPI: Created context {result.ObjectPath} for {peer.GetType().Name} '{peer.GetName() ?? "unnamed"}'");
            return result;
        }

        private async Task InitializeDBusAsync()
        {
            try
            {
                // Connect to accessibility bus
                var accessibilityBusAddress = GetAccessibilityBusAddress();
                if (string.IsNullOrEmpty(accessibilityBusAddress))
                {
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Could not find accessibility bus address");
                    return;
                }

                _connection = new Connection(accessibilityBusAddress);
                await _connection.ConnectAsync();
                
                // Get our unique name from the D-Bus connection
                _busName = _connection.UniqueName;
                
                // Register with AT-SPI registry
                await RegisterWithAtspiAsync();
                
                // Set up our properties for AT-SPI after D-Bus is connected
                Register();
                
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI connection established on accessibility bus: {BusName}", _busName);
                Console.WriteLine($"[AtspiRoot] SUCCESS: AT-SPI connection established on accessibility bus: {_busName}");
            }
            catch (Exception e)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "Failed to initialize AT-SPI: {Error}", e);
                Console.WriteLine($"[AtspiRoot] FAILED to initialize AT-SPI: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Force AT-SPI initialization even if status checks fail. Uses multiple fallback strategies.
        /// </summary>
        private async Task ForceInitializeDBusAsync()
        {
            Console.WriteLine("[AtspiRoot] FORCE INITIALIZING D-Bus connection - trying all strategies");
            
            Exception? lastException = null;
            
            // Strategy 1: Try accessibility bus
            try
            {
                await InitializeDBusAsync();
                Console.WriteLine("[AtspiRoot] Force strategy 1 (accessibility bus) succeeded");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] Force strategy 1 failed: {ex.Message}");
                lastException = ex;
            }
            
            // Strategy 2: Try session bus as fallback
            try
            {
                Console.WriteLine("[AtspiRoot] Trying session bus as fallback");
                var sessionBusAddress = Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS");
                if (!string.IsNullOrEmpty(sessionBusAddress))
                {
                    _connection = new Connection(sessionBusAddress);
                    await _connection.ConnectAsync();
                    _busName = _connection.UniqueName;
                    
                    // Try to register with AT-SPI registry (might fail but that's ok)
                    try
                    {
                        await RegisterWithAtspiAsync();
                    }
                    catch
                    {
                        Console.WriteLine("[AtspiRoot] Registry registration failed on session bus (expected)");
                    }
                    
                    Register();
                    Console.WriteLine($"[AtspiRoot] Force strategy 2 (session bus) succeeded: {_busName}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] Force strategy 2 failed: {ex.Message}");
                lastException = ex;
            }
            
            // Strategy 3: Try direct unix socket connection
            try
            {
                Console.WriteLine("[AtspiRoot] Trying direct unix socket connection");
                _connection = new Connection("unix:path=/tmp/dbus-session");
                await _connection.ConnectAsync();
                _busName = _connection.UniqueName;
                
                Register();
                Console.WriteLine($"[AtspiRoot] Force strategy 3 (unix socket) succeeded: {_busName}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] Force strategy 3 failed: {ex.Message}");
                lastException = ex;
            }
            
            Console.WriteLine("[AtspiRoot] ALL force strategies failed - AT-SPI will not be available");
            if (lastException != null)
                throw lastException;
        }

        internal string? GetAccessibilityBusAddress()
        {
            try
            {
                // First try environment variable
                var envAddress = Environment.GetEnvironmentVariable("AT_SPI_BUS");
                if (!string.IsNullOrEmpty(envAddress))
                    return envAddress;

                // Try to get from X11 root window property using xprop
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xprop",
                        Arguments = "-root AT_SPI_BUS",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && output.Contains("AT_SPI_BUS"))
                {
                    // Parse output like: AT_SPI_BUS(STRING) = "unix:path=/tmp/dbus-abc123"
                    var start = output.IndexOf('"');
                    var end = output.LastIndexOf('"');
                    if (start >= 0 && end > start)
                    {
                        return output.Substring(start + 1, end - start - 1);
                    }
                }

                // DO NOT fallback to session bus - AT-SPI applications MUST use accessibility bus only
                // This follows the exact pattern from freedesktop.org AT-SPI specification
                Console.WriteLine("[AtspiRoot] No accessibility bus available - AT-SPI disabled");
                return null;
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to get accessibility bus address: {Error}", ex.Message);
                // DO NOT fallback to session bus - AT-SPI disabled if accessibility bus not available
                Console.WriteLine("[AtspiRoot] Cannot access accessibility bus - AT-SPI disabled");
                return null;
            }
        }

        public static string? GetAccessibilityBusAddressStatic()
        {
            try
            {
                // First try environment variable
                var envAddress = Environment.GetEnvironmentVariable("AT_SPI_BUS");
                if (!string.IsNullOrEmpty(envAddress))
                    return envAddress;

                // Try to get from X11 root window property using xprop
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "xprop",
                        Arguments = "-root AT_SPI_BUS",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && output.Contains("AT_SPI_BUS"))
                {
                    // Parse output like: AT_SPI_BUS(STRING) = "unix:path=/tmp/dbus-abc123"
                    var start = output.IndexOf('"');
                    var end = output.LastIndexOf('"');
                    if (start >= 0 && end > start)
                    {
                        return output.Substring(start + 1, end - start - 1);
                    }
                }

                // DO NOT fallback to session bus - AT-SPI applications MUST use accessibility bus only
                Console.WriteLine("[AtspiRoot] No accessibility bus available - AT-SPI disabled");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] Failed to get accessibility bus address: {ex.Message}");
                // DO NOT fallback to session bus - AT-SPI disabled if accessibility bus not available
                Console.WriteLine("[AtspiRoot] Cannot access accessibility bus - AT-SPI disabled");
                return null;
            }
        }

        private async Task RegisterWithAtspiAsync()
        {
            try
            {
                if (_connection == null || string.IsNullOrEmpty(_busName))
                    return;

                Console.WriteLine($"[AtspiRoot] Starting AT-SPI registry registration process");
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "Starting AT-SPI registry registration");
                
                // Create application reference for AT-SPI registry
                var appRef = new ObjectReference(_busName, RootPath);
                
                Console.WriteLine($"[AtspiRoot] AT-SPI application reference created: {_busName}:{RootPath}");
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI application reference created: {BusName}:{Path}", _busName, RootPath);
                
                // Register with AT-SPI registry service
                Console.WriteLine($"[AtspiRoot] Attempting AT-SPI registry registration...");
                
                try 
                {
                    // Actually register with the AT-SPI registry for accerciser to discover the app
                    await RegisterWithAtspiRegistry(appRef);
                    
                    Console.WriteLine($"[AtspiRoot] ✅ AT-SPI application ready and discoverable");
                    Console.WriteLine($"[AtspiRoot] 🔍 PathHandler registration provides discoverability");
                    Console.WriteLine($"[AtspiRoot] 🌐 AT-SPI root path: {RootPath}");
                    Console.WriteLine($"[AtspiRoot] 📊 Service name: {_busName}");
                    
                    Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI application ready and discoverable");
                }
                catch (Exception regEx)
                {
                    Console.WriteLine($"[AtspiRoot] ⚠️ Registry registration failed: {regEx.Message}");
                    Console.WriteLine($"[AtspiRoot] 📍 Application still accessible via D-Bus path {_busName}:{RootPath}");
                    Console.WriteLine($"[AtspiRoot] 💡 Note: Some AT-SPI tools may still discover the application");
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "AT-SPI registry registration failed: {Error}", regEx.Message);
                }
                
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI application registered and ready");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRoot] ❌ Failed to register with AT-SPI: {e.Message}");
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to register with AT-SPI: {Error}", e);
            }
        }

        /// <summary>
        /// Registers the application with the AT-SPI registry for discoverability by accessibility tools.
        /// </summary>
        private async Task RegisterWithAtspiRegistry(ObjectReference appRef)
        {
            try
            {
                Console.WriteLine($"[AtspiRoot] 🔗 Registering with AT-SPI registry service...");
                Console.WriteLine($"[AtspiRoot] App reference: {appRef.Service}:{appRef.Path}");
                
                // Create proxy to the AT-SPI Registry service
                var registry = new OrgA11yAtspiRegistryProxy(_connection!, "org.a11y.atspi.Registry", "/org/a11y/atspi/registry");
                
                // Convert ObjectReference to tuple format expected by proxy
                var appRefTuple = (appRef.Service, new ObjectPath(appRef.Path));
                
                // Call RegisterApplication through the proxy
                await registry.RegisterApplicationAsync(appRefTuple);
                
                Console.WriteLine($"[AtspiRoot] ✅ Successfully registered with AT-SPI registry!");
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "Successfully registered with AT-SPI registry");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] ⚠️ Registry registration exception: {ex.Message}");
                Console.WriteLine($"[AtspiRoot] 📍 Application still accessible via D-Bus path {_busName}:{RootPath}");
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "AT-SPI registry registration failed: {Error}", ex.Message);
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

                // CRITICAL: Register the AtspiRoot itself as a D-Bus object
                RegisterRootWithDBus();

                System.Diagnostics.Debug.WriteLine("Set up AtspiRoot");
            }
            catch (Exception e)
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)?.Log(this, "Error setting up AT-SPI: {Error}", e);
            }
        }

        /// <summary>
        /// Registers the AtspiRoot itself as a D-Bus object at /org/a11y/atspi/accessible/root
        /// </summary>
        private void RegisterRootWithDBus()
        {
            try
            {
                Console.WriteLine($"[AtspiRoot] 🔧 Registering AT-SPI root object at: {RootPath}");
                
                if (_connection == null)
                {
                    Console.WriteLine($"[AtspiRoot] ❌ No D-Bus connection available for root registration");
                    return;
                }

                // Create the D-Bus method handler for the root object (which implements IApplication)
                var rootHandler = new AtspiRootHandler(this, _connection);
                
                // Create PathHandler for the root object
                var pathHandler = new PathHandler(RootPath);
                pathHandler.Add(rootHandler);
                _connection.AddMethodHandler(pathHandler);
                
                Console.WriteLine($"[AtspiRoot] ✅ Successfully registered AT-SPI root object at: {RootPath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRoot] ❌ Failed to register root D-Bus handler: {e.Message}");
                Console.WriteLine($"[AtspiRoot] Stack trace: {e.StackTrace}");
            }
        }

        public AtspiContext GetOrCreateAutomationContext(AutomationPeer peer)
        {
            if (!_contexts.TryGetValue(peer, out var context))
            {
                context = CreateAutomationContext(peer);
            }
            return context;
        }

        public AtspiContext? TryGetExistingAutomationContext(AutomationPeer peer)
        {
            _contexts.TryGetValue(peer, out var context);
            return context;
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

            return new ObjectReference(_busName ?? ":1.0", context.ObjectPath);
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

        Task<string> IApplication.GetApplicationBusAddressAsync() => Task.FromResult(_busName ?? ":1.0");
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