//#define DEBUG_TABLE
using System;
using System.Text;

namespace CuckooFilter
{
	public class PackedTable : Table
	{
		protected static Random rand = new Random (3);
		protected readonly uint dirbits_per_tag;
		protected readonly uint bits_per_bucket;
		protected readonly uint bytes_per_bucket;
		// using a pointer adds one more indirection
		protected uint len_;
		protected byte[,] buckets_;
		protected PermEncoding perm_ = new PermEncoding ();
		public readonly uint TAGMASK;
		public readonly int DIRBITSMASK;

		public PackedTable (uint bits_per_tag, uint num): base(bits_per_tag, num)
		{
			dirbits_per_tag = bits_per_tag - 4;
			bits_per_bucket = (3 + dirbits_per_tag) * 4;
			bytes_per_bucket = (bits_per_bucket + 7) >> 3;
			TAGMASK = (uint)((1 << (int)bits_per_tag) - 1);
			DIRBITSMASK = (int)(((1UL << (int)dirbits_per_tag) - 1) << 4);

			// NOTE(binfan): use extra bytes to avoid overrun as we
			// always read a uint64
			len_ = bytes_per_bucket * num_buckets + 7;
			buckets_ = new byte[NumBuckets + 1, this.bytes_per_bucket];
			CleanupTags ();
		}
		#region Table implementation
		public override uint SizeInBytes ()
		{
			return len_; 
		}

		public override uint SizeInTags ()
		{
			return 4 * num_buckets; 
		}

		public override bool InsertTagToStash (uint i, uint tag)
		{
			throw new NotSupportedException("Stash is not supported in SingleTable");
		}

		public override bool InsertTagToBucket (uint i, uint tag, bool kickout, out uint oldtag)
		{
			oldtag = 0;
			DPRINTF ("PackedTable.InsertTagToBucket {0}", i);

			uint[] tags = new uint[4];
			DPRINTF ("PackedTable.InsertTagToBucket read bucket to tags");
			ReadBucket (i, tags);
#if DEBUG_TABLE
			PrintTags (tags);
			PrintBucket (i);
#endif
			for (uint j = 0; j < 4; j++) {
				if (tags [j] == 0) {
					DPRINTF ("PackedTable.InsertTagToBucket slot {0} is empty", j);

					tags [j] = tag;
					WriteBucket (i, tags);
#if DEBUG_TABLE
					PrintBucket (i);
					ReadBucket (i, tags);
#endif
					DPRINTF ("PackedTable.InsertTagToBucket Ok");
					return true;
				}
			}
			if (kickout) {

				uint r = (uint)(rand.Next () & 3);
				DPRINTF ("PackedTable.InsertTagToBucket, let's kick out a random slot {0}", r);
				//PrintBucket(i);

				oldtag = tags [r];
				tags [r] = tag;
				WriteBucket (i, tags);
#if DEBUG_TABLE
				PrintTags (tags);
#endif
			}
			DPRINTF ("PackedTable.InsertTagToBucket, insert failed");
			return false;
		}

