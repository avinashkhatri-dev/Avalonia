using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus method handler for org.a11y.atspi.Cache interface.
    /// This is THE critical interface that allows assistive technologies (pyatspi, Orca, etc.)
    /// to discover and enumerate accessible applications on the AT-SPI bus.
    /// 
    /// Without this handler, applications are invisible to screen readers even if connected to the bus.
    /// The Cache.GetItems() method returns all accessible objects in the application tree.
    /// </summary>
    internal class AtspiCacheMethodHandler : IMethodHandler
    {
        private readonly AtspiCache _cache;
        private readonly Connection _connection;

        public AtspiCacheMethodHandler(AtspiCache cache, Connection connection)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiCacheMethodHandler] 🚀 Created handler for {_cache.ObjectPath}");
            Console.WriteLine($"[AtspiCacheMethodHandler] This enables application discovery by AT tools!");
        }

        public string Path => _cache.ObjectPath.ToString();

        public bool RunMethodHandlerSynchronously(Message message) => true;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                var interfaceName = context.Request.InterfaceAsString;
                var member = context.Request.MemberAsString;

                Console.WriteLine($"[AtspiCacheMethodHandler] 📞 Method call:");
                Console.WriteLine($"  Interface: {interfaceName}");
                Console.WriteLine($"  Member: {member}");
                Console.WriteLine($"  Sender: {context.Request.SenderAsString}");

                if (interfaceName != "org.a11y.atspi.Cache")
                {
                    Console.WriteLine($"[AtspiCacheMethodHandler] ❌ Wrong interface: {interfaceName}");
                    return default;
                }

                if (member == "GetItems")
                {
                    HandleGetItems(context);
                }
                else
                {
                    Console.WriteLine($"[AtspiCacheMethodHandler] ❌ Unknown method: {member}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiCacheMethodHandler] ❌ Exception: {ex.Message}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }

            return default;
        }

        private void HandleGetItems(MethodContext context)
        {
            try
            {
                Console.WriteLine($"[AtspiCacheMethodHandler] 🔍 GetItems() called - returning cache items for discovery");
                
                var items = _cache.GetItemsAsync().Result;
                Console.WriteLine($"[AtspiCacheMethodHandler] Cache contains {items.Length} items");

                // Create reply writer
                // Signature: a((so)(so)(so)iiassusau) - note the DOUBLE parentheses!
                // Array of STRUCT, where each struct contains: (so)(so)(so)iiassusau
                // From AT-SPI Cache.xml: https://gitlab.gnome.org/GNOME/at-spi2-core/-/blob/master/xml/Cache.xml
                
                var writer = context.CreateReplyWriter("a((so)(so)(so)iiassusau)");
                
                // Write array of CacheItem structs
                var arrayStart = writer.WriteArrayStart(DBusType.Struct);

                foreach (var item in items)
                {
                    // Write main struct: ((so)(so)(so)iiassusau)
                    writer.WriteStructureStart();
                    
                    // Item ObjectReference - struct (so)
                    writer.WriteStructureStart();
                    writer.WriteString(item.Path.Service);
                    writer.WriteObjectPath(item.Path.Path);
                    
                    // Application ObjectReference - struct (so)
                    writer.WriteStructureStart();
                    writer.WriteString(item.Application.Service);
                    writer.WriteObjectPath(item.Application.Path);
                    
                    // Parent ObjectReference - struct (so)
                    writer.WriteStructureStart();
                    writer.WriteString(item.Parent.Service);
                    writer.WriteObjectPath(item.Parent.Path);
                    
                    // Index (i) - always -1 for root items
                    writer.WriteInt32(-1);
                    
                    // ChildCount (i)
                    writer.WriteInt32(item.Children.Length);
                    
                    // Interfaces (as) - array of strings
                    writer.WriteArray(item.SupportedInterfaces);
                    
                    // Name (s)
                    writer.WriteString(item.Name);
                    
                    // Role (u) - uint32
                    writer.WriteUInt32(item.Role);
                    
                    // Description (s)
                    writer.WriteString(item.Description);
                    
                    // States (au) - array of uint32
                    var statesStart = writer.WriteArrayStart(DBusType.UInt32);
                    foreach (var state in item.State)
                    {
                        writer.WriteUInt32((uint)state);
                    }
                    writer.WriteArrayEnd(statesStart);
                }

                writer.WriteArrayEnd(arrayStart);
                
                // Send the reply
                var message = writer.CreateMessage();
                writer.Dispose();
                context.Reply(message);
                
                Console.WriteLine($"[AtspiCacheMethodHandler] ✅ GetItems() reply sent with {items.Length} cache items");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiCacheMethodHandler] ❌ GetItems failed: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }
        }
    }
}
