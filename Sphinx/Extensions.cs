using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using dnlib.DotNet.Writer;

namespace Sphinx
{
    internal static class Extensions
    {
        public static string RandomString()
        {
            return Path.GetRandomFileName().Replace(".", "");
        }

        // ReSharper disable once InconsistentNaming
        public static byte[] SHA1(this byte[] buffer)
        {
            var sha = new SHA1Managed();
            return sha.ComputeHash(buffer);
        }

        // ReSharper disable once InconsistentNaming
        public static byte[] SHA256(this byte[] buffer)
        {
            var sha = new SHA256Managed();
            return sha.ComputeHash(buffer);
        }

        /// <summary>
        ///     Xor the values in the two buffer together.
        /// </summary>
        /// <param name="buffer1">The input buffer 1.</param>
        /// <param name="buffer2">The input buffer 2.</param>
        /// <returns>The result buffer.</returns>
        /// <exception cref="System.ArgumentException">Length of the two buffers are not equal.</exception>
        // ReSharper disable once InconsistentNaming
        public static byte[] XOR(this byte[] buffer1, byte[] buffer2)
        {
            if (buffer1.Length != buffer2.Length)
                throw new ArgumentException("Length mismatched.");
            var ret = new byte[buffer1.Length];
            for (var i = 0; i < ret.Length; i++)
                ret[i] = (byte) (buffer1[i] ^ buffer2[i]);
            return ret;
        }

        /// <summary>
        ///     Used for <see cref="Components.ConstRemovalComponent" />
        /// </summary>
        /// <param name="s"></param>
        /// <param name="n"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        public static string XOR(this string s, int n, byte[] key)
        {
            var num = s.Length;
            var array = s.ToCharArray();
            while (--num >= 0) array[num] = (char) (array[num] ^ (key[n & (key.Length - 1)] | n));
            return new string(array);
        }

        /// <summary>
        ///     If the input string is empty, return null; otherwise, return the original input string.
        /// </summary>
        /// <param name="val">The input string.</param>
        /// <returns><c>null</c> if the input string is empty; otherwise, the original input string.</returns>
        public static string NullIfEmpty(this string val)
        {
            return string.IsNullOrEmpty(val) ? null : val;
        }

        public static string Base64Encode(this byte[] buf)
        {
            return Convert.ToBase64String(buf).Trim('=').Replace('+', '$').Replace('/', '_');
        }

        public static string Base64Encode(this string str)
        {
            return Base64Encode(Encoding.ASCII.GetBytes(str));
        }

        public static byte[] Base64Decode(this string str)
        {
            str = str.Replace('$', '+').Replace('_', '/').PadRight((str.Length + 3) & ~3, '=');
            return Convert.FromBase64String(str);
        }

        /// <summary>
        ///     Removes all elements that match the conditions defined by the specified predicate from a the list.
        /// </summary>
        /// <typeparam name="T">The type of the elements of <paramref name="self" />.</typeparam>
        /// <param name="self">The list to remove from.</param>
        /// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
        /// <returns><paramref name="self" /> for method chaining.</returns>
        public static IList<T> RemoveWhere<T>(this IList<T> self, Predicate<T> match)
        {
            for (var i = self.Count - 1; i >= 0; i--)
                if (match(self[i]))
                    self.RemoveAt(i);
            return self;
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            for (var i = list.Count - 1; i > 1; i--)
            {
                var k = RandomNumberGenerator.GetInt32(1, i + 1);
                var tmp = list[k];
                list[k] = list[i];
                list[i] = tmp;
            }
        }

        public static void Shuffle<T>(this MDTable<T> table) where T : struct
        {
            if (table.IsEmpty) return;

            for (var i = (uint) table.Rows; i > 2; i--)
            {
                var k = Convert.ToUInt32(RandomNumberGenerator.GetInt32(1, (int) i - 1)) + 1;

                var tmp = table[k];
                table[k] = table[i];
                table[i] = tmp;
            }
        }

        /// <summary>
        ///     Gets the value associated with the specified key, or default value if the key does not exists.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="defValue">The default value.</param>
        /// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defValue = default)
        {
            return dictionary.TryGetValue(key, out var ret) ? ret : defValue;
        }

        /// <summary>
        ///     Gets the value associated with the specified key, or default value if the key does not exists.
        /// </summary>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="defValueFactory">The default value factory function.</param>
        /// <returns>The value associated with the specified key, or the default value if the key does not exists</returns>
        public static TValue GetValueOrDefaultLazy<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            Func<TKey, TValue> defValueFactory)
        {
            return dictionary.TryGetValue(key, out var ret) ? ret : defValueFactory(key);
        }

        /// <summary>
        ///     Adds the specified key and value to the multi dictionary.
        /// </summary>
        /// <typeparam name="TKey">The type of key.</typeparam>
        /// <typeparam name="TValue">The type of value.</typeparam>
        /// <param name="self">The dictionary to add to.</param>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add.</param>
        /// <exception cref="System.ArgumentNullException">key is <c>null</c>.</exception>
        public static void AddListEntry<TKey, TValue>(this IDictionary<TKey, List<TValue>> self, TKey key, TValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (!self.TryGetValue(key, out var list))
                list = self[key] = new List<TValue>();
            list.Add(value);
        }
    }
}