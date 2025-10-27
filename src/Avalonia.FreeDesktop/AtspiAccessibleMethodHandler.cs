using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;
using Avalonia.FreeDesktop.Atspi;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus method handler for AT-SPI Accessible interface methods.
    /// This handles actual AT-SPI method calls from screen readers, not just introspection.
    /// </summary>
    internal class AtspiAccessibleMethodHandler : OrgA11yAtspiAccessibleHandler
    {
        private readonly IAccessible _accessible;
        private readonly Connection _connection;

        internal AtspiAccessibleMethodHandler(IAccessible accessible, Connection connection) : base()
        {
            _accessible = accessible ?? throw new ArgumentNullException(nameof(accessible));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiAccessibleMethodHandler] Created for accessible object");
        }

        public override Connection Connection => _connection;

        protected override async ValueTask<uint> OnGetRoleAsync(Message request)
        {
            Console.WriteLine($"[AtspiAccessibleMethodHandler] 🔥 D-BUS METHOD CALLED: GetRole() from {request.SenderAsString} for path {request.PathAsString}");
            var role = await _accessible.GetRoleAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetRole() -> {role}");
            return role;
        }


        protected override async ValueTask<string> OnGetRoleNameAsync(Message request)
        {
            var roleName = await _accessible.GetRoleNameAsync();
            return roleName;
        }

        protected override async ValueTask<string> OnGetLocalizedRoleNameAsync(Message request)
        {
            var localizedRoleName = await _accessible.GetLocalizedRoleNameAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetLocalizedRoleName() -> '{localizedRoleName}'");
            return localizedRoleName;
        }

        protected override async ValueTask<uint[]> OnGetStateAsync(Message request)
        {
            var state = await _accessible.GetStateAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetState() -> [{string.Join(", ", state)}]");
            return state;
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetChildAtIndexAsync(Message request, int index)
        {
            var childRef = await _accessible.GetChildAtIndexAsync(index);
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetChildAtIndex({index}) -> {childRef.Service}:{childRef.Path}");
            return (childRef.Service, childRef.Path);
        }

        protected override async ValueTask<(string, ObjectPath)[]> OnGetChildrenAsync(Message request)
        {
            var children = await _accessible.GetChildrenAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetChildren() -> {children.Length} children");
            
            var result = new (string, ObjectPath)[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                result[i] = (children[i].Service, children[i].Path);
            }
            return result;
        }

        protected override async ValueTask<int> OnGetIndexInParentAsync(Message request)
        {
            var index = await _accessible.GetIndexInParentAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetIndexInParent() -> {index}");
            return index;
        }

        protected override async ValueTask<(uint, (string, ObjectPath)[])[]> OnGetRelationSetAsync(Message request)
        {
            var relationSet = await _accessible.GetRelationSetAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetRelationSet() -> {relationSet.Length} relations");
            
            var result = new (uint, (string, ObjectPath)[])[relationSet.Length];
            for (int i = 0; i < relationSet.Length; i++)
            {
                var (relationType, targets) = relationSet[i];
                var targetTuples = new (string, ObjectPath)[targets.Length];
                for (int j = 0; j < targets.Length; j++)
                {
                    targetTuples[j] = (targets[j].Service, targets[j].Path);
                }
                result[i] = (relationType, targetTuples);
            }
            return result;
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetApplicationAsync(Message request)
        {
            var appRef = await _accessible.GetApplicationAsync();
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetApplication() -> {appRef.Service}:{appRef.Path}");
            return (appRef.Service, appRef.Path);
        }

        protected override async ValueTask<Dictionary<string, string>> OnGetAttributesAsync(Message request)
        {
            var attributes = await _accessible.GetAttributesAsync();
            return new Dictionary<string, string>(attributes);
        }

        protected override async ValueTask<string[]> OnGetInterfacesAsync(Message request)
        {
            // Return the list of AT-SPI interfaces this object supports
            var interfaces = new[] { "org.a11y.atspi.Accessible" };
            
            // Add Component interface if this object supports it
            if (_accessible is IComponent)
            {
                interfaces = new[] { "org.a11y.atspi.Accessible", "org.a11y.atspi.Component" };
            }
            
            Console.WriteLine($"[AtspiAccessibleMethodHandler] GetInterfaces() -> [{string.Join(", ", interfaces)}]");
            return interfaces;
        }
    }
}