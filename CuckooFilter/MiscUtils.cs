using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CuckooFilter
{
	public static class PrimitiveHelpers
	{
		public static bool GetBit (uint x, int bitnum)
		{
#if DEBUG
			Debug.Assert (! (bitnum < 0 || bitnum > 31), "Invalid bit number");
#endif
			return (x & (1 << bitnum)) != 0;
		}

		public static void SetBit (ref uint x, int bitnum)
		{
			#if DEBUG
			Debug.Assert (! (bitnum < 0 || bitnum > 31), "Invalid bit number");
			#endif

			x |= (UInt32)(1 << bitnum);

		}

		public static void ClearBit (ref uint x, int bitnum)
		{
			#if DEBUG
			Debug.Assert (! (bitnum < 0 || bitnum > 31), "Invalid bit number");
			#endif

			x &= ~(UInt32)(1 << bitnum);
		}

		public static ulong RotateLeft (this ulong original, int bits)
		{
			return (original << bits) | (original >> (64 - bits));
		}

		public static ulong RotateRight (this ulong original, int bits)
		{
			return (original >> bits) | (original << (64 - bits));
		}

		unsafe public static ulong GetUInt64 (this byte[] bb, int pos)
		{
			// we read aligned longs, so a simple casting is enough
			fixed (byte* pbyte = &bb[pos]) {
				return *((ulong*)pbyte);
			}
		}

		unsafe public static uint GetUInt32 (this byte[] bb, int pos)
		{
			// we read aligned ints, so a simple casting is enough
			fixed (byte* pbyte = &bb[pos]) {
				return *((uint*)pbyte);
			}
		}

		public static uint Upperpower2 (uint x)
		{
			x--;
			x |= x >> 1;
			x |= x >> 2;
			x |= x >> 4;
			x |= x >> 8;
			x |= x >> 16; 
			x |= x >> 32; 
			x++;
			return x;
		}
	}

	public static class StringUtils
	{
		public static byte[] GetBytes (this string str)
		{
			byte[] bytes = new byte[str.Length * sizeof(char)];
			System.Buffer.BlockCopy (str.ToCharArray (), 0, bytes, 0, bytes.Length);
			return bytes;
		}

		public static string GetString (this byte[] bytes)
		{
			char[] chars = new char[bytes.Length / sizeof(char)];
			System.Buffer.BlockCopy (bytes, 0, chars, 0, bytes.Length);
			return new string (chars);
		}
	}

	public interface IBytesProvider<T>
	{
		byte[] GetBytes (T value);
	}

	public class BytesProvider<T> : IBytesProvider<T>
	{
		public static BytesProvider<T> Default {
			get { return DefaultBytesProviders.GetDefaultProvider<T> (); }
		}

		Func<T, byte[]> _conversion;

		internal BytesProvider (Func<T, byte[]> conversion)
		{
			_conversion = conversion;
		}

		public byte[] GetBytes (T value)
		{
			return _conversion (value);
		}
	}

	static class DefaultBytesProviders
	{
		static Dictionary<Type, object> _providers;

		static DefaultBytesProviders ()
		{
			// Here are a couple for illustration. Yes, I am suggesting that
			// in reality you would add a BytesProvider<T> for each T
			// supported by the BitConverter class.
			_providers = new Dictionary<Type, object> {
				{ typeof(ushort), new BytesProvider<ushort>(BitConverter.GetBytes) },
				{ typeof(uint), new BytesProvider<uint>(BitConverter.GetBytes) },
				{ typeof(ulong), new BytesProvider<ulong>(BitConverter.GetBytes) },
				{ typeof(short), new BytesProvider<short>(BitConverter.GetBytes) },
				{ typeof(int), new BytesProvider<int>(BitConverter.GetBytes) },
				{ typeof(long), new BytesProvider<long>(BitConverter.GetBytes) },
				{ typeof(float), new BytesProvider<float>(BitConverter.GetBytes) },
				{ typeof(double), new BytesProvider<double>(BitConverter.GetBytes) }
			};
		}

		public static BytesProvider<T> GetDefaultProvider<T> ()
		{
			return (BytesProvider<T>)_providers [typeof(T)];
		}
	}
}

