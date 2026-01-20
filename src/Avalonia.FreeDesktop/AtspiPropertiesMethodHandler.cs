using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus method handler for org.freedesktop.DBus.Properties interface.
    /// This allows D-Bus clients to access object properties via Get/GetAll methods.
    /// </summary>
    internal class AtspiPropertiesMethodHandler : IMethodHandler
    {
        private readonly AtspiContext _context;
        private readonly Connection _connection;

        public AtspiPropertiesMethodHandler(AtspiContext context, Connection connection)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiPropertiesMethodHandler] Created for {context.ObjectPath}");
        }

        public string Path => _context.ObjectPath.ToString();

        public bool RunMethodHandlerSynchronously(Message message) => true;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                var interfaceName = context.Request.InterfaceAsString;
                var member = context.Request.MemberAsString;

                Console.WriteLine($"[AtspiPropertiesMethodHandler] HandleMethodAsync called:");
                Console.WriteLine($"  Interface: {interfaceName}");
                Console.WriteLine($"  Member: {member}");
                Console.WriteLine($"  Path: {context.Request.PathAsString}");
                Console.WriteLine($"  Sender: {context.Request.SenderAsString}");

                if (interfaceName != "org.freedesktop.DBus.Properties")
                {
                    Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Wrong interface: {interfaceName}");
                    return default;
                }

                switch (member)
                {
                    case "Get":
                        HandleGet(context);
                        break;
                    case "GetAll":
                        HandleGetAll(context);
                        break;
                    case "Set":
                        HandleSet(context);
                        break;
                }

                Console.WriteLine($"[AtspiPropertiesMethodHandler] HandleMethodAsync completing normally");
                return default;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌❌❌ FATAL EXCEPTION in HandleMethodAsync: {ex.GetType().Name}");
                Console.WriteLine($"  Message: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}");
                try
                {
                    context.ReplyError("org.freedesktop.DBus.Error.Failed", $"Internal error: {ex.Message}");
                }
                catch (Exception replyEx)
                {
                    Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Failed to send error reply: {replyEx.Message}");
                }
                return default;
            }
        }

        private void HandleGet(MethodContext context)
        {
            try
            {
                var reader = context.Request.GetBodyReader();
                var propertyInterface = reader.ReadString();
                var propertyName = reader.ReadString();

                Console.WriteLine($"[AtspiPropertiesMethodHandler] Get('{propertyInterface}', '{propertyName}')");

                if (propertyInterface != "org.a11y.atspi.Accessible")
                {
                    Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Unsupported interface: {propertyInterface}");
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Interface '{propertyInterface}' not supported");
                    return;
                }

                var variant = GetPropertyValue(propertyName);
                if (variant == null)
                {
                    Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Property not found: {propertyName}");
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Property '{propertyName}' not found");
                    return;
                }

                // Write variant reply - signature already declared in CreateReplyWriter("v")
                var writer = context.CreateReplyWriter("v");
                Console.WriteLine($"[AtspiPropertiesMethodHandler] Writing variant value for '{propertyName}'...");
                WriteVariantValue(ref writer, variant.Value);
                Console.WriteLine($"[AtspiPropertiesMethodHandler] Creating message...");
                var message = writer.CreateMessage();
                Console.WriteLine($"[AtspiPropertiesMethodHandler] Replying...");
                context.Reply(message);
                // Don't dispose writer - let it be garbage collected
                // writer.Dispose();
                
                Console.WriteLine($"[AtspiPropertiesMethodHandler] ✅ Get('{propertyName}') returned successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Get failed: {e.Message}");
                Console.WriteLine($"  Stack: {e.StackTrace}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
            }
        }

        private void HandleGetAll(MethodContext context)
        {
            try
            {
                var reader = context.Request.GetBodyReader();
                var propertyInterface = reader.ReadString();

                Console.WriteLine($"[AtspiPropertiesMethodHandler] GetAll('{propertyInterface}')");

                if (propertyInterface != "org.a11y.atspi.Accessible")
                {
                    Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ Unsupported interface: {propertyInterface}");
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Interface '{propertyInterface}' not supported");
                    return;
                }

                var writer = context.CreateReplyWriter("a{sv}");
                var arrayStart = writer.WriteDictionaryStart();
                
                // Write all accessible properties
                WriteProperty(ref writer, "Name", new PropertyValue { Type = 's', StringValue = _context.Name });
                WriteProperty(ref writer, "Description", new PropertyValue { Type = 's', StringValue = _context.Description });
                WriteProperty(ref writer, "ChildCount", new PropertyValue { Type = 'i', IntValue = _context.ChildCount });
                WriteProperty(ref writer, "Locale", new PropertyValue { Type = 's', StringValue = _context.Locale });
                
                // Parent is (so) type - string + object path tuple
                var parent = _context.Parent;
                WriteProperty(ref writer, "Parent", new PropertyValue 
                { 
                    Type = 'r', // struct
                    StructValue = (parent.Service, parent.Path)
                });
                
                writer.WriteDictionaryEnd(arrayStart);
                var message = writer.CreateMessage();
                writer.Dispose();
                context.Reply(message);
                
                Console.WriteLine($"[AtspiPropertiesMethodHandler] ✅ GetAll returned 5 properties");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiPropertiesMethodHandler] ❌ GetAll failed: {e.Message}");
                Console.WriteLine($"  Stack: {e.StackTrace}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
            }
        }

        private void HandleSet(MethodContext context)
        {
            Console.WriteLine($"[AtspiPropertiesMethodHandler] Set() called - not supported");
            context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", "AT-SPI properties are read-only");
        }

        private PropertyValue? GetPropertyValue(string propertyName)
        {
            return propertyName switch
            {
                "Name" => new PropertyValue { Type = 's', StringValue = _context.Name },
                "Description" => new PropertyValue { Type = 's', StringValue = _context.Description },
                "ChildCount" => new PropertyValue { Type = 'i', IntValue = _context.ChildCount },
                "Locale" => new PropertyValue { Type = 's', StringValue = _context.Locale },
                "Parent" => new PropertyValue 
                { 
                    Type = 'r',
                    StructValue = (_context.Parent.Service, _context.Parent.Path)
                },
                _ => null
            };
        }

        private void WriteVariant(ref MessageWriter writer, PropertyValue value)
        {
            // Write variant with signature (for use in dictionaries)
            switch (value.Type)
            {
                case 's':
                    writer.WriteVariantString(value.StringValue ?? "");
                    break;
                case 'i':
                    writer.WriteVariantInt32(value.IntValue);
                    break;
                case 'r': // struct (so)
                    writer.WriteSignature("(so)");
                    writer.WriteStructureStart();
                    writer.WriteString(value.StructValue.Item1);
                    writer.WriteObjectPath(value.StructValue.Item2);
                    // Structure is implicitly closed - no WriteStructureEnd needed
                    break;
                default:
                    throw new NotSupportedException($"Type '{value.Type}' not supported");
            }
        }

        private void WriteVariantValue(ref MessageWriter writer, PropertyValue value)
        {
            // Write variant value for Properties.Get reply
            // The signature "v" in CreateReplyWriter already declares this is a variant
            // We must write the variant signature + value (what WriteVariantString does internally)
            // But actually, since the MESSAGE signature is already "v", we need to write
            // the variant CONTENTS directly as the body
            Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Called with Type='{value.Type}'");
            switch (value.Type)
            {
                case 's':
                    // For a variant containing a string, we write:
                    // 1. Signature of the variant contents ("s")
                    // 2. The string value itself
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Writing string variant: '{value.StringValue}'");
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Calling WriteSignature('s')...");
                    writer.WriteSignature("s");
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Calling WriteString('{value.StringValue}')...");
                    writer.WriteString(value.StringValue ?? "");
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Write complete");
                    break;
                case 'i':
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Writing int32 variant: {value.IntValue}");
                    writer.WriteSignature("i");
                    writer.WriteInt32(value.IntValue);
                    break;
                case 'r': // struct (so)
                    Console.WriteLine($"[AtspiPropertiesMethodHandler.WriteVariantValue] Writing struct variant");
                    writer.WriteSignature("(so)");
                    writer.WriteStructureStart();
                    writer.WriteString(value.StructValue.Item1);
                    writer.WriteObjectPath(value.StructValue.Item2);
                    break;
                default:
                    throw new NotSupportedException($"Type '{value.Type}' not supported");
            }
        }

        private void WriteProperty(ref MessageWriter writer, string name, PropertyValue value)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(name);
            WriteVariant(ref writer, value);
        }

        private struct PropertyValue
        {
            public char Type;
            public string? StringValue;
            public int IntValue;
            public (string, ObjectPath) StructValue;
        }
    }
}