		public unsafe override  bool FindTagInBuckets (uint i1, uint i2, uint tag)
		{

			//            DPRINTF(DEBUG_TABLE, "PackedTable.FindTagInBucket %zu\n", i);
			uint[] tags1 = new uint[4];
			uint[] tags2 = new uint[4];

			// disable for now
			// _mm_prefetch( buckets_ + (i1 * bits_per_bucket) / 8,  _MM_HINT_NTA);
			// _mm_prefetch( buckets_ + (i2 * bits_per_bucket) / 8,  _MM_HINT_NTA);

			//ReadBucket(i1, tags1);
			//ReadBucket(i2, tags2);
			fixed (byte* p1 = &buckets_[i1, 0]) {
				fixed (byte* p2 = &buckets_[i2, 0]) {
					ushort v;
					ulong bucketbits1 = *((ulong*)(p1));
					ulong bucketbits2 = *((ulong*)(p2));


					tags1 [0] = (uint)((int)(bucketbits1 >> 8) & DIRBITSMASK);
					tags1 [1] = (uint)((int)(bucketbits1 >> 17) & DIRBITSMASK);
					tags1 [2] = (uint)((int)(bucketbits1 >> 26) & DIRBITSMASK);
					tags1 [3] = (uint)((int)(bucketbits1 >> 35) & DIRBITSMASK);
					v = perm_.dec_table [(bucketbits1) & 0x0fff];

					// the order 0 2 1 3 is not a bug
					tags1 [0] |= (uint)((v & 0x000f)); 
					tags1 [2] |= (uint)(((v >> 4) & 0x000f));
					tags1 [1] |= (uint)(((v >> 8) & 0x000f));
					tags1 [3] |= (uint)(((v >> 12) & 0x000f));


					tags2 [0] = (uint)((int)(bucketbits2 >> 8) & DIRBITSMASK);
					tags2 [1] = (uint)((int)(bucketbits2 >> 17) & DIRBITSMASK);
					tags2 [2] = (uint)((int)(bucketbits2 >> 26) & DIRBITSMASK);
					tags2 [3] = (uint)((int)(bucketbits2 >> 35) & DIRBITSMASK);
					v = perm_.dec_table [(bucketbits2) & 0x0fff];
					tags2 [0] |= (uint)((v & 0x000f)); 
					tags2 [2] |= (uint)(((v >> 4) & 0x000f));
					tags2 [1] |= (uint)(((v >> 8) & 0x000f));
					tags2 [3] |= (uint)(((v >> 12) & 0x000f));

					bool ret1 = ((tags1 [0] == tag) || (tags1 [1] == tag) || (tags1 [2] == tag) || (tags1 [3] == tag));
					bool ret2 = ((tags2 [0] == tag) || (tags2 [1] == tag) || (tags2 [2] == tag) || (tags2 [3] == tag));

					return ret1 || ret2;
				}
			}
		}

		public override  bool DeleteTagFromBucket (uint i, uint tag)
		{
			uint[] tags = new uint[4];
			ReadBucket (i, tags);
#if DEBUG_TABLE
			PrintTags (tags);
#endif
			for (uint j = 0; j < 4; j++) {
				if (tags [j] == tag) {
					tags [j] = 0;
					WriteBucket (i, tags);
					return true;
				}
			}
			return false;
		}

		public override string Info ()
		{
			StringBuilder ss = new StringBuilder ();
			ss.Append ("PackedHashtable with tag size: " + bits_per_tag + " bits");
			ss.Append ("\t4 packed bits(3 bits after compression) and " + dirbits_per_tag + " direct bits\n");
			ss.Append ("\t\tAssociativity: 4\n");
			ss.Append ("\t\tTotal # of rows: " + num_buckets + "\n");
			ss.Append ("\t\ttotal # slots: " + SizeInTags () + "\n");
			return ss.ToString ();
		}

		#endregion
		public void CleanupTags ()
		{ 
			/* Not needed in C#. Matrix is already initialized 
			for (int i = 0; i< buckets_.GetLength(0); i++)
				for (int j = 0; j< buckets_.GetLength(1); j++)
					buckets_ [i, j] = 0; 
		    */
		}

		public unsafe void PrintBucket (uint i)
		{
			DPRINTF ("PackedTable.PrintBucket {0}", i);

			byte[] b = new byte[bytes_per_bucket + 1];
			for (int j = 0; i < bits_per_bucket; j++)
				b [j] = buckets_ [i, j];
			Console.WriteLine ("\tbucketbits  =" + StringUtils.ByteArrayToHexString (b));

			uint[] tags = new uint[4];

			ReadBucket (i, tags);
			PrintTags (tags);
			DPRINTF ("PackedTable.PrintBucket done");
		}

		public void PrintTags (uint[] tags)
		{
			DPRINTF ("PackedTable.PrintTags");
			byte[] lowbits = new byte[4];
			uint[] dirbits = new uint[4];
			for (int j = 0; j < 4; j ++) {
				lowbits [j] = (byte)(tags [j] & 0x0f);
				dirbits [j] = (uint)((tags [j] & DIRBITSMASK) >> 4);
			}
			ushort codeword = perm_.Encode (lowbits);
			Console.WriteLine ("\tcodeword  =" + StringUtils.ByteArrayToHexString (BitConverter.GetBytes (codeword)));
			for (int j = 0; j < 4; j ++) {
				Console.Write ("\ttag[" + j + "]: " + StringUtils.ByteArrayToHexString (BitConverter.GetBytes (tags [j])));
				Console.WriteLine (" lowbits=" + lowbits [j].ToString ("X02") + " dirbits=" + StringUtils.ByteArrayToHexString (BitConverter.GetBytes (dirbits [j]), (int)(dirbits_per_tag / 8 + 1)));
			}
			DPRINTF ("PackedTable.PrintTags done");
		}

		public void Comparator (ref uint a, ref uint b)
		{
			if ((a & 0x0f) > (b & 0x0f)) {
				uint tmp = a;
				a = b;
				b = tmp;
			}
		}

		public void SortTags (uint[] tags)
		{
			Comparator (ref tags [0], ref tags [2]);
			Comparator (ref tags [1], ref tags [3]);
			Comparator (ref tags [0], ref tags [1]);
			Comparator (ref tags [2], ref tags [3]);
			Comparator (ref tags [1], ref tags [2]);
		}

		/// <summary>
		/// Read and decode the bucket i, pass the 4 decoded tags to the 2nd arg
		/// bucket bits = 12 codeword bits + dir bits of tag1 + dir bits of tag2 ...
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="tags">Tags.</param>
		public unsafe void ReadBucket (uint i, uint[] tags)
		{
			DPRINTF ("PackedTable.ReadBucket {0}", i);
			DPRINTF ("dirbitsmask={0:X04}", DIRBITSMASK);
			fixed (byte* pbuckets = &buckets_[0, 0]) {
				byte* p;
				//const char* p; // =  buckets_ + ((bits_per_bucket * i) >> 3);
				ushort codeword = 0;
				byte[] lowbits = new byte[4];

				if (bits_per_tag == 5) {
					// 1 dirbits per tag, 16 bits per bucket
					p = pbuckets + (i * 2);
					ushort bucketbits = *((ushort*)p);
					codeword = (ushort)(bucketbits & 0x0fff);
					tags [0] = (uint)((bucketbits >> 8) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> 9) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> 10) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> 11) & DIRBITSMASK);
				} else if (bits_per_tag == 6) { 
					// 2 dirbits per tag, 20 bits per bucket
					p = pbuckets + ((20 * i) >> 3);
					int bucketbits = *((int*)p);
					codeword = (ushort)((*((short*)p)) >> (int)((i & 1) << 2) & 0x0fff);
					tags [0] = (uint)((bucketbits >> (int)(8 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> (int)(10 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> (int)(12 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> (int)(14 + ((i & 1) << 2))) & DIRBITSMASK);
				} else if (bits_per_tag == 7) { 
					// 3 dirbits per tag, 24 bits per bucket
					p = pbuckets + (i << 1) + i;
					uint bucketbits = *((uint*)p);
					codeword = (ushort)(*((short*)p) & 0x0fff);
					tags [0] = (uint)((bucketbits >> 8) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> 11) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> 14) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> 17) & DIRBITSMASK);
				} else if (bits_per_tag == 8) { 
					// 4 dirbits per tag, 28 bits per bucket
					p = pbuckets + ((28 * i) >> 3);
					int bucketbits = *((int*)p);
					codeword = (ushort)((*((short*)p) >> (int)((i & 1) << 2)) & 0x0fff);
					tags [0] = (uint)((bucketbits >> (int)(8 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> (int)(12 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> (int)(16 + ((i & 1) << 2))) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> (int)(20 + ((i & 1) << 2))) & DIRBITSMASK);
				} else if (bits_per_tag == 9) { 
					// 5 dirbits per tag, 32 bits per bucket
					p = pbuckets + (i * 4);
					uint bucketbits = *((uint*)p);
					codeword = (ushort)(*((ushort*)p) & 0x0fff);
					tags [0] = (uint)((bucketbits >> 8) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> 13) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> 18) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> 23) & DIRBITSMASK);
				} else if (bits_per_tag == 13) {
					// 9 dirbits per tag,  48 bits per bucket
					p = pbuckets + (i * 6);
					long bucketbits = *((long*)p);
					codeword = (ushort)(*((ushort*)p) & 0x0fff);
					tags [0] = (uint)((bucketbits >> 8) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> 17) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> 26) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> 35) & DIRBITSMASK);
				} else if (bits_per_tag == 17) {
					// 13 dirbits per tag, 64 bits per bucket
					p = pbuckets + (i << 3);
					long bucketbits = *((long*)p);
					codeword = (ushort)(*((ushort*)p) & 0x0fff);
					tags [0] = (uint)((bucketbits >> 8) & DIRBITSMASK);
					tags [1] = (uint)((bucketbits >> 21) & DIRBITSMASK);
					tags [2] = (uint)((bucketbits >> 34) & DIRBITSMASK);
					tags [3] = (uint)((bucketbits >> 47) & DIRBITSMASK);
				}

				/* codeword is the lowest 12 bits in the bucket */
				ushort v = perm_.dec_table [codeword];
				lowbits [0] = (byte)(v & 0x000f);
				lowbits [2] = (byte)((v >> 4) & 0x000f);
				lowbits [1] = (byte)((v >> 8) & 0x000f);
				lowbits [3] = (byte)((v >> 12) & 0x000f);

				tags [0] |= lowbits [0]; 
				tags [1] |= lowbits [1]; 
				tags [2] |= lowbits [2]; 
				tags [3] |= lowbits [3]; 

