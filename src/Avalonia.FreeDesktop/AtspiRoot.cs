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
        private static Connection? _connection; // AT-SPI bus connection

        private List<Child> _children = new List<Child>();
        private readonly Dictionary<AutomationPeer, AtspiContext> _contexts = new();
        private readonly HashSet<string> _registeredPaths = new(); // Track D-Bus registered paths
        private AtspiCache? _cache;
        internal AccessibleProperties? _accessibleProperties;
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

        // Properties exposed via org.freedesktop.DBus.Properties interface
        public string Name 
        {
            get
            {
                try
                {
                    var name = _accessibleProperties?.Name ?? Application.Current?.Name ?? "Unnamed";
                    Console.WriteLine($"[AtspiRoot] Property 'Name' accessed: '{name}'");
                    return name;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Name: {e}");
                    return "Unnamed";
                }
            }
        }
        
        public string Description 
        {
            get
            {
                try
                {
                    var desc = _accessibleProperties?.Description ?? "";
                    Console.WriteLine($"[AtspiRoot] Property 'Description' accessed: '{desc}'");
                    return desc;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Description: {e}");
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
                    Console.WriteLine($"[AtspiRoot] Property 'Parent' accessed");
                    var parent = new ObjectReference("org.a11y.atspi.Registry", new ObjectPath("/org/a11y/atspi/accessible/root"));
                    Console.WriteLine($"[AtspiRoot] Parent value: {parent.Service}:{parent.Path}");
                    return parent;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Parent: {e}");
                    Console.WriteLine($"[AtspiRoot] Stack trace: {e.StackTrace}");
                    return new ObjectReference("org.a11y.atspi.Registry", new ObjectPath("/org/a11y/atspi/accessible/root"));
                }
            }
        }
        
        public int ChildCount 
        {
            get
            {
                try
                {
                    var count = _children?.Count ?? 0;
                    Console.WriteLine($"[AtspiRoot] Property 'ChildCount' accessed: {count}");
                    return count;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting ChildCount: {e}");
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
                    var locale = _accessibleProperties?.Locale ?? CultureInfo.CurrentCulture.Name;
                    Console.WriteLine($"[AtspiRoot] Property 'Locale' accessed: '{locale}'");
                    return locale;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Locale: {e}");
                    return "en-US";
                }
            }
        }
        
        // Application properties exposed via org.freedesktop.DBus.Properties interface
        public string ToolkitName 
        {
            get
            {
                try
                {
                    var name = _applicationProperties?.ToolkitName ?? "Avalonia";
                    Console.WriteLine($"[AtspiRoot] Property 'ToolkitName' accessed: '{name}'");
                    return name;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting ToolkitName: {e}");
                    return "Avalonia";
                }
            }
        }
        
        public string Version 
        {
            get
            {
                try
                {
                    var version = _applicationProperties?.Version ?? FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location).FileVersion ?? "0.0.0.0";
                    Console.WriteLine($"[AtspiRoot] Property 'Version' accessed: '{version}'");
                    return version;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Version: {e}");
                    return "0.0.0.0";
                }
            }
        }
        
        public int Id 
        {
            get
            {
                try
                {
                    var id = _applicationProperties?.Id ?? 0;
                    Console.WriteLine($"[AtspiRoot] Property 'Id' accessed: {id}");
                    return id;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[AtspiRoot] ERROR getting Id: {e}");
                    return 0;
                }
            }
        }

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

                            // Note: Well-known D-Bus names are optional
                            // The Registry uses unique bus names (e.g., :1.123) for registration
                            // Commenting out well-known name registration as it's not required

                            // Set up the bus name and register D-Bus handlers
                            _instance._busName = _connection.UniqueName;
                            _instance.Register();
                            Console.WriteLine("[AtspiRoot] ✅ Root object registered on AT-SPI bus.");
                            
                            // Embed application in AT-SPI desktop for pyatspi discovery
                            await _instance.RegisterWithAtspiAsync();
                            
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

                        // If this is the first child and it's a window peer, create and register its AT-SPI context
                        if (_instance._children.Count == 1 && child.Peer != null)
                        {
                            Console.WriteLine($"[AtspiRoot] Checking if first child is window peer for context creation. Peer type: {child.Peer.GetType().Name}");
                            var context = _instance.CreateAutomationContext(child.Peer);
                            if (context != null)
                            {
                                Console.WriteLine($"[AtspiRoot] Created AT-SPI context for window peer: {child.Peer.GetType().Name}, ObjectPath={context.ObjectPath}");
                                _instance.RegisterChildContextWithDBus(context);
                                Console.WriteLine($"[AtspiRoot] Registered AT-SPI context for window peer with D-Bus: {context.ObjectPath}");
                            }
                            else
                            {
                                Console.WriteLine($"[AtspiRoot] Failed to create AT-SPI context for window peer: {child.Peer.GetType().Name}");
                            }
                        }
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
            Console.WriteLine($"[AtspiRoot] CreateAutomationContext called for peer: {peer?.GetType().Name ?? "null"}, peer={peer}");
            if (_contexts.TryGetValue(peer, out var existingContext))
            {
                Console.WriteLine($"[AtspiRoot] Context already exists for peer: {peer.GetType().Name}, ObjectPath={existingContext.ObjectPath}");
                return existingContext;
            }
            var result = AtspiContextFactory.Create(this, peer);
            _contexts[peer] = result;
            Console.WriteLine($"[AtspiRoot] Context created and added for peer: {peer.GetType().Name}, ObjectPath={result.ObjectPath}");
            _cache?.Add(result);
            System.Diagnostics.Debug.WriteLine($"Created {result.ObjectPath} for {peer}");
            Console.WriteLine($"[AtspiRoot] Created context {result.ObjectPath} for {peer.GetType().Name} '{peer.GetName() ?? "unnamed"}'");
            
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
                
                // CRITICAL: Applications MUST call Socket.Embed to register with Registry
                // This adds the app to Registry's children list, making it discoverable by pyatspi
                await RegisterWithAtspiRegistry(appRef);
                
                Console.WriteLine($"[AtspiRoot] ✅ AT-SPI application registered and ready");
                Console.WriteLine($"[AtspiRoot] 🌐 AT-SPI root path: {RootPath}");
                Console.WriteLine($"[AtspiRoot] 📊 Service name: {_busName}");
                
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "AT-SPI application ready and discoverable");
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
        /// Modern AT-SPI uses Socket/Plug pattern where apps "embed" into the Registry desktop object.
        /// </summary>
        private async Task RegisterWithAtspiRegistry(ObjectReference appRef)
        {
            try
            {
                Console.WriteLine($"[AtspiRoot] 🔗 Embedding into AT-SPI Registry desktop...");
                Console.WriteLine($"[AtspiRoot] App reference: {appRef.Service}:{appRef.Path}");
                
                // Call Socket.Embed on the Registry
                // Workaround for Tmds.DBus.Protocol ref struct limitation: create message in non-async helper
                var message = CreateEmbedMessage(appRef);
                var socketRef = await _connection!.CallMethodAsync<ObjectReference>(message, 
                    (Message m, object? state) =>
                    {
                        var r = m.GetBodyReader();
                        return new ObjectReference(r.ReadString(), r.ReadObjectPath().ToString());
                    });
                
                var socketBusName = socketRef.Service;
                var socketPath = socketRef.Path;
                
                Console.WriteLine($"[AtspiRoot] ✅ Successfully embedded into AT-SPI desktop!");
                Console.WriteLine($"[AtspiRoot] Socket reference: {socketBusName}:{socketPath}");
                Logger.TryGet(LogEventLevel.Information, LogArea.Control)?.Log(this, "Successfully embedded into AT-SPI desktop");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRoot] ⚠️ Registry embedding failed: {ex.Message}");
                Console.WriteLine($"[AtspiRoot] Stack: {ex.StackTrace}");
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "AT-SPI registry embedding failed: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Creates a D-Bus message for calling Socket.Embed on the Registry.
        /// Non-async to work around Tmds.DBus.Protocol's MessageWriter ref struct limitation.
        /// Socket interface is exposed on the Registry's root accessible object.
        /// </summary>
        private MessageBuffer CreateEmbedMessage(ObjectReference appRef)
        {
            using var writer = _connection!.GetMessageWriter();
            
            writer.WriteMethodCallHeader(
                destination: "org.a11y.atspi.Registry",
                path: "/org/a11y/atspi/accessible/root",  // Socket is on root, not /registry
                @interface: "org.a11y.atspi.Socket",
                member: "Embed",
                signature: "(so)");
            
            // Write the ObjectReference as a struct (so)
            writer.WriteStructureStart();
            writer.WriteString(appRef.Service);
            writer.WriteObjectPath(new ObjectPath(appRef.Path));
            
            return writer.CreateMessage();
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
        /// Registers the AtspiRoot on the SESSION bus for pyatspi discovery
        /// </summary>
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

                // Add Introspectable handler
                pathHandler.Add(new AtspiIntrospectionHandler(_connection, null));
                Console.WriteLine("[AtspiRoot] ✅ Introspectable handler added to PathHandler.");

                // Add Accessible method handler
                var accessibleHandler = new AtspiAccessibleMethodHandler(this, _connection);
                pathHandler.Add(accessibleHandler);
                Console.WriteLine("[AtspiRoot] ✅ Accessible method handler added to PathHandler.");
                Console.WriteLine($"[AtspiRoot] 🔍 PathHandler now contains {pathHandler.Count} handlers");

                if (this is IApplication)
                {
                    var applicationHandler = new AtspiApplicationMethodHandler(this as IApplication, _connection);
                    pathHandler.Add(applicationHandler);
                    Console.WriteLine("[AtspiRoot] ✅ Application method handler added to PathHandler.");
                }

                // CRITICAL: Register the Cache interface at /org/a11y/atspi/cache
                // This is how pyatspi and other AT tools discover the application!
                if (_cache != null)
                {
                    _connection.AddMethodHandler(new AtspiCacheMethodHandler(_cache, _connection));
                    Console.WriteLine($"[AtspiRoot] ✅ Cache handler registered at {_cache.ObjectPath} - enables app discovery!");
                }

                // Register Properties handler directly with connection BEFORE PathHandler
                // (cannot use PathHandler.Add as it requires IDBusInterfaceHandler)
                _connection.AddMethodHandler(new AtspiRootPropertiesMethodHandler(this, _connection));
                Console.WriteLine("[AtspiRoot] ✅ Properties handler registered directly with connection.");

                try
                {
                    Console.WriteLine($"[AtspiRoot] 🔧 Adding PathHandler to D-Bus connection.");
                    Console.WriteLine($"[AtspiRoot] 🔧 PathHandler path: {pathHandler.Path}");
                    Console.WriteLine($"[AtspiRoot] 🔧 PathHandler handler count: {pathHandler.Count}");
                    _connection.AddMethodHandler(pathHandler);
                    Console.WriteLine($"[AtspiRoot] 🔧 PathHandler added to connection - now monitoring for D-Bus calls...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AtspiRoot] ⚠️ PathHandler already registered (expected if Properties handler registered first): {ex.Message}");
                }
                
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
                
                // Add Introspectable handler
                pathHandler.Add(new AtspiIntrospectionHandler(_connection, null));
                
                // Add Accessible method handler
                var accessibleHandler = new AtspiAccessibleMethodHandler(context, _connection);
                pathHandler.Add(accessibleHandler);
                
                // Register Properties handler directly with connection for this path
                // (cannot use PathHandler.Add as it requires IDBusInterfaceHandler)
                _connection.AddMethodHandler(new AtspiPropertiesMethodHandler(context, _connection));
                
                Console.WriteLine($"[AtspiRoot] ✅ Added Accessible, Properties, and Introspectable handlers for child: {context.ObjectPath}");
                _connection.AddMethodHandler(pathHandler);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRoot] ❌ Failed to register child D-Bus handler: {e.Message}");
                Console.WriteLine($"[AtspiRoot] Stack trace: {e.StackTrace}");
            }
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

        // Synchronous method for D-Bus handlers - returns cached child reference
        public ObjectReference? GetCachedChildAtIndex(int index)
        {
            lock (_children)
            {
                Console.WriteLine($"[AtspiRoot] GetCachedChildAtIndex({index}) called. Total children: {_children.Count}");
                for (int i = 0; i < _children.Count; i++)
                {
                    var child = _children[i];
                    var peerType = child.Peer?.GetType().Name ?? "null";
                    Console.WriteLine($"[AtspiRoot] Child[{i}]: PeerType={peerType}, Peer={(child.Peer != null ? "set" : "null")}");
                }
                if (index < 0 || index >= _children.Count)
                {
                    Console.WriteLine($"[AtspiRoot] GetCachedChildAtIndex({index}) - out of range (count: {_children.Count})");
                    return null;
                }

                var childAtIndex = _children[index];
                var peer = childAtIndex.Peer;
                if (peer == null)
                {
                    Console.WriteLine($"[AtspiRoot] GetCachedChildAtIndex({index}) - peer is null, not initialized");
                    return null;
                }
                if (!_contexts.TryGetValue(peer, out var context))
                {
                    Console.WriteLine($"[AtspiRoot] GetCachedChildAtIndex({index}) - context not found for peer, not initialized");
                    return null;
                }
                Console.WriteLine($"[AtspiRoot] GetCachedChildAtIndex({index}) - returning ObjectReference: Service={_busName ?? ":1.0"}, Path={context.ObjectPath}");
                return new ObjectReference(_busName ?? ":1.0", context.ObjectPath);
            }
        }

        public int GetCachedChildCount()
        {
            lock (_children)
            {
                return _children.Count;
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

        private void AddChild(Child child)
        {
            Console.WriteLine($"[AtspiRoot] AddChild called for child with peer: {child.Peer?.GetType().Name ?? "null"}");
            lock (_children)
            {
                _children.Add(child);
                Console.WriteLine($"[AtspiRoot] Child added. Total children: {_children.Count}");
                // Ensure peer is created before context
                if (child.Peer == null)
                {
                    child.CreatePeer();
                    Console.WriteLine($"[AddChild] Created peer for child");
                }
                // Ensure automation context is created for the peer
                if (child.Peer != null && !_contexts.ContainsKey(child.Peer))
                {
                    var context = CreateAutomationContext(child.Peer);
                    Console.WriteLine($"[AddChild] Created automation context for child: {context.ObjectPath}");
                }
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

                // Ensure all child peers and contexts are created on the UI thread
                Console.WriteLine($"[CompleteInitialization] Creating peers and contexts for {_children.Count} children...");
                foreach (var child in _children)
                {
                    if (child.Peer == null)
                    {
                        child.CreatePeer();
                        Console.WriteLine($"[CompleteInitialization] Created peer for child");
                    }
                    
                    // Pre-create the automation context while on UI thread
                    var peer = child.Peer;
                    if (peer != null && !_contexts.ContainsKey(peer))
                    {
                        var context = CreateAutomationContext(peer);
                        Console.WriteLine($"[CompleteInitialization] Pre-created context: {context.ObjectPath}");
                    }
                }

                _isInitialized = true;
                Console.WriteLine($"[CompleteInitialization] Initialization complete. {_contexts.Count} contexts created.");
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
                // Ensure all child peers and contexts are created before D-Bus registration
                CompleteInitialization();
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