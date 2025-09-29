using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.FreeDesktop.Atspi;
using Tmds.DBus.Protocol;

#nullable enable

namespace Avalonia.FreeDesktop
{
    internal class AtspiCache : ICache
    {
        private readonly AtspiRoot _root;
        private readonly ObservableCollection<CacheItem> _items = new ObservableCollection<CacheItem>();

        public AtspiCache(AtspiRoot root)
        {
            _root = root;
        }

        public ObjectPath ObjectPath => "/org/a11y/atspi/cache";

        public void Add(AtspiContext item) => _items.Add(item.ToCacheItem());
        public Task<CacheItem[]> GetItemsAsync() => Task.FromResult(_items.ToArray());
        public Task<IDisposable> WatchAddAccessibleAsync(Action<CacheItem> handler, Action<Exception>? onError = null)
        {
            void Listener(object? s, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
                    foreach (CacheItem i in e.NewItems) handler(i);
            }

            _items.CollectionChanged += Listener;
            return Task.FromResult((IDisposable)new CollectionChangedDisposable(_items, Listener));
        }

        public Task<IDisposable> WatchRemoveAccessibleAsync(Action<CacheItem> handler, Action<Exception>? onError = null)
        {
            void Listener(object? s, NotifyCollectionChangedEventArgs e)
            {
                if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
                    foreach (CacheItem i in e.OldItems) handler(i);
            }

            _items.CollectionChanged += Listener;
            return Task.FromResult((IDisposable)new CollectionChangedDisposable(_items, Listener));
        }
    }

    internal class CollectionChangedDisposable : IDisposable
    {
        private readonly ObservableCollection<CacheItem> _collection;
        private readonly NotifyCollectionChangedEventHandler _handler;

        public CollectionChangedDisposable(ObservableCollection<CacheItem> collection, NotifyCollectionChangedEventHandler handler)
        {
            _collection = collection;
            _handler = handler;
        }

        public void Dispose()
        {
            _collection.CollectionChanged -= _handler;
        }
    }
}