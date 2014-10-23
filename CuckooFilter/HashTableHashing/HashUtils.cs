using System;
using System.Security.Cryptography;

namespace CuckooFilter
{
	public static class HashUtils
	{
		static HashUtils()
		{
			//create new instance of md5
			sha1 = SHA1.Create();
		}

		/// <summary>
		/// Bob Jenkins Hash.
		/// </summary>
		/// <returns>The hash.</returns>
		/// <param name="buf">Buffer.</param>
		/// <param name="length">Length.</param>
		/// <param name="seed">Seed.</param>
		public static ushort BobHash (byte[] buf, int length, uint seed = 0)
		{
			throw new NotImplementedException ();
		}

		public static ushort BobHash (string s, uint seed = 0)
		{
			byte[] buff = s.GetBytes ();
			return BobHash (buff, buff.Length, seed);
		}
		// Bob Jenkins Hash that returns two indices in one call
		// Useful for Cuckoo hashing, power of two choices, etc.
		// Use idx1 before idx2, when possible. idx1 and idx2 should be initialized to seeds.
		public static void BobHash (byte[] buf, int length, out ushort idx1, out ushort idx2)
		{
			throw new NotImplementedException ();
		}

		public static void BobHash (string s, out ushort idx1, out ushort idx2)
		{
			byte[] buff = s.GetBytes ();
			BobHash (buff, buff.Length, out idx1, out idx2);
		}
		// MurmurHash 2
		static ushort Murmur2Hash (byte[] buf, int length, uint seed = 0)
		{
			throw new NotImplementedException ();
		}

		public static ushort Murmur2Hash (string  s, uint seed = 0)
		{
			throw new NotImplementedException ();
		}
		// MurmurHash 3
		/// <summary>
		/// MurmurHash 3.
		/// </summary>
		/// <returns>The hash.</returns>
		/// <param name="buf">Buffer.</param>
		/// <param name="length">Length.</param>
		/// <param name="seed">Seed.</param>
		public static ushort Murmur3Hash (byte[] buf, int length, uint seed = 0)
		{
			throw new NotImplementedException ();
		}

		public static ushort Murmur3Hash (string  s, uint seed = 0)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// SuperFastHash.
		/// SuperFastHash aka Hsieh Hash, License: GPL 2.0
		/// </summary>
		/// <returns>The fast hash.</returns>
		/// <param name="buf">Buffer.</param>
		/// <param name="len">Length.</param>
		public static ushort SuperFastHash (byte[] buf, int len)
		{
			throw new NotImplementedException ();
		}

		public static ushort SuperFastHash (string  s)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Null hash (shift and mask)
		/// </summary>
		/// <returns>The hash.</returns>
		/// <param name="buf">Buffer.</param>
		/// <param name="length">Length.</param>
		/// <param name="shiftbytes">Shiftbytes.</param>
		public static ushort NullHash (byte[] buf, int length, uint shiftbytes)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Wrappers for MD5 and SHA1 hashing using EVP.
		/// </summary>
		/// <returns>The d5 hash.</returns>
		/// <param name="inbuf">Inbuf.</param>
		/// <param name="in_length">In_length.</param>
		public static byte[] MD5Hash (byte[]  data)
		{
			//create new instance of md5
			MD5 md5 = MD5.Create();

			//convert the input text to array of bytes
			byte[] hashData = md5.ComputeHash(data);

			return hashData;
		}

		private static SHA1 sha1;

		public static byte[] SHA1Hash (byte[]  data)
		{
			//convert the input text to array of bytes
			byte[] hashData = sha1.ComputeHash(data);

			return hashData;
		}
	}
}

