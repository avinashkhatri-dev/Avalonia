using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus Properties handler specifically for the AtspiRoot object.
    /// Handles org.freedesktop.DBus.Properties interface for the application root.
    /// </summary>
    internal class AtspiRootPropertiesMethodHandler : IMethodHandler
    {
        private readonly AtspiRoot _root;
        private readonly Connection _connection;

        public AtspiRootPropertiesMethodHandler(AtspiRoot root, Connection connection)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Created for {_root.ObjectPath}");
        }

        public string Path => _root.ObjectPath.ToString();

        public bool RunMethodHandlerSynchronously(Message message) => true;

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            try
            {
                var interfaceName = context.Request.InterfaceAsString;
                var member = context.Request.MemberAsString;

                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] HandleMethodAsync called:");
                Console.WriteLine($"  Interface: {interfaceName}");
                Console.WriteLine($"  Member: {member}");
                Console.WriteLine($"  Path: {context.Request.PathAsString}");
                Console.WriteLine($"  Sender: {context.Request.SenderAsString}");

                // Handle Properties interface
                if (interfaceName == "org.freedesktop.DBus.Properties")
                {
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
                        default:
                            Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Unknown member: {member}");
                            context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"Method '{member}' not supported");
                            break;
                    }
                }
                // Handle Accessible interface (specifically GetApplication)
                else if (interfaceName == "org.a11y.atspi.Accessible")
                {
                    if (member == "GetApplication")
                    {
                        HandleGetApplication(context);
                    }
                    else
                    {
                        Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Accessible method '{member}' not implemented");
                        context.ReplyError("org.freedesktop.DBus.Error.UnknownMethod", $"Method '{member}' not implemented");
                    }
                }
                else
                {
                    Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Unsupported interface: {interfaceName}");
                    return default;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ HandleMethodAsync exception: {e.Message}");
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Stack: {e.StackTrace}");
                try
                {
                    context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
                }
                catch (Exception replyEx)
                {
                    Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Failed to send error reply: {replyEx.Message}");
                }
            }

            return default;
        }

        private void HandleGetApplication(MethodContext context)
        {
            try
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] HandleGetApplication called");
                
                // Return the application path (root points to itself as the application)
                var writer = context.CreateReplyWriter("(so)");
                writer.WriteStructureStart();
                writer.WriteString(_root.ApplicationPath.Service);
                writer.WriteObjectPath(_root.ApplicationPath.Path);
                context.Reply(writer.CreateMessage());
                
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ✅ GetApplication returned: {_root.ApplicationPath.Service}, {_root.ApplicationPath.Path}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ GetApplication failed: {e.Message}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
            }
        }

        private void HandleGet(MethodContext context)
        {
            try
            {
                var reader = context.Request.GetBodyReader();
                var propertyInterface = reader.ReadString();
                var propertyName = reader.ReadString();

                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Get('{propertyInterface}', '{propertyName}')");

                PropertyValue? variant = null;
                
                if (propertyInterface == "org.a11y.atspi.Accessible")
                {
                    variant = GetPropertyValue(propertyName);
                }
                else if (propertyInterface == "org.a11y.atspi.Application")
                {
                    variant = GetApplicationPropertyValue(propertyName);
                }
                else
                {
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Interface '{propertyInterface}' not supported");
                    return;
                }

                if (variant == null || !variant.HasValue)
                {
                    Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Property not found: {propertyName}");
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Property '{propertyName}' not found");
                    return;
                }

                var writer = context.CreateReplyWriter("v");
                WriteVariantValue(ref writer, variant.Value);
                var message = writer.CreateMessage();
                context.Reply(message);
                
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ✅ Get('{propertyName}') succeeded");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Get failed: {e.Message}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
            }
        }

        private void HandleGetAll(MethodContext context)
        {
            try
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] HandleGetAll ENTRY");
                var reader = context.Request.GetBodyReader();
                var propertyInterface = reader.ReadString();

                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] GetAll('{propertyInterface}')");

                if (propertyInterface == "org.a11y.atspi.Accessible")
                {
                    HandleGetAllAccessible(context);
                }
                else if (propertyInterface == "org.a11y.atspi.Application")
                {
                    HandleGetAllApplication(context);
                }
                else
                {
                    context.ReplyError("org.freedesktop.DBus.Error.InvalidArgs", $"Interface '{propertyInterface}' not supported");
                    return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ GetAll failed: {e.Message}");
                context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
            }
        }

        private void HandleGetAllAccessible(MethodContext context)
        {
            try
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Creating reply writer...");
                var writer = context.CreateReplyWriter("a{sv}");
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Starting dictionary...");
                var arrayStart = writer.WriteDictionaryStart();
                
                // Write all accessible properties for the root
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing Name property...");
                WriteProperty(ref writer, "Name", new PropertyValue { Type = 's', StringValue = _root.Name });
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing Description property...");
                WriteProperty(ref writer, "Description", new PropertyValue { Type = 's', StringValue = _root.Description });
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing ChildCount property...");
                WriteProperty(ref writer, "ChildCount", new PropertyValue { Type = 'i', IntValue = _root.ChildCount });
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing Locale property...");
                WriteProperty(ref writer, "Locale", new PropertyValue { Type = 's', StringValue = _root.Locale });
                
                // Parent - AT-SPI registry
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Getting Parent...");
                var parent = _root.Parent;
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing Parent property...");
                WriteProperty(ref writer, "Parent", new PropertyValue 
                { 
                    Type = 'r',
                    StructValue = (parent.Service, parent.Path)
                });
                
                // AccessibleId - unique identifier for the accessible object
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing AccessibleId property...");
                WriteProperty(ref writer, "AccessibleId", new PropertyValue { Type = 's', StringValue = "" });
                
                // HelpText - help text for the accessible object
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Writing HelpText property...");
                WriteProperty(ref writer, "HelpText", new PropertyValue { Type = 's', StringValue = "" });
                
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Ending dictionary...");
                writer.WriteDictionaryEnd(arrayStart);
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Creating message...");
                var message = writer.CreateMessage();
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Disposing writer...");
                writer.Dispose();
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Sending reply...");
                context.Reply(message);
                
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ✅ GetAll returned 7 properties for root");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌❌❌ GetAll EXCEPTION: {e.GetType().Name}");
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Message: {e.Message}");
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Stack: {e.StackTrace}");
                try
                {
                    context.ReplyError("org.freedesktop.DBus.Error.Failed", e.Message);
                }
                catch (Exception replyEx)
                {
                    Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ Failed to send error reply: {replyEx.Message}");
                }
            }
        }

        private void HandleGetAllApplication(MethodContext context)
        {
            var writer = context.CreateReplyWriter("a{sv}");
            var arrayStart = writer.WriteDictionaryStart();

            WriteProperty(ref writer, "ToolkitName", new PropertyValue { Type = 's', StringValue = "Avalonia" });
            WriteProperty(ref writer, "Version", new PropertyValue { Type = 's', StringValue = "12.0.999" });
            WriteProperty(ref writer, "AtspiVersion", new PropertyValue { Type = 's', StringValue = "2.1" });
            WriteProperty(ref writer, "Id", new PropertyValue { Type = 'i', IntValue = 0 });

            writer.WriteDictionaryEnd(arrayStart);
            context.Reply(writer.CreateMessage());
            
            Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ✅ GetAll returned 4 Application properties");
        }

        private PropertyValue? GetApplicationPropertyValue(string propertyName)
        {
            try
            {
                return propertyName switch
                {
                    "ToolkitName" => new PropertyValue { Type = 's', StringValue = "Avalonia" },
                    "Version" => new PropertyValue { Type = 's', StringValue = "12.0.999" },
                    "AtspiVersion" => new PropertyValue { Type = 's', StringValue = "2.1" },
                    "Id" => new PropertyValue { Type = 'i', IntValue = 0 },
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ GetApplicationPropertyValue('{propertyName}') exception: {ex.Message}");
                throw;
            }
        }

        private void HandleSet(MethodContext context)
        {
            Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Set() called - not supported");
            context.ReplyError("org.freedesktop.DBus.Error.PropertyReadOnly", "AT-SPI properties are read-only");
        }

        private PropertyValue? GetPropertyValue(string propertyName)
        {
            try
            {
                return propertyName switch
                {
                    "Name" => new PropertyValue { Type = 's', StringValue = _root.Name },
                    "Description" => new PropertyValue { Type = 's', StringValue = _root.Description },
                    "ChildCount" => new PropertyValue { Type = 'i', IntValue = _root.ChildCount },
                    "Locale" => new PropertyValue { Type = 's', StringValue = _root.Locale },
                    "Parent" => new PropertyValue 
                    { 
                        Type = 'r',
                        StructValue = (_root.Parent.Service, _root.Parent.Path)
                    },
                    "AccessibleId" => new PropertyValue { Type = 's', StringValue = "" },
                    "HelpText" => new PropertyValue { Type = 's', StringValue = "" },
                    _ => null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] ❌ GetPropertyValue('{propertyName}') exception: {ex.Message}");
                Console.WriteLine($"[AtspiRootPropertiesMethodHandler] Stack: {ex.StackTrace}");
                throw;
            }
        }

        private void WriteVariant(ref MessageWriter writer, PropertyValue value)
        {
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
                    break;
                default:
                    throw new NotSupportedException($"Type '{value.Type}' not supported");
            }
        }

        private void WriteVariantValue(ref MessageWriter writer, PropertyValue value)
        {
            switch (value.Type)
            {
                case 's':
                    writer.WriteSignature("s");
                    writer.WriteString(value.StringValue ?? "");
                    break;
                case 'i':
                    writer.WriteSignature("i");
                    writer.WriteInt32(value.IntValue);
                    break;
                case 'r': // struct (so)
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
