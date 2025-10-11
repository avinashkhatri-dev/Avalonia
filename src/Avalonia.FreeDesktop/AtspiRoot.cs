using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

        private List<Child> _children = new List<Child>();
        private readonly Dictionary<AutomationPeer, AtspiContext> _contexts = new();
        private readonly HashSet<string> _registeredPaths = new(); // Track D-Bus registered paths
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
            Console.WriteLine("[AtspiRoot] RegisterRoot called");
            try
            {
                if (!_instanceInitialized)
                {
                    Console.WriteLine("[AtspiRoot] Instance not initialized. Initializing now...");
                    _instance = new AtspiRoot();
                    
                    // Mark as initialized BEFORE starting the async task
                    _instance.CompleteInitialization();
                    _instanceInitialized = true;

                    Console.WriteLine("[AtspiRoot] Registering root with AT-SPI bus...");

                    // Start the async initialization task (non-blocking)
                    _ = Task.Run(async () => 
                    {
                        Console.WriteLine("[AtspiRoot] 🔄 Async initialization task started...");
                        try
                        {
                            Console.WriteLine("[AtspiRoot] 🔍 Getting accessibility bus address...");
                            var busAddress = _instance.GetAccessibilityBusAddress();
                            if (string.IsNullOrEmpty(busAddress))
                            {
                                Console.WriteLine("[AtspiRoot] ❌ Failed to retrieve AT-SPI bus address.");
                                return;
                            }

                            Console.WriteLine($"[AtspiRoot] 🚀 Connecting to AT-SPI bus at: {busAddress}");
                            _connection = new Connection(busAddress);
                            await _connection.ConnectAsync();

                            Console.WriteLine("[AtspiRoot] ✅ Successfully connected to AT-SPI bus.");

                            // Set up the bus name and register D-Bus handlers
                            _instance._busName = _connection.UniqueName;
                            _instance.Register();
                            Console.WriteLine("[AtspiRoot] ✅ Root object registered on AT-SPI bus.");
                            Console.WriteLine("[AtspiRoot] ✅ AT-SPI initialization completed successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AtspiRoot] ❌ Error during AT-SPI bus registration: {ex.Message}");
                            Console.WriteLine($"[AtspiRoot] Stack trace: {ex.StackTrace}");
                        }
                    });
                    
                    Console.WriteLine("[AtspiRoot] 📋 Initialization task created and running in background...");
                }

                var child = new Child(peerGetter);
                child.CreatePeer();
                lock (_instance._children)
                {
                    _instance._children.Add(child);
                    Console.WriteLine($"[AtspiRoot] Added child. Thread: {Thread.CurrentThread.ManagedThreadId}, Total children: {_instance._children.Count}");
                }
                Console.WriteLine("[AtspiRoot] RegisterRoot completed successfully.");
                return _instance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] Error in RegisterRoot: {ex.Message}");
                return null;
            }
        }

        internal AtspiContext CreateAutomationContext(AutomationPeer peer)
        {
            // Check if context already exists for this peer
            if (_contexts.TryGetValue(peer, out var existingContext))
            {
                Console.WriteLine($"🔧 AT-SPI: Reusing existing context {existingContext.ObjectPath} for {peer.GetType().Name} '{peer.GetName() ?? "unnamed"}'");
                return existingContext;
            }
            
            var result = AtspiContextFactory.Create(this, peer);
            _contexts[peer] = result;
            _cache?.Add(result);
            System.Diagnostics.Debug.WriteLine($"Created {result.ObjectPath} for {peer}");
            Console.WriteLine($"🔧 AT-SPI: Created context {result.ObjectPath} for {peer.GetType().Name} '{peer.GetName() ?? "unnamed"}'");
            
            // Only register child context with D-Bus if we have a connection, path not already registered, AND root is registered
            if (_connection != null && 
                !_registeredPaths.Contains(result.ObjectPath.ToString()) &&
                _registeredPaths.Contains(RootPath))
            {
                RegisterChildContextWithDBus(result);
                _registeredPaths.Add(result.ObjectPath.ToString());
                Console.WriteLine($"🔧 AT-SPI: Registered new path {result.ObjectPath} with D-Bus");
            }
            else if (_registeredPaths.Contains(result.ObjectPath.ToString()))
            {
                Console.WriteLine($"🔧 AT-SPI: Path {result.ObjectPath} already registered with D-Bus, skipping registration");
            }
            else if (!_registeredPaths.Contains(RootPath))
            {
                Console.WriteLine($"🔧 AT-SPI: Root not yet registered, deferring registration of {result.ObjectPath}");
            }
            
            return result;
        }

        private async Task InitializeDBusAsync()
        {
            try
            {
                // AT-SPI MUST use the dedicated accessibility bus, not the session bus
                Console.WriteLine("[AtspiRoot] 🔍 Getting AT-SPI accessibility bus address...");
                var accessibilityBusAddress = GetAccessibilityBusAddress();
                Console.WriteLine($"[AtspiRoot] 📍 Accessibility bus address: {accessibilityBusAddress ?? "NULL"}");
                
                if (string.IsNullOrEmpty(accessibilityBusAddress))
                {
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Could not find accessibility bus address");
                    Console.WriteLine("[AtspiRoot] ❌ No accessibility bus found - AT-SPI disabled");
                    return;
                }

                Console.WriteLine($"[AtspiRoot] 🚀 Connecting to AT-SPI accessibility bus: {accessibilityBusAddress}");
                _connection = new Connection(accessibilityBusAddress);
                await _connection.ConnectAsync();
                Console.WriteLine($"[AtspiRoot] ✅ Connected to AT-SPI accessibility bus");
                
                // Get our unique name from the D-Bus connection
                _busName = _connection.UniqueName;
                Console.WriteLine($"[AtspiRoot] 🚀 Using D-Bus service: {_busName}");
                
                // Register D-Bus method handlers for root object
                Register();
                
                // Register with AT-SPI registry
                await RegisterWithAtspiAsync();
                
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
            
            // Strategy 1: Try main InitializeDBusAsync (which now reuses main connection)
            try
            {
                await InitializeDBusAsync();
                Console.WriteLine("[AtspiRoot] Force strategy 1 (main connection reuse) succeeded");
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
                    Console.WriteLine($"[AtspiRoot] 🚀 Using fallback D-Bus service: {_busName}");
                    
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
                Console.WriteLine("[AtspiRoot] 🔍 Detecting AT-SPI accessibility bus address...");
                
                // First try environment variable
                var envAddress = Environment.GetEnvironmentVariable("AT_SPI_BUS");
                if (!string.IsNullOrEmpty(envAddress))
                {
                    Console.WriteLine($"[AtspiRoot] ✅ Found AT-SPI bus via AT_SPI_BUS environment variable: {envAddress}");
                    return envAddress;
                }
                
                // Try standard location for user session
                var userId = Environment.GetEnvironmentVariable("UID") ?? 
                           Environment.GetEnvironmentVariable("USER") ?? "1000";
                
                // Get actual user ID from environment or system
                var actualUserId = "1000"; // default fallback
                try
                {
                    var whoamiResult = Process.Start(new ProcessStartInfo
                    {
                        FileName = "id",
                        Arguments = "-u",
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    });
                    if (whoamiResult != null)
                    {
                        whoamiResult.WaitForExit();
                        var idOutput = whoamiResult.StandardOutput.ReadToEnd().Trim();
                        if (!string.IsNullOrEmpty(idOutput) && int.TryParse(idOutput, out _))
                        {
                            actualUserId = idOutput;
                        }
                    }
                }
                catch { /* ignore errors, use fallback */ }
                
                var standardPath = $"unix:path=/run/user/{actualUserId}/at-spi/bus";
                Console.WriteLine($"[AtspiRoot] 🔍 Trying standard AT-SPI bus path: {standardPath}");
                
                // Check if the socket file exists
                var socketPath = standardPath.Replace("unix:path=", "");
                if (File.Exists(socketPath))
                {
                    Console.WriteLine($"[AtspiRoot] ✅ Found AT-SPI bus socket at: {standardPath}");
                    return standardPath;
                }

                // Try to get from X11 root window property using xprop
                Console.WriteLine("[AtspiRoot] 🔍 Trying X11 root window property via xprop...");
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
                    Console.WriteLine($"[AtspiRoot] 📄 xprop output: {output.Trim()}");
                    // Parse output like: AT_SPI_BUS(STRING) = "unix:path=/tmp/dbus-abc123"
                    var start = output.IndexOf('"');
                    var end = output.LastIndexOf('"');
                    if (start >= 0 && end > start)
                    {
                        var busAddress = output.Substring(start + 1, end - start - 1);
                        Console.WriteLine($"[AtspiRoot] ✅ Found AT-SPI bus via xprop: {busAddress}");
                        return busAddress;
                    }
                }

                // DO NOT fallback to session bus - AT-SPI applications MUST use accessibility bus only
                // This follows the exact pattern from freedesktop.org AT-SPI specification
                Console.WriteLine("[AtspiRoot] ❌ No accessibility bus available - AT-SPI disabled");
                return null;
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to get accessibility bus address: {Error}", ex.Message);
                // DO NOT fallback to session bus - AT-SPI disabled if accessibility bus not available
                Console.WriteLine($"[AtspiRoot] ❌ Exception finding accessibility bus: {ex.Message}");
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
                if (ex.Message.Contains("UnknownMethod") && ex.Message.Contains("RegisterApplication"))
                {
                    Console.WriteLine($"[AtspiRoot] ℹ️ Registry does not support application registration (common on modern systems)");
                    Console.WriteLine($"[AtspiRoot] ✅ Application is discoverable on accessibility bus at {_busName}:{RootPath}");
                    Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI registry does not require application registration - app is accessible on bus");
                }
                else
                {
                    Console.WriteLine($"[AtspiRoot] ⚠️ Registry registration exception: {ex.Message}");
                    Console.WriteLine($"[AtspiRoot] 📍 Application still accessible via D-Bus path {_busName}:{RootPath}");
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "AT-SPI registry registration failed: {Error}", ex.Message);
                }
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

                // Skip if already registered
                if (_registeredPaths.Contains(RootPath))
                {
                    Console.WriteLine($"[AtspiRoot] Root path {RootPath} already registered, skipping");
                    return;
                }

                // Create the D-Bus method handler for the root object
                var pathHandler = new PathHandler(RootPath);
                
                // Add introspection support for the root object (with xmlns:xsi cleaning)
                var introspectionXml = GenerateRootIntrospectionXml();
                var introspectionHandler = new AtspiIntrospectionHandler(_connection, introspectionXml);
                Console.WriteLine($"[AtspiRoot] 🔧 Created introspection handler for root: {introspectionHandler.GetType().Name}");
                pathHandler.Add(introspectionHandler);
                Console.WriteLine("[AtspiRoot] ✅ Introspection handler added to PathHandler.");
                Console.WriteLine($"[AtspiRoot] 🔍 PathHandler now contains {pathHandler.Count} handlers");
                
                // CRITICAL: Add actual AT-SPI method handlers for accessibility queries
                var accessibleHandler = new AtspiAccessibleMethodHandler(this, _connection);
                pathHandler.Add(accessibleHandler);
                Console.WriteLine("[AtspiRoot] ✅ Accessible method handler added to PathHandler.");
                Console.WriteLine($"[AtspiRoot] 🔍 PathHandler now contains {pathHandler.Count} handlers");
                
                // Add Application interface handler if needed (root object is also IApplication)
                if (this is IApplication)
                {
                    var applicationHandler = new AtspiApplicationMethodHandler(this as IApplication, _connection);
                    pathHandler.Add(applicationHandler);
                    Console.WriteLine("[AtspiRoot] ✅ Application method handler added to PathHandler.");
                }
                
                Console.WriteLine($"[AtspiRoot] 🔧 Adding PathHandler to D-Bus connection.");
                Console.WriteLine($"[AtspiRoot] 🔧 PathHandler path: {pathHandler.Path}");
                Console.WriteLine($"[AtspiRoot] 🔧 PathHandler handler count: {pathHandler.Count}");
                _connection.AddMethodHandler(pathHandler);
                Console.WriteLine($"[AtspiRoot] 🔧 PathHandler added to connection - now monitoring for D-Bus calls...");
                _registeredPaths.Add(RootPath); // Track registration
                Console.WriteLine($"[AtspiRoot] ✅ Successfully registered AT-SPI root object with method handlers at: {RootPath}");
                
                // Now register any pending child contexts that were created before root was ready
                Console.WriteLine($"[AtspiRoot] 🔧 Registering pending child contexts...");
                foreach (var context in _contexts.Values)
                {
                    if (!_registeredPaths.Contains(context.ObjectPath.ToString()))
                    {
                        RegisterChildContextWithDBus(context);
                        _registeredPaths.Add(context.ObjectPath.ToString());
                        Console.WriteLine($"🔧 AT-SPI: Registered pending path {context.ObjectPath} with D-Bus");
                    }
                }
                Console.WriteLine($"[AtspiRoot] ✅ Pending child context registration completed.");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRoot] ❌ Failed to register root D-Bus handler: {e.Message}");
                Console.WriteLine($"[AtspiRoot] Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Registers a child AtspiContext with D-Bus for introspection and accessibility
        /// </summary>
        private void RegisterChildContextWithDBus(AtspiContext context)
        {
            try
            {
                Console.WriteLine($"[AtspiRoot] 🔧 Registering child AT-SPI context at: {context.ObjectPath}");
                
                if (_connection == null)
                {
                    Console.WriteLine($"[AtspiRoot] ❌ No D-Bus connection available for child registration");
                    return;
                }

                // Create PathHandler for this specific child context
                var pathHandler = new PathHandler(context.ObjectPath.ToString());
                
                // Add introspection handler with xmlns:xsi cleaning (overrides default introspection)
                var introspectionXml = GenerateChildIntrospectionXml(context);
                var introspectionHandler = new AtspiIntrospectionHandler(_connection, introspectionXml);
                Console.WriteLine($"[AtspiRoot] 🔧 Created introspection handler for child: {introspectionHandler.GetType().Name}");
                pathHandler.Add(introspectionHandler);
                Console.WriteLine("[AtspiRoot] ✅ Introspection handler added to PathHandler for child context.");
                Console.WriteLine($"[AtspiRoot] 🔍 Child PathHandler now contains {pathHandler.Count} handlers");
                
                // CRITICAL: Add actual AT-SPI method handlers for accessibility queries
                var accessibleHandler = new AtspiAccessibleMethodHandler(context, _connection);
                pathHandler.Add(accessibleHandler);
                Console.WriteLine("[AtspiRoot] ✅ Accessible method handler added to PathHandler for child context.");
                Console.WriteLine($"[AtspiRoot] 🔍 Child PathHandler now contains {pathHandler.Count} handlers");
                
                // Add Component interface handler if the context supports it
                if (context is IComponent component)
                {
                    var componentHandler = new AtspiComponentMethodHandler(component, _connection);
                    pathHandler.Add(componentHandler);
                    Console.WriteLine("[AtspiRoot] ✅ Component method handler added to PathHandler for child context.");
                }
                
                Console.WriteLine($"[AtspiRoot] 🔧 Adding PathHandler for child context to D-Bus connection.");
                Console.WriteLine($"[AtspiRoot] 🔧 Child PathHandler path: {pathHandler.Path}");
                Console.WriteLine($"[AtspiRoot] 🔧 Child PathHandler handler count: {pathHandler.Count}");
                _connection.AddMethodHandler(pathHandler);
                Console.WriteLine($"[AtspiRoot] 🔧 Child PathHandler added to connection - now monitoring for D-Bus calls...");
                Console.WriteLine($"[AtspiRoot] ✅ Successfully registered child AT-SPI context with method handlers at: {context.ObjectPath}");
                
                // Debug: Verify PathHandler registration
                Console.WriteLine("[AtspiRoot] 🔍 Verifying PathHandler registration for child context...");
                Console.WriteLine($"[AtspiRoot] PathHandler registered for: {context.ObjectPath}");
                Console.WriteLine($"[AtspiRoot] Introspection XML preview: {introspectionXml.Substring(0, Math.Min(100, introspectionXml.Length))}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRoot] ❌ Failed to register child D-Bus handler: {e.Message}");
                Console.WriteLine($"[AtspiRoot] Stack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Generates basic AT-SPI introspection XML for child contexts
        /// </summary>
        private string GenerateChildIntrospectionXml(AtspiContext context)
        {
            var componentInterface = "";
            
            // Add Component interface if this context supports it
            if (context is IComponent)
            {
                componentInterface = @"
  <interface name='org.a11y.atspi.Component'>
    <method name='Contains'>
      <arg direction='in' type='i' name='x'/>
      <arg direction='in' type='i' name='y'/>
      <arg direction='in' type='u' name='coord_type'/>
      <arg direction='out' type='b' name='result'/>
    </method>
    <method name='GetAccessibleAtPoint'>
      <arg direction='in' type='i' name='x'/>
      <arg direction='in' type='i' name='y'/>
      <arg direction='in' type='u' name='coord_type'/>
      <arg direction='out' type='(so)' name='accessible'/>
    </method>
    <method name='GetExtents'>
      <arg direction='in' type='u' name='coord_type'/>
      <arg direction='out' type='(iiii)' name='extents'/>
    </method>
    <method name='GetPosition'>
      <arg direction='in' type='u' name='coord_type'/>
      <arg direction='out' type='(ii)' name='position'/>
    </method>
    <method name='GetSize'>
      <arg direction='out' type='(ii)' name='size'/>
    </method>
    <method name='GetLayer'>
      <arg direction='out' type='u' name='layer'/>
    </method>
    <method name='GrabFocus'>
      <arg direction='out' type='b' name='result'/>
    </method>
  </interface>";
            }

            return $@"<node name='{context.ObjectPath}'>
  <interface name='org.a11y.atspi.Accessible'>
    <method name='GetRole'>
      <arg direction='out' type='u' name='role'/>
    </method>
    <method name='GetRoleName'>
      <arg direction='out' type='s' name='name'/>
    </method>
    <method name='GetLocalizedRoleName'>
      <arg direction='out' type='s' name='name'/>
    </method>
    <method name='GetChildCount'>
      <arg direction='out' type='i' name='count'/>
    </method>
    <method name='GetChildAtIndex'>
      <arg direction='in' type='i' name='index'/>
      <arg direction='out' type='(so)' name='child'/>
    </method>
    <method name='GetChildren'>
      <arg direction='out' type='a(so)' name='children'/>
    </method>
    <method name='GetIndexInParent'>
      <arg direction='out' type='i' name='index'/>
    </method>
    <method name='GetRelationSet'>
      <arg direction='out' type='a(ua(so))' name='relations'/>
    </method>
    <method name='GetState'>
      <arg direction='out' type='au' name='states'/>
    </method>
    <method name='GetApplication'>
      <arg direction='out' type='(so)' name='application'/>
    </method>
    <method name='GetAttributes'>
      <arg direction='out' type='a{{ss}}' name='attributes'/>
    </method>
    <method name='GetInterfaces'>
      <arg direction='out' type='as' name='interfaces'/>
    </method>
    <property name='Name' type='s' access='read'/>
    <property name='Description' type='s' access='read'/>
    <property name='Parent' type='(so)' access='read'/>
    <property name='ChildCount' type='i' access='read'/>
  </interface>{componentInterface}
  <interface name='org.freedesktop.DBus.Introspectable'>
    <method name='Introspect'>
      <arg direction='out' type='s' name='xml'/>
    </method>
  </interface>
  <interface name='org.freedesktop.DBus.Properties'>
    <method name='Get'>
      <arg direction='in' type='s' name='interface_name'/>
      <arg direction='in' type='s' name='property_name'/>
      <arg direction='out' type='v' name='value'/>
    </method>
    <method name='GetAll'>
      <arg direction='in' type='s' name='interface_name'/>
      <arg direction='out' type='a{{sv}}' name='properties'/>
    </method>
  </interface>
</node>";
        }

        private string GenerateRootIntrospectionXml()
        {
            Console.WriteLine($"[AtspiRoot] 🔧 Generating root introspection XML for {RootPath}");

            // Ensure no unexpected attributes like xmlns:xsi are included
            var xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<node>
  <interface name=""org.a11y.atspi.Accessible"">
    <method name=""GetChildAtIndex"">
      <arg type=""i"" direction=""in"" />
      <arg type=""(so)"" direction=""out"" />
    </method>
    <method name=""GetChildren"">
      <arg type=""a(so)"" direction=""out"" />
    </method>
    <method name=""GetIndexInParent"">
      <arg type=""i"" direction=""out"" />
    </method>
    <method name=""GetRelationSet"">
      <arg type=""a(ua(so))"" direction=""out"" />
    </method>
    <method name=""GetRole"">
      <arg type=""u"" direction=""out"" />
    </method>
    <method name=""GetRoleName"">
      <arg type=""s"" direction=""out"" />
    </method>
    <method name=""GetLocalizedRoleName"">
      <arg type=""s"" direction=""out"" />
    </method>
    <method name=""GetState"">
      <arg type=""au"" direction=""out"" />
    </method>
    <method name=""GetAttributes"">
      <arg type=""a{ss}"" direction=""out"" />
    </method>
    <method name=""GetApplication"">
      <arg type=""(so)"" direction=""out"" />
    </method>
    <method name=""GetInterfaces"">
      <arg type=""as"" direction=""out"" />
    </method>
    <property name=""Name"" type=""s"" access=""read"" />
    <property name=""Description"" type=""s"" access=""read"" />
    <property name=""Parent"" type=""(so)"" access=""read"" />
    <property name=""ChildCount"" type=""i"" access=""read"" />
  </interface>
  <interface name=""org.a11y.atspi.Application"">
    <property name=""ToolkitName"" type=""s"" access=""read"" />
    <property name=""Version"" type=""s"" access=""read"" />
    <property name=""Id"" type=""i"" access=""read"" />
    <method name=""GetApplicationBusAddress"">
      <arg type=""s"" direction=""out"" />
    </method>
    <method name=""GetLocale"">
      <arg type=""u"" direction=""in"" />
      <arg type=""s"" direction=""out"" />
    </method>
    <method name=""RegisterEventListener"">
      <arg type=""s"" direction=""in"" />
    </method>
    <method name=""DeregisterEventListener"">
      <arg type=""s"" direction=""in"" />
    </method>
    <method name=""GetApplicationInterfaces"">
      <arg type=""as"" direction=""out"" />
    </method>
  </interface>
  <interface name=""org.freedesktop.DBus.Introspectable"">
    <method name=""Introspect"">
      <arg type=""s"" direction=""out"" />
    </method>
  </interface>
  <interface name=""org.freedesktop.DBus.Properties"">
    <method name=""Get"">
      <arg direction=""in"" type=""s"" name=""interface_name""/>
      <arg direction=""in"" type=""s"" name=""property_name""/>
      <arg direction=""out"" type=""v"" name=""value""/>
    </method>
    <method name=""GetAll"">
      <arg direction=""in"" type=""s"" name=""interface_name""/>
      <arg direction=""out"" type=""a{sv}"" name=""properties""/>
    </method>
  </interface>
</node>";

    Console.WriteLine($"[AtspiRoot] ✅ Generated root introspection XML ({xml.Length} chars)");
    return xml;
}

        private void EnsureInitializationComplete()
        {
            const int timeoutMilliseconds = 5000; // 5 seconds timeout
            var startTime = DateTime.Now;

            if (!_isInitialized)
            {
                Console.WriteLine("[EnsureInitializationComplete] Waiting for initialization to complete...");
                while (!_isInitialized)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
                    {
                        Console.WriteLine("[EnsureInitializationComplete] Timeout reached while waiting for initialization.");
                        throw new TimeoutException("Initialization did not complete within the expected time.");
                    }
                    Thread.Sleep(10); // Small delay to avoid busy-waiting
                }
                Console.WriteLine("[EnsureInitializationComplete] Initialization complete.");
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
            Console.WriteLine($"[IAccessible.GetChildrenAsync] Thread: {Thread.CurrentThread.ManagedThreadId}, _children.Count: {_children.Count}");
            try
            {
                Console.WriteLine($"[AtspiRoot] GetChildrenAsync called - children count: {_children.Count}");
                
                List<ObjectReference> result;
                lock (_children)
                {
                    if (_children.Count == 0)
                    {
                        Console.WriteLine("[AtspiRoot] No children to return");
                        return Array.Empty<ObjectReference>();
                    }

                    result = new List<ObjectReference>(_children.Count);
                    foreach (var child in _children)
                    {
                        result.Add(((IAccessible)this).GetChildAtIndexAsync(_children.IndexOf(child)).Result);
                    }
                }

                foreach (var objRef in result)
                {
                    Console.WriteLine($"[AtspiRoot] Child: {objRef.Service}:{objRef.Path}");
                }

                return result.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] ERROR in GetChildrenAsync: {ex.Message}");
                Console.WriteLine($"[AtspiRoot] Stack trace: {ex.StackTrace}");
                // Return empty array instead of crashing
                return Array.Empty<ObjectReference>();
            }
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
            Console.WriteLine($"[AtspiRoot] GetAsync called for property: {prop}");
            return Task.FromResult<object?>(prop switch
            {
                nameof(AccessibleProperties.Name) => _accessibleProperties!.Name,
                nameof(AccessibleProperties.Description) => _accessibleProperties!.Description,
                nameof(AccessibleProperties.Parent) => _accessibleProperties!.Parent,
                nameof(AccessibleProperties.ChildCount) => _children.Count, // Use actual current count
                nameof(AccessibleProperties.Locale) => _accessibleProperties!.Locale,
                nameof(AccessibleProperties.AccessibleId) => _accessibleProperties!.AccessibleId,
                _ => null,
            });
        }

        Task<AccessibleProperties> IAccessible.GetAllAsync() 
        {
            // Return a properties object with current child count
            var props = new AccessibleProperties
            {
                Name = _accessibleProperties!.Name,
                Description = _accessibleProperties!.Description,
                Parent = _accessibleProperties!.Parent,
                ChildCount = _children.Count, // Use actual current count
                Locale = _accessibleProperties!.Locale,
                AccessibleId = _accessibleProperties!.AccessibleId
            };
            return Task.FromResult(props);
        }

        Task IAccessible.SetAsync(string prop, object val)
        {
            throw new NotImplementedException();
        }

        private void AddChild(Child child)
        {
            lock (_children)
            {
                _children.Add(child);
                Console.WriteLine($"[AtspiRoot] Child added. Total children: {_children.Count}");
            }
        }

        // Add an initialization flag to ensure ChildCount is accessed only after initialization
        private bool _isInitialized;

        public void CompleteInitialization()
        {
            lock (_children)
            {
                if (_isInitialized)
                {
                    Console.WriteLine("[CompleteInitialization] Initialization already completed.");
                    return;
                }

                _isInitialized = true;
                Console.WriteLine("[CompleteInitialization] Initialization complete.");
            }
        }

        // Example usage in ChildCount getter
        private int ChildCount
        {
            get
            {
                EnsureInitializationComplete();
                return _children.Count;
            }
        }

        public IReadOnlyList<Child> GetAccessibleChildren()
        {
            lock (_children)
            {
                Console.WriteLine($"[GetAccessibleChildren] Thread: {Thread.CurrentThread.ManagedThreadId}, _children.Count: {_children.Count}");
                return _children.AsReadOnly();
            }
        }

        private void RegisterRootInstance(Func<AutomationPeer> peerGetter)
        {
            try
            {
                Console.WriteLine("[RegisterRootInstance] Starting registration of AT-SPI root.");

                // Ensure D-Bus connection is available
                if (_connection == null)
                {
                    Console.WriteLine("[RegisterRootInstance] ❌ No D-Bus connection available. Aborting registration.");
                    return;
                }

                // Initialize the root object
                _accessibleProperties = new AccessibleProperties
                {
                    Name = "Root",
                    Description = "AT-SPI Root Object",
                    Locale = CultureInfo.CurrentCulture.Name,
                    ChildCount = _children.Count,
                    AccessibleId = "root"
                };

                Console.WriteLine("[RegisterRootInstance] Accessible properties initialized.");

                // Register the root with D-Bus
                RegisterRootWithDBus();
                Console.WriteLine("[RegisterRootInstance] Root registered with D-Bus.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RegisterRootInstance] ❌ Exception during registration: {ex.Message}");
                Console.WriteLine($"[RegisterRootInstance] Stack trace: {ex.StackTrace}");
            }
        }

        private bool IsDbusConnectionAvailable()
        {
            if (_connection == null)
            {
                Console.WriteLine("[IsDbusConnectionAvailable] ❌ No D-Bus connection available.");
                return false;
            }

            Console.WriteLine("[IsDbusConnectionAvailable] ✅ D-Bus connection is available.");
            return true;
        }

        /// <summary>
        /// Implements the GetAddress method for the org.a11y.Bus interface.
        /// </summary>
        /// <returns>The address of the AT-SPI bus.</returns>
        public Task<string> GetAddressAsync()
        {
            Console.WriteLine("[AtspiRoot] GetAddress method called.");

            // Return the bus address if available, otherwise return an empty string.
            return Task.FromResult(_busName ?? string.Empty);
        }

        /// <summary>
        /// Implements the IsEnabled property for the org.a11y.Status interface.
        /// </summary>
        public bool IsEnabled => true; // Replace with actual logic if needed

        /// <summary>
        /// Implements the ScreenReaderEnabled property for the org.a11y.Status interface.
        /// </summary>
        public bool ScreenReaderEnabled => false; // Replace with actual logic if needed

        /// <summary>
        /// Implements the GetMachineId method for the org.freedesktop.DBus.Peer interface.
        /// </summary>
        /// <returns>The unique machine ID.</returns>
        public Task<string> GetMachineIdAsync()
        {
            Console.WriteLine("[AtspiRoot] GetMachineId method called.");

            // Return a placeholder machine ID. Replace with actual logic if needed.
            return Task.FromResult(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Implements the Ping method for the org.freedesktop.DBus.Peer interface.
        /// </summary>
        public Task PingAsync()
        {
            Console.WriteLine("[AtspiRoot] Ping method called.");

            // Simply return a completed task to indicate success.
            return Task.CompletedTask;
        }
    }

    public class Child
    {
        private readonly Func<AutomationPeer> _peerGetter;
        public Child(Func<AutomationPeer> peerGetter) => _peerGetter = peerGetter;

        public AutomationPeer? Peer { get; private set; }

        public void CreatePeer()
        {
            Peer = _peerGetter();
        }
    }
}