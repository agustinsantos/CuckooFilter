using System;
using System.Text;
using System.Diagnostics;
using System.Collections.Specialized;

namespace CuckooFilter
{
	/// <summary>
	/// the most naive table implementation: one huge bit array.
	/// </summary>
	public class SingleTable : Table
	{
		const uint tags_per_bucket = 4;
		readonly uint bytes_per_bucket;
		byte[,] buckets_;
		public readonly uint TAGMASK;

		public SingleTable (uint bits_per_tag, uint num): base(bits_per_tag, num)
		{
			if (bits_per_tag > 32)
				throw new ArgumentException ("bits_per_tag should be <= 32");

			this.bytes_per_bucket = (bits_per_tag * tags_per_bucket + 7) >> 3;
			this.TAGMASK = (uint)((1 << (int)bits_per_tag) - 1);
			buckets_ = new byte[NumBuckets, this.bytes_per_bucket];
			CleanupTags ();
		}

		public void CleanupTags ()
		{
			for (int i= 0; i < NumBuckets; i++)
				for (int j= 0; j < this.bytes_per_bucket; j++)
					buckets_ [i, j] = 0;
		}

		public override uint SizeInBytes ()
		{
			return bytes_per_bucket * NumBuckets;
		}

		public override uint SizeInTags ()
		{
			return tags_per_bucket * NumBuckets;
		}

		public override string Info ()
		{
			StringBuilder ss = new StringBuilder ();
			ss.Append ("SingleHashtable with tag size: " + BitsPerTag + " bits \n");
			ss.Append ("\t\tAssociativity: " + tags_per_bucket + "\n");
			ss.Append ("\t\tTotal # of rows: " + NumBuckets + "\n");
			ss.Append ("\t\tTotal # slots: " + SizeInTags () + "\n");
			return ss.ToString ();
		}

		/// <summary>
		/// read tag from pos(i,j)
		/// </summary>
		/// <returns>The tag.</returns>
		/// <param name="i">The index.</param>
		/// <param name="j">J.</param>
		public unsafe uint ReadTag (uint i, uint j)
		{
			/* following code only works for little-endian */
			Debug.Assert(BitConverter.IsLittleEndian);
			fixed (byte* ptr = &buckets_[i, 0]) {
				byte* p = ptr;
				uint tag = 0;

				if (bits_per_tag == 2) {
					tag = (uint)(*((byte*)p) >> (byte)(j * 2));
				} else if (bits_per_tag == 4) {
					p += (j >> 1);
					tag = (uint)(*((byte*)p) >> (byte)((j & 1) << 2));
				} else if (bits_per_tag == 8) {
					p += j;
					tag = *((byte*)p);
				} else if (bits_per_tag == 12) {
					p += j + (j >> 1);
					tag = (uint)(*((ushort*)p) >> (ushort)((j & 1) << 2));
				} else if (bits_per_tag == 16) {
					p += (j << 1);
					tag = *((ushort*)p);
				} else if (bits_per_tag == 32) {
					tag = ((uint*)p) [j];
				}
				return tag & TAGMASK;
			}
		}

		/// <summary>
		/// write tag to pos(i,j)
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="j">J.</param>
		/// <param name="t">T.</param>
		public unsafe void  WriteTag (uint i, uint j, uint t)
		{
			fixed (byte* ptr = &buckets_[i, 0]) {
				byte* p = ptr;
				uint tag = t & TAGMASK;
				/* following code only works for little-endian */
				if (bits_per_tag == 2) {
					*((byte*)p) |= (byte)(tag << (byte)(2 * j));
				} else if (bits_per_tag == 4) {
					p += (j >> 1);
					if ((j & 1) == 0) {
						*((byte*)p) &= 0xf0;
						*((byte*)p) |= (byte)tag;
					} else {
						*((byte*)p) &= 0x0f;
						*((byte*)p) |= (byte)(tag << 4);
					}
				} else if (bits_per_tag == 8) {
					((byte*)p) [j] = (byte)tag;
				} else if (bits_per_tag == 12) {
					p += (j + (j >> 1));
					if ((j & 1) == 0) {
						((ushort*)p) [0] &= 0xf000;
						((ushort*)p) [0] |= (ushort)tag;
						//ushort tagdebug = ((ushort*)p) [0];
					} else {
						((ushort*)p) [0] &= 0x000f;
						((ushort*)p) [0] |= (ushort)(tag << 4);
					}
				} else if (bits_per_tag == 16) {
					((ushort*)p) [j] = (ushort)tag;
				} else if (bits_per_tag == 32) {
					((uint*)p) [j] = tag;
				}

				return;
			}
		}

		public unsafe override bool FindTagInBuckets (uint i1,
		                                              uint i2,
		                                              uint tag)
		{
			fixed (byte* p1 = &buckets_[i1, 0]) {
				fixed (byte* p2 = &buckets_[i2, 0]) {
					ulong v1 = *((ulong*)p1);
					ulong v2 = *((ulong*)p2);

					// caution: unaligned access & assuming little endian
					if (bits_per_tag == 4 && tags_per_bucket == 4) {
						return Hasvalue4 (v1, tag) || Hasvalue4 (v2, tag);
					} else if (bits_per_tag == 8 && tags_per_bucket == 4) {
						return Hasvalue8 (v1, tag) || Hasvalue8 (v2, tag);
					} else if (bits_per_tag == 12 && tags_per_bucket == 4) {
						return Hasvalue12 (v1, tag) || Hasvalue12 (v2, tag);
					} else if (bits_per_tag == 16 && tags_per_bucket == 4) {
						return Hasvalue16 (v1, tag) || Hasvalue16 (v2, tag);
					} else {
						for (uint j = 0; j < tags_per_bucket; j++) {
							if ((ReadTag (i1, j) == tag) || (ReadTag (i2, j) == tag))
								return true;
						}
						return false;
					}
				}
			}
		}

		public unsafe bool  FindTagInBucket (uint i, uint tag)
		{
			fixed (byte* p = &buckets_[i, 0]) {
				// caution: unaligned access & assuming little endian
				if (bits_per_tag == 4 && tags_per_bucket == 4) {
					ulong v = *(ulong*)p; // uint16_t may suffice
					return Hasvalue4 (v, tag);
				} else if (bits_per_tag == 8 && tags_per_bucket == 4) {
					ulong v = *(ulong*)p; // uint may suffice
					return Hasvalue8 (v, tag);
				} else if (bits_per_tag == 12 && tags_per_bucket == 4) {
					ulong v = *(ulong*)p;
					return Hasvalue12 (v, tag);
				} else if (bits_per_tag == 16 && tags_per_bucket == 4) {
					ulong v = *(ulong*)p;
					return Hasvalue16 (v, tag);
				} else {
					for (uint j = 0; j < tags_per_bucket; j++) {
						if (ReadTag (i, j) == tag)
							return true;
					}
					return false;
				}
			}
		}

		public override bool DeleteTagFromBucket (uint i, uint tag)
		{
			for (uint j = 0; j < tags_per_bucket; j++) {
				if (ReadTag (i, j) == tag) {
					Debug.Assert (FindTagInBucket (i, tag) == true);
					WriteTag (i, j, 0);
					return true;
				}
			}
			return false;
		}

		private static Random rand = new Random();
		private int cnt = 0;

		public override bool InsertTagToBucket (uint i, uint tag, bool kickout, out uint oldtag)
		{
			oldtag = 0;
			for (uint j = 0; j < tags_per_bucket; j++) {
				if (ReadTag (i, j) == 0) {
					WriteTag (i, j, tag);
					return true;
				}
			}
			if (kickout) {
				cnt++;
				uint r = (uint)(rand.Next () % tags_per_bucket);
				//uint r = Lfsr113Bits () % tags_per_bucket;
				//Debug.WriteLine(cnt + ", " + r);
				oldtag = ReadTag (i, r);
				WriteTag (i, r, tag);
			}
			return false;
		}

		public  uint NumTagsInBucket (uint i)
		{
			uint num = 0;
			for (uint j = 0; j < tags_per_bucket; j++) {
				if (ReadTag (i, j) != 0) {
					num ++;
				}
			}
			return num;
		}

		private static bool  Haszero4 (ulong x)
		{
			return (((x) - 0x1111UL) & (~(x)) & 0x8888UL) != 0;
		}

		private static bool Hasvalue4 (ulong x, uint n)
		{
			return (Haszero4 ((x) ^ (0x1111UL * (n))));
		}

		private static bool Haszero8 (ulong x)
		{
			return (((x) - 0x01010101UL) & (~(x)) & 0x80808080UL) != 0;
		}

		private static bool Hasvalue8 (ulong x, uint n)
		{
			return (Haszero8 ((x) ^ (0x01010101UL * (n))));
		}

		private static bool Haszero12 (ulong x)
		{
			return  (((x) - 0x001001001001UL) & (~(x)) & 0x800800800800UL) != 0;
		}

		private static bool Hasvalue12 (ulong x, uint n)
		{
			return (Haszero12 ((x) ^ (0x001001001001UL * (n))));
		}

		private static bool Haszero16 (ulong x)
		{
			return (((x) - 0x0001000100010001UL) & (~(x)) & 0x8000800080008000UL) != 0;
		}

		private static bool Hasvalue16 (ulong x, uint n)
		{
			return (Haszero16 ((x) ^ (0x0001000100010001UL * (n))));
		}

		private uint z1 = 12345, z2 = 12345, z3 = 12345, z4 = 12345;

		private uint Lfsr113Bits ()
		{
			uint b;
			b  = ((z1 << 6) ^ z1) >> 13;
			z1 = ((z1 & 4294967294U) << 18) ^ b;
			b  = ((z2 << 2) ^ z2) >> 27; 
			z2 = ((z2 & 4294967288U) << 2) ^ b;
			b  = ((z3 << 13) ^ z3) >> 21;
			z3 = ((z3 & 4294967280U) << 7) ^ b;
			b  = ((z4 << 3) ^ z4) >> 12;
			z4 = ((z4 & 4294967168U) << 13) ^ b;
			return (z1 ^ z2 ^ z3 ^ z4);
		}
	}
}

