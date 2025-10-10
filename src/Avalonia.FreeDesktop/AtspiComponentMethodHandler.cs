using System;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;
using Avalonia.FreeDesktop.Atspi;

#nullable enable

namespace Avalonia.FreeDesktop
{
    /// <summary>
    /// D-Bus method handler for AT-SPI Component interface methods.
    /// This handles spatial and UI interaction methods for AT-SPI objects.
    /// </summary>
    internal class AtspiComponentMethodHandler : OrgA11yAtspiComponentHandler
    {
        private readonly IComponent _component;
        private readonly Connection _connection;

        internal AtspiComponentMethodHandler(IComponent component, Connection connection) : base()
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            
            Console.WriteLine($"[AtspiComponentMethodHandler] Created for component object");
        }

        public override Connection Connection => _connection;

        protected override async ValueTask<bool> OnContainsAsync(Message request, int x, int y, uint coordType)
        {
            var contains = await _component.ContainsAsync(x, y, coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] Contains({x}, {y}, {coordType}) -> {contains}");
            return contains;
        }

        protected override async ValueTask<(string, ObjectPath)> OnGetAccessibleAtPointAsync(Message request, int x, int y, uint coordType)
        {
            var accessible = await _component.GetAccessibleAtPointAsync(x, y, coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] GetAccessibleAtPoint({x}, {y}, {coordType}) -> {accessible.Service}:{accessible.Path}");
            return (accessible.Service, accessible.Path);
        }

        protected override async ValueTask<(int, int, int, int)> OnGetExtentsAsync(Message request, uint coordType)
        {
            var extents = await _component.GetExtentsAsync(coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] GetExtents({coordType}) -> ({extents.Item1}, {extents.Item2}, {extents.Item3}, {extents.Item4})");
            return (extents.Item1, extents.Item2, extents.Item3, extents.Item4);
        }

        protected override async ValueTask<(int, int)> OnGetPositionAsync(Message request, uint coordType)
        {
            var position = await _component.GetPositionAsync(coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] GetPosition({coordType}) -> ({position.Item1}, {position.Item2})");
            return (position.Item1, position.Item2);
        }

        protected override async ValueTask<(int, int)> OnGetSizeAsync(Message request)
        {
            var size = await _component.GetSizeAsync();
            Console.WriteLine($"[AtspiComponentMethodHandler] GetSize() -> ({size.Item1}, {size.Item2})");
            return (size.Item1, size.Item2);
        }

        protected override async ValueTask<uint> OnGetLayerAsync(Message request)
        {
            var layer = await _component.GetLayerAsync();
            Console.WriteLine($"[AtspiComponentMethodHandler] GetLayer() -> {layer}");
            return layer;
        }

        protected override async ValueTask<short> OnGetMDIZOrderAsync(Message request)
        {
            var zOrder = await _component.GetMDIZOrderAsync();
            Console.WriteLine($"[AtspiComponentMethodHandler] GetMDIZOrder() -> {zOrder}");
            return zOrder;
        }

        protected override async ValueTask<bool> OnGrabFocusAsync(Message request)
        {
            var success = await _component.GrabFocusAsync();
            Console.WriteLine($"[AtspiComponentMethodHandler] GrabFocus() -> {success}");
            return success;
        }

        protected override async ValueTask<double> OnGetAlphaAsync(Message request)
        {
            var alpha = await _component.GetAlphaAsync();
            Console.WriteLine($"[AtspiComponentMethodHandler] GetAlpha() -> {alpha}");
            return alpha;
        }

        protected override async ValueTask<bool> OnSetExtentsAsync(Message request, int x, int y, int width, int height, uint coordType)
        {
            var success = await _component.SetExtentsAsync(x, y, width, height, coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] SetExtents({x}, {y}, {width}, {height}, {coordType}) -> {success}");
            return success;
        }

        protected override async ValueTask<bool> OnSetPositionAsync(Message request, int x, int y, uint coordType)
        {
            var success = await _component.SetPositionAsync(x, y, coordType);
            Console.WriteLine($"[AtspiComponentMethodHandler] SetPosition({x}, {y}, {coordType}) -> {success}");
            return success;
        }

        protected override async ValueTask<bool> OnSetSizeAsync(Message request, int width, int height)
        {
            var success = await _component.SetSizeAsync(width, height);
            Console.WriteLine($"[AtspiComponentMethodHandler] SetSize({width}, {height}) -> {success}");
            return success;
        }

        protected override async ValueTask OnScrollToAsync(Message request, uint scrollType)
        {
            await _component.ScrollToAsync(scrollType);
            Console.WriteLine($"[AtspiComponentMethodHandler] ScrollTo({scrollType})");
        }

        protected override async ValueTask<bool> OnScrollToPointAsync(Message request, uint coordType, int x, int y)
        {
            var success = await _component.ScrollToPointAsync(coordType, x, y);
            Console.WriteLine($"[AtspiComponentMethodHandler] ScrollToPoint({coordType}, {x}, {y}) -> {success}");
            return success;
        }
    }
}