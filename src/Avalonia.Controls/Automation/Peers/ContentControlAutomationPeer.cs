using System;
using Avalonia.Controls;

namespace Avalonia.Automation.Peers
{
    public class ContentControlAutomationPeer : ControlAutomationPeer
    {
        protected ContentControlAutomationPeer(ContentControl owner)
            : base(owner) 
        { 
        }

        public new ContentControl Owner => (ContentControl)base.Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string? GetNameCore()
        {
            var result = base.GetNameCore();
            Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] base result: '{result ?? "NULL"}'");
            Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] Owner: {Owner?.GetType().Name ?? "NULL"}");
            Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] Owner.Content: {Owner?.Content ?? "NULL"}");
            Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] Owner.Content type: {Owner?.Content?.GetType().Name ?? "NULL"}");

            if (result is null && Owner?.Presenter?.Child is TextBlock text)
            {
                result = text.Text;
                Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] From TextBlock: '{result ?? "NULL"}'");
            }

            if (result is null && Owner?.Content is object content)
            {
                result = content.ToString();
                Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] From Content.ToString(): '{result ?? "NULL"}'");
            }

            Console.WriteLine($"[ContentControlAutomationPeer.GetNameCore] Final result: '{result ?? "NULL"}'");
            return result;
        }

        protected override bool IsContentElementCore() => false;
        protected override bool IsControlElementCore() => false;
    }
}