#if DEBUG_TABLE
				PrintTags (tags);
#endif
			}
			DPRINTF ("PackedTable.ReadBucket done");
		}

		/// <summary>
		/// Tag = 4 low bits + x high bits
		/// L L L L H H H H ...
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="tags">Tags.</param>
		/// <param name="sort">If set to <c>true</c> sort.</param>
		public unsafe void WriteBucket (uint i, uint[] tags, bool sort = true)
		{
			DPRINTF ("PackedTable.WriteBucket {0}", i);
			/* first sort the tags in increasing order is arg sort = true*/
			if (sort) {
				DPRINTF ("Sort tags");
				SortTags (tags);
			}
#if DEBUG_TABLE
			PrintTags (tags);
#endif

			/* put in direct bits for each tag*/

			byte[] lowbits = new byte[4];
			uint[] highbits = new uint[4];

			lowbits [0] = (byte)(tags [0] & 0x0f);
			lowbits [1] = (byte)(tags [1] & 0x0f);
			lowbits [2] = (byte)(tags [2] & 0x0f);
			lowbits [3] = (byte)(tags [3] & 0x0f);

			highbits [0] = tags [0] & 0xfffffff0;
			highbits [1] = tags [1] & 0xfffffff0;
			highbits [2] = tags [2] & 0xfffffff0;
			highbits [3] = tags [3] & 0xfffffff0;

			// note that :  tags[j] = lowbits[j] | highbits[j]

			ushort codeword = perm_.Encode (lowbits);
			DPRINTF ("codeword={0}", StringUtils.ByteArrayToHexString (BitConverter.GetBytes (codeword)));
			/* write out the bucketbits to its place*/
			fixed (byte* pbuckets = &buckets_[0, 0]) {
				byte* p = pbuckets + ((bits_per_bucket * i) >> 3);
				//TODO DPRINTF ("original bucketbits={0}", StringUtils.ByteArrayToHexString ((char*)p, 8));

				if (bits_per_bucket == 16) {
					// 1 dirbits per tag
					*((ushort*)p) = (ushort)(codeword | (highbits [0] << 8) | (highbits [1] << 9) | (highbits [2] << 10) | (highbits [3] << 11));
				} else if (bits_per_bucket == 20) {
					// 2 dirbits per tag
					if ((i & 0x0001) == 0) {
						*((uint*)p) &= 0xfff00000;
						*((uint*)p) |= 
						codeword | (highbits [0] << 8) | (highbits [1] << 10) | (highbits [2] << 12) | (highbits [3] << 14);
					} else {
						*((uint*)p) &= 0xff00000f;
						*((uint*)p) |= (uint)((int)(codeword << 4) | (int)(highbits [0] << 12) | (int)(highbits [1] << 14) | (int)(highbits [2] << 16) | (int)(highbits [3] << 18));
					}
				} else if (bits_per_bucket == 24) {
					// 3 dirbits per tag
					*((uint*)p) &= 0xff000000;
					*((uint*)p) |=
					codeword | (highbits [0] << 8) | (highbits [1] << 11) | (highbits [2] << 14) | (highbits [3] << 17);
				} else if (bits_per_bucket == 28) {
					// 4 dirbits per tag
					if ((i & 0x0001) == 0) {
						*((uint*)p) &= 0xf0000000;
						*((uint*)p) |= 
						codeword | (highbits [0] << 8) | (highbits [1] << 12) | (highbits [2] << 16) | (highbits [3] << 20);
					} else {
						*((uint*)p) &= 0x0000000f;
						*((uint*)p) |= (uint)((codeword << 4) | ((int)highbits [0] << 12) | (int)(highbits [1] << 16) | (int)(highbits [2] << 20) | (int)(highbits [3] << 24));
					}
				} else if (bits_per_bucket == 32) {
					// 5 dirbits per tag
					*((uint*)p) = 
					codeword | (highbits [0] << 8) | (highbits [1] << 13) | (highbits [2] << 18) | (highbits [3] << 23);
					//TODO DPRINTF (" new bucketbits={0}", StringUtils.ByteArrayToHexString ((char*)p, 4));
				} else if (bits_per_bucket == 48) {
					// 9 dirbits per tag
					*((ulong*)p) &= 0xffff000000000000UL;
					*((ulong*)p) |= 
					codeword | 
						((ulong)highbits [0] << 8) | 
						((ulong)highbits [1] << 17) | 
						((ulong)highbits [2] << 26) | 
						((ulong)highbits [3] << 35);
					//TODO DPRINTF (" new bucketbits={0}", StringUtils.ByteArrayToHexString ((char*)p, 4));

				} else if (bits_per_bucket == 64) {
					// 13 dirbits per tag
					*((ulong*)p) = 
					codeword | 
						((ulong)highbits [0] << 8) | 
						((ulong)highbits [1] << 21) | 
						((ulong)highbits [2] << 34) | 
						((ulong)highbits [3] << 47);
				}
			}
			DPRINTF ("PackedTable.WriteBucket done");
		}

		public bool FindTagInBucket (uint i, uint tag)
		{
			DPRINTF ("PackedTable.FindTagInBucket {0}", i);
			uint[] tags = new uint[4];
			ReadBucket (i, tags);
#if DEBUG_TABLE
			PrintTags (tags);
#endif

			bool ret = ((tags [0] == tag) || (tags [1] == tag) || (tags [2] == tag) || (tags [3] == tag));
			DPRINTF ("PackedTable.FindTagInBucket {0}", ret);
			return ret;

		}

		protected void DPRINTF (string str, params object[] args)
		{
#if DEBUG_DPRINTF
			Console.WriteLine (str, args);
#endif
		}
	}
}

