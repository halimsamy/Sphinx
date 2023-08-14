using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace Sphinx
{
    /// <summary>
    ///     Provides methods to annotate objects.
    /// </summary>
    /// <remarks>
    ///     The annotations are stored using <see cref="WeakReference" />
    /// </remarks>
    public class Annotations
    {
        private readonly Dictionary<object, ListDictionary> _annotations =
            new Dictionary<object, ListDictionary>(WeakReferenceComparer.Instance);

        /// <summary>
        ///     Retrieves the annotation on the specified object associated with the specified key.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="defValue">The default value if the specified annotation does not exists on the object.</param>
        /// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
        /// </exception>
        public TValue Get<TValue>(object obj, object key, TValue defValue = default)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (!this._annotations.TryGetValue(obj, out var objAnno))
                return defValue;
            if (!objAnno.Contains(key))
                return defValue;

            var valueType = typeof(TValue);
            if (valueType.IsValueType)
                return (TValue) Convert.ChangeType(objAnno[key], typeof(TValue));
            return (TValue) objAnno[key];
        }

        /// <summary>
        ///     Retrieves the annotation on the specified object associated with the specified key.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="defValueFactory">The default value factory function.</param>
        /// <returns>The value of annotation, or default value if the annotation does not exist.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
        /// </exception>
        public TValue GetLazy<TValue>(object obj, object key, Func<object, TValue> defValueFactory)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (!this._annotations.TryGetValue(obj, out var objAnno))
                return defValueFactory(key);
            if (!objAnno.Contains(key))
                return defValueFactory(key);

            var valueType = typeof(TValue);
            if (valueType.IsValueType)
                return (TValue) Convert.ChangeType(objAnno[key], typeof(TValue));
            return (TValue) objAnno[key];
        }

        /// <summary>
        ///     Retrieves or create the annotation on the specified object associated with the specified key.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="factory">The factory function to create the annotation value when the annotation does not exist.</param>
        /// <returns>The value of annotation, or the newly created value.</returns>
        /// <exception cref="System.ArgumentNullException">
        ///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
        /// </exception>
        public TValue GetOrCreate<TValue>(object obj, object key, Func<object, TValue> factory)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (!this._annotations.TryGetValue(obj, out var objAnno))
                objAnno = this._annotations[new WeakReferenceKey(obj)] = new ListDictionary();
            TValue ret;
            if (objAnno.Contains(key))
            {
                var valueType = typeof(TValue);
                if (valueType.IsValueType)
                    return (TValue) Convert.ChangeType(objAnno[key], typeof(TValue));
                return (TValue) objAnno[key];
            }

            objAnno[key] = ret = factory(key);
            return ret;
        }

        /// <summary>
        ///     Sets an annotation on the specified object.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="obj">The object.</param>
        /// <param name="key">The key of annotation.</param>
        /// <param name="value">The value of annotation.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     <paramref name="obj" /> or <paramref name="key" /> is <c>null</c>.
        /// </exception>
        public void Set<TValue>(object obj, object key, TValue value)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            if (!this._annotations.TryGetValue(obj, out var objAnno))
                objAnno = this._annotations[new WeakReferenceKey(obj)] = new ListDictionary();
            objAnno[key] = value;
        }

        /// <summary>
        ///     Trims the annotations of unreachable objects from this instance.
        /// </summary>
        public void Trim()
        {
            foreach (var key in this._annotations
                .Where(kvp => !((WeakReferenceKey) kvp.Key).IsAlive)
                .Select(kvp => kvp.Key))
                this._annotations.Remove(key);
        }

        /// <summary>
        ///     Equality comparer of weak references.
        /// </summary>
        private class WeakReferenceComparer : IEqualityComparer<object>
        {
            /// <summary>
            ///     The singleton instance of this comparer.
            /// </summary>
            public static readonly WeakReferenceComparer Instance = new WeakReferenceComparer();

            /// <summary>
            ///     Prevents a default instance of the <see cref="WeakReferenceComparer" /> class from being created.
            /// </summary>
            private WeakReferenceComparer()
            {
            }

            /// <inheritdoc />
            public new bool Equals(object x, object y)
            {
                if (y is WeakReferenceKey && !(x is WeakReference)) return this.Equals(y, x);
                var xWeak = x as WeakReferenceKey;
                var yWeak = y as WeakReferenceKey;
                if (xWeak != null && yWeak != null)
                    return xWeak.IsAlive && yWeak.IsAlive && ReferenceEquals(xWeak.Target, yWeak.Target);
                if (xWeak != null) return xWeak.IsAlive && ReferenceEquals(xWeak.Target, y);
                if (yWeak != null) return yWeak.IsAlive && ReferenceEquals(yWeak.Target, x);
                /*
				if (xWeak == null && yWeak == null) return xWeak.IsAlive && ReferenceEquals(xWeak.Target, y);
				*/
                throw new UnreachableException();
            }

            /// <inheritdoc />
            public int GetHashCode(object obj)
            {
                return obj is WeakReferenceKey key ? key.HashCode : obj.GetHashCode();
            }
        }

        /// <summary>
        ///     Represent a key using <see cref="WeakReference" />.
        /// </summary>
        private class WeakReferenceKey : WeakReference
        {
            /// <inheritdoc />
            public WeakReferenceKey(object target)
                : base(target)
            {
                this.HashCode = target.GetHashCode();
            }

            /// <summary>
            ///     Gets the hash code of the target object.
            /// </summary>
            /// <value>The hash code.</value>
            public int HashCode { get; }
        }
    }
}