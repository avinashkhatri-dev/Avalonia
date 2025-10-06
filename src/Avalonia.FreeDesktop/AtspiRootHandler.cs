using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation.Peers;
using Avalonia.FreeDesktop.Atspi;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// Concrete implementation of D-Bus AT-SPI handlers for the root application object
    /// This handler only inherits from OrgA11yAtspiAccessibleHandler but manually implements
    /// the application interface methods through delegation.
    /// </summary>
    internal class AtspiRootHandler : OrgA11yAtspiAccessibleHandler
    {
        private readonly AtspiRoot _root;
        private readonly Connection _connection;

        public AtspiRootHandler(AtspiRoot root, Connection connection) : base()
        {
            _root = root;
            _connection = connection;
            
            // Initialize basic properties from the root
            InitializeBasicProperties();
        }

        public override Connection Connection => _connection;

        private void InitializeBasicProperties()
        {
            Name = "Test Application";
            Description = "Avalonia Test Application";
            Parent = (null, default(ObjectPath));
            ChildCount = 0;
            Locale = "en_US";
            AccessibleId = "/org/a11y/atspi/accessible/root";
            HelpText = "Root application object";
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetChildAtIndexAsync(Message request, int index)
        {
            var result = await ((IAccessible)_root).GetChildAtIndexAsync(index);
            return (result.Service ?? "", result.Path);
        }

        protected override async ValueTask<(string, ObjectPath)[]> OnGetChildrenAsync(Message request)
        {
            var result = await ((IAccessible)_root).GetChildrenAsync();
            return result.Select(r => (r.Service ?? "", r.Path)).ToArray();
        }

        protected override async ValueTask<int> OnGetIndexInParentAsync(Message request)
        {
            return await ((IAccessible)_root).GetIndexInParentAsync();
        }

        protected override async ValueTask<(uint, (string, ObjectPath)[])[]> OnGetRelationSetAsync(Message request)
        {
            var result = await ((IAccessible)_root).GetRelationSetAsync();
            return result.Select(r => (r.Item1, r.Item2.Select(o => (o.Service ?? "", o.Path)).ToArray())).ToArray();
        }

        protected override async ValueTask<uint> OnGetRoleAsync(Message request)
        {
            return await ((IAccessible)_root).GetRoleAsync();
        }

        protected override async ValueTask<string> OnGetRoleNameAsync(Message request)
        {
            return await ((IAccessible)_root).GetRoleNameAsync();
        }

        protected override async ValueTask<string> OnGetLocalizedRoleNameAsync(Message request)
        {
            return await ((IAccessible)_root).GetLocalizedRoleNameAsync();
        }

        protected override async ValueTask<uint[]> OnGetStateAsync(Message request)
        {
            return await ((IAccessible)_root).GetStateAsync();
        }

        protected override async ValueTask<Dictionary<string, string>> OnGetAttributesAsync(Message request)
        {
            var result = await ((IAccessible)_root).GetAttributesAsync();
            return new Dictionary<string, string>(result);
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetApplicationAsync(Message request)
        {
            var result = await ((IAccessible)_root).GetApplicationAsync();
            return (result.Service ?? "", result.Path);
        }

        protected override async ValueTask<string[]> OnGetInterfacesAsync(Message request)
        {
            // Root supports both Accessible and Application interfaces
            return new[]
            {
                "org.a11y.atspi.Accessible",
                "org.a11y.atspi.Application"
            };
        }
    }
}