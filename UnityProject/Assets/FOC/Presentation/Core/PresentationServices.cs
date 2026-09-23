#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Domain.Common;

namespace FOC.Presentation.Core
{
    public interface IPresentationLocalizer { string Get(string key); }

    public sealed class KeyFallbackLocalizer : IPresentationLocalizer
    {
        private readonly IReadOnlyDictionary<string, string> _values;
        public KeyFallbackLocalizer(IReadOnlyDictionary<string, string>? values = null) { _values = values ?? new Dictionary<string, string>(); }
        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return _values.TryGetValue(key, out var value) ? value : key;
        }
    }

    public interface IPresentationFormatter
    {
        string Integer(long value);
        string Money(long value);
        string WorldTime(long ticks);
        string Availability(PresentationAvailability availability);
    }

    public sealed class InvariantPresentationFormatter : IPresentationFormatter
    {
        public string Integer(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public string Money(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
        public string WorldTime(long ticks) => "T+" + ticks.ToString(CultureInfo.InvariantCulture);
        public string Availability(PresentationAvailability availability) => "presentation.availability." + availability.ToString().ToLowerInvariant();
    }

    public sealed class PresentationViewerContext
    {
        private readonly HashSet<PresentationEntityRef> _exactEntities;
        public PresentationViewerContext(FactionId actorId, PresentationEntityRef controlledIdentity, IEnumerable<PresentationEntityRef>? exactEntities = null, bool developmentDebug = false)
        {
            if (!actorId.IsValid) throw new ArgumentException("Viewing actor is required.", nameof(actorId));
            ActorId = actorId;
            ControlledIdentity = controlledIdentity;
            DevelopmentDebug = developmentDebug;
            _exactEntities = new HashSet<PresentationEntityRef>(exactEntities ?? Array.Empty<PresentationEntityRef>());
            _exactEntities.Add(controlledIdentity);
        }
        public FactionId ActorId { get; }
        public PresentationEntityRef ControlledIdentity { get; }
        public bool DevelopmentDebug { get; }
        public bool CanReadExact(PresentationEntityRef entity) => _exactEntities.Contains(entity);
    }

    public readonly struct PresentationRoute : IEquatable<PresentationRoute>
    {
        public PresentationRoute(PresentationScreenId screen, PresentationEntityRef? subject = null) { Screen = screen; Subject = subject; }
        public PresentationScreenId Screen { get; }
        public PresentationEntityRef? Subject { get; }
        public bool Equals(PresentationRoute other) => Screen == other.Screen && Nullable.Equals(Subject, other.Subject);
        public override bool Equals(object? obj) => obj is PresentationRoute other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Screen * 397) ^ (Subject?.GetHashCode() ?? 0));
    }

    public sealed class PresentationNavigator : IDisposable
    {
        private readonly List<PresentationRoute> _history = new List<PresentationRoute>();
        private int _position = -1;
        private bool _disposed;
        public event Action<PresentationRoute>? Changed;
        public PresentationRoute? Current => _position >= 0 ? _history[_position] : (PresentationRoute?)null;
        public bool CanGoBack => _position > 0;
        public bool CanGoForward => _position >= 0 && _position < _history.Count - 1;
        public int SubscriptionCount => Changed?.GetInvocationList().Length ?? 0;

        public void Navigate(PresentationRoute route)
        {
            ThrowIfDisposed();
            if (Current.HasValue && Current.Value.Equals(route)) return;
            if (_position < _history.Count - 1) _history.RemoveRange(_position + 1, _history.Count - _position - 1);
            _history.Add(route);
            _position = _history.Count - 1;
            Changed?.Invoke(route);
        }
        public bool Back() { ThrowIfDisposed(); if (!CanGoBack) return false; _position--; Changed?.Invoke(_history[_position]); return true; }
        public bool Forward() { ThrowIfDisposed(); if (!CanGoForward) return false; _position++; Changed?.Invoke(_history[_position]); return true; }
        public void Dispose() { if (_disposed) return; _disposed = true; Changed = null; _history.Clear(); _position = -1; }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PresentationNavigator)); }
    }

    public enum PresentationLayoutClass { CompactDesktop, StandardDesktop, WideDesktop, UltraWideDesktop }
    public sealed class PresentationLayout
    {
        public PresentationLayout(PresentationLayoutClass kind, int columns, int minimumWidth, float scale)
        { Kind = kind; Columns = columns; MinimumWidth = minimumWidth; Scale = scale; }
        public PresentationLayoutClass Kind { get; }
        public int Columns { get; }
        public int MinimumWidth { get; }
        public float Scale { get; }
    }
    public static class PresentationLayoutPolicy
    {
        public static PresentationLayout Resolve(int width, int height, float scale = 1f)
        {
            if (width < 1024 || height < 600) throw new ArgumentOutOfRangeException(nameof(width), "The production desktop shell requires at least 1024x600.");
            if (scale < .75f || scale > 1.5f) throw new ArgumentOutOfRangeException(nameof(scale));
            if (width >= 3000) return new PresentationLayout(PresentationLayoutClass.UltraWideDesktop, 4, 3000, scale);
            if (width >= 2400) return new PresentationLayout(PresentationLayoutClass.WideDesktop, 3, 2400, scale);
            if (width >= 1600) return new PresentationLayout(PresentationLayoutClass.StandardDesktop, 3, 1600, scale);
            return new PresentationLayout(PresentationLayoutClass.CompactDesktop, 2, 1024, scale);
        }
    }

    public sealed class StablePresentationList<T>
    {
        private readonly IReadOnlyList<T> _items;
        public StablePresentationList(IEnumerable<T> source, Func<T, string> searchableText, Comparison<T> comparison)
        {
            if (source == null || searchableText == null || comparison == null) throw new ArgumentNullException();
            SearchableText = searchableText;
            var list = source.Select((item, index) => new Indexed(item, index)).ToList();
            list.Sort((left, right) => { var result = comparison(left.Item, right.Item); return result != 0 ? result : left.Index.CompareTo(right.Index); });
            _items = list.Select(x => x.Item).ToList().AsReadOnly();
        }
        private Func<T, string> SearchableText { get; }
        public IReadOnlyList<T> Items => _items;
        public IReadOnlyList<T> Filter(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _items;
            return _items.Where(x => SearchableText(x).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList().AsReadOnly();
        }
        private sealed class Indexed { public Indexed(T item, int index) { Item = item; Index = index; } public T Item { get; } public int Index { get; } }
    }

    public sealed class VirtualizedListWindow<T>
    {
        private readonly IReadOnlyList<T> _items;
        public VirtualizedListWindow(IReadOnlyList<T> items, int visibleCapacity, int overscan = 4)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            if (visibleCapacity <= 0 || overscan < 0) throw new ArgumentOutOfRangeException(nameof(visibleCapacity));
            VisibleCapacity = visibleCapacity;
            Overscan = overscan;
        }
        public int VisibleCapacity { get; }
        public int Overscan { get; }
        public int MaximumLiveRows => Math.Min(_items.Count, VisibleCapacity + (2 * Overscan));
        public IReadOnlyList<T> GetWindow(int firstVisibleIndex)
        {
            if (_items.Count == 0) return Array.Empty<T>();
            if (firstVisibleIndex < 0 || firstVisibleIndex >= _items.Count) throw new ArgumentOutOfRangeException(nameof(firstVisibleIndex));
            var start = Math.Max(0, firstVisibleIndex - Overscan);
            var count = Math.Min(_items.Count - start, VisibleCapacity + (2 * Overscan));
            return _items.Skip(start).Take(count).ToList().AsReadOnly();
        }
    }

    public sealed class PresentationActionResult
    {
        private PresentationActionResult(bool succeeded, string messageKey, Exception? technicalError) { Succeeded = succeeded; MessageKey = messageKey; TechnicalError = technicalError; }
        public bool Succeeded { get; }
        public string MessageKey { get; }
        public Exception? TechnicalError { get; }
        public static PresentationActionResult Success(string messageKey = "presentation.action.success") => new PresentationActionResult(true, messageKey, null);
        public static PresentationActionResult Rejected(string messageKey) => new PresentationActionResult(false, messageKey, null);
        public static PresentationActionResult Failed(Exception error) => new PresentationActionResult(false, "presentation.action.unexpected-error", error ?? throw new ArgumentNullException(nameof(error)));
    }

    public sealed class PresentationActionGate
    {
        private readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);
        public bool TryBegin(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) throw new ArgumentException(nameof(actionId));
            return _pending.Add(actionId);
        }
        public void Complete(string actionId) { _pending.Remove(actionId); }
        public bool IsPending(string actionId) => _pending.Contains(actionId);
    }

    public interface IPresentationActionDispatcher
    {
        PresentationActionResult Dispatch(PresentationActionDescriptor action);
    }

    public sealed class PresentationCommandBindingRegistry : IPresentationActionDispatcher, IDisposable
    {
        private readonly Dictionary<string, Func<PresentationActionResult>> _bindings = new Dictionary<string, Func<PresentationActionResult>>(StringComparer.Ordinal);
        private bool _disposed;
        public int Count => _bindings.Count;
        public void Register(string actionId, Func<PresentationActionResult> command)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PresentationCommandBindingRegistry));
            if (string.IsNullOrWhiteSpace(actionId) || command == null) throw new ArgumentException("Command binding is invalid.");
            if (_bindings.ContainsKey(actionId)) throw new InvalidOperationException("Presentation command binding is duplicated.");
            _bindings.Add(actionId, command);
        }
        public PresentationActionResult Dispatch(PresentationActionDescriptor action)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PresentationCommandBindingRegistry));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!action.IsEnabled) return PresentationActionResult.Rejected(action.DisabledReasonKey);
            return _bindings.TryGetValue(action.ActionId, out var command) ? command() : PresentationActionResult.Rejected("presentation.action.binding-unavailable");
        }
        public void Dispose() { if (_disposed) return; _bindings.Clear(); _disposed = true; }
    }

    public interface IPresentationScreenSource
    {
        ScreenPresentationState Get(PresentationRoute route);
    }

    public sealed class PresentationShellViewModel : IDisposable
    {
        private readonly PresentationNavigator _navigator;
        private readonly IPresentationScreenSource _source;
        private bool _disposed;
        public PresentationShellViewModel(PresentationNavigator navigator, IPresentationScreenSource source)
        {
            _navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _navigator.Changed += OnRouteChanged;
        }
        public ScreenPresentationState? Current { get; private set; }
        public event Action<ScreenPresentationState>? Changed;
        public void Open(PresentationRoute route) { ThrowIfDisposed(); _navigator.Navigate(route); }
        public bool Back() { ThrowIfDisposed(); return _navigator.Back(); }
        public bool Forward() { ThrowIfDisposed(); return _navigator.Forward(); }
        public void Refresh()
        {
            ThrowIfDisposed();
            if (!_navigator.Current.HasValue) return;
            OnRouteChanged(_navigator.Current.Value);
        }
        private void OnRouteChanged(PresentationRoute route) { Current = _source.Get(route); Changed?.Invoke(Current); }
        public void Dispose()
        {
            if (_disposed) return;
            _navigator.Changed -= OnRouteChanged;
            Changed = null;
            _disposed = true;
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(PresentationShellViewModel)); }
    }
}
