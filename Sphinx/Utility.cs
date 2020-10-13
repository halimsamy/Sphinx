using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using dnlib.DotNet.Writer;

namespace Sphinx
{
    internal class Utility
    {
        // ReSharper disable once InconsistentNaming
        public static byte[] SHA1(byte[] buffer)
        {
            var sha = new SHA1Managed();
            return sha.ComputeHash(buffer);
        }

        // ReSharper disable once InconsistentNaming
        public static byte[] SHA256(byte[] buffer)
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
        public static byte[] Xor(byte[] buffer1, byte[] buffer2)
        {
            if (buffer1.Length != buffer2.Length)
                throw new ArgumentException("Length mismatched.");
            var ret = new byte[buffer1.Length];
            for (var i = 0; i < ret.Length; i++)
                ret[i] = (byte) (buffer1[i] ^ buffer2[i]);
            return ret;
        }

        public static string Base64Encode(byte[] buf)
        {
            return Convert.ToBase64String(buf).Trim('=').Replace('+', '$').Replace('/', '_');
        }

        public static string Base64Encode(string str)
        {
            return Base64Encode(Encoding.ASCII.GetBytes(str));
        }

        public static byte[] Base64Decode(string str)
        {
            str = str.Replace('$', '+').Replace('_', '/').PadRight((str.Length + 3) & ~3, '=');
            return Convert.FromBase64String(str);
        }

        public static string RandomString()
        {
            return Path.GetRandomFileName().Replace(".", "");
        }

        public static string RandomBase64Encode()
        {
            return Base64Encode(RandomString());
        }

        public static string Shuffle(string str)
        {
            var span = str.ToCharArray().AsSpan();
            for (var i = span.Length - 1; i > 1; i--)
            {
                var k = RandomNumberGenerator.GetInt32(1, i + 1);
                var tmp = span[k];
                span[k] = span[i];
                span[i] = tmp;
            }

            return span.ToString();
        }

        public static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 1; i--)
            {
                var k = RandomNumberGenerator.GetInt32(1, i + 1);
                var tmp = list[k];
                list[k] = list[i];
                list[i] = tmp;
            }
        }

        public static void Shuffle<T>(MDTable<T> table) where T : struct
        {
            if (table.IsEmpty) return;

            for (var i = (uint) table.Rows; i > 2; i--)
            {
                var k = Convert.ToUInt32(RandomNumberGenerator.GetInt32(1, (int) i - 1)) + 1;
                Debug.Assert(k >= 1, $"{nameof(k)} >= 1");
                Debug.Assert(k < i, $"{nameof(k)} < {nameof(i)}");
                Debug.Assert(k <= table.Rows, $"{nameof(k)} <= {nameof(table)}.Rows");

                var tmp = table[k];
                table[k] = table[i];
                table[i] = tmp;
            }
        }
    }
}