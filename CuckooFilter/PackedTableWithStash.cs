//#define DEBUG_TABLE
using System;
using System.Text;

namespace CuckooFilter
{
	public class PackedTableWithStash : PackedTable
	{
		private ushort[] stashcode;
		private uint[] stashtag;
		private const int MAX_STASH_LEN = 220;
		private int lastStashPos = -1;

		public PackedTableWithStash (uint bits_per_tag, uint num): base(bits_per_tag, num)
		{
			stashcode = new ushort[MAX_STASH_LEN];
			stashtag = new uint[MAX_STASH_LEN];
		}
		#region Table implementation
		public override uint SizeInBytes ()
		{
			return len_; 
		}

		public override bool InsertTagToStash (uint i, uint tag)
		{
			int pos = lastStashPos + 1;
			while (pos != lastStashPos) {
				if (stashcode [pos] != 0) {
					IncrStashPosition (ref pos);
					continue;
				} else {
					ushort codeword;
					ModifyBucket(i, 3876+pos, out codeword);
					stashcode[pos] = codeword;
					stashtag[pos] = tag;
					lastStashPos = pos;
					return true;
				}
			}
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
					ushort codeword1 = (ushort)((bucketbits1) & 0x0fff);
					if (codeword1 >= PermEncoding.N_ENTS)
					{
						if (stashtag[codeword1 - PermEncoding.N_ENTS] == tag)
							return true;
						codeword1 = stashcode[codeword1 - PermEncoding.N_ENTS];
					}

					v = perm_.dec_table [codeword1];

					// the order 0 2 1 3 is not a bug
					tags1 [0] |= (uint)((v & 0x000f)); 
					tags1 [2] |= (uint)(((v >> 4) & 0x000f));
					tags1 [1] |= (uint)(((v >> 8) & 0x000f));
					tags1 [3] |= (uint)(((v >> 12) & 0x000f));


					tags2 [0] = (uint)((int)(bucketbits2 >> 8) & DIRBITSMASK);
					tags2 [1] = (uint)((int)(bucketbits2 >> 17) & DIRBITSMASK);
					tags2 [2] = (uint)((int)(bucketbits2 >> 26) & DIRBITSMASK);
					tags2 [3] = (uint)((int)(bucketbits2 >> 35) & DIRBITSMASK);
					ushort codeword2 = (ushort)((bucketbits2) & 0x0fff);
					if (codeword2 >= PermEncoding.N_ENTS)
					{
						if (stashtag[codeword2 - PermEncoding.N_ENTS] == tag)
							return true;
						codeword2 = stashcode[codeword2 - PermEncoding.N_ENTS];
					}
					v = perm_.dec_table [codeword2];
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

		public override string Info ()
		{
			StringBuilder ss = new StringBuilder ();
			ss.Append ("PackedHashtableWithStash with tag size: " + bits_per_tag + " bits");
			ss.Append ("\t4 packed bits(3 bits after compression) and " + dirbits_per_tag + " direct bits\n");
			ss.Append ("\t\tAssociativity: 4\n");
			ss.Append ("\t\tTotal # of rows: " + num_buckets + "\n");
			ss.Append ("\t\ttotal # slots: " + SizeInTags () + "\n");
			ss.Append ("\t\ttotal # stash slots: " + MAX_STASH_LEN + "\n");
			ss.Append ("\t\ttotal # stash entries: " + (lastStashPos+1) + "\n");
			return ss.ToString ();
		}

		#endregion
		public unsafe void ModifyBucket (uint i, int newCodeWord, out ushort oldCodeWord)
		{
			DPRINTF ("PackedTable.ModifyBucket {0}", i);
			DPRINTF ("dirbitsmask={0:X04}", DIRBITSMASK);
			fixed (byte* pbuckets = &buckets_[0, 0]) {
				byte* p;
				oldCodeWord = 0;

				if (bits_per_tag == 5) {
					// 1 dirbits per tag, 16 bits per bucket
					p = pbuckets + (i * 2);
					oldCodeWord = (ushort)(*((ushort*)p) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) & 0x0fff);
				} else if (bits_per_tag == 6) { 
					// 2 dirbits per tag, 20 bits per bucket
					p = pbuckets + ((20 * i) >> 3);
					oldCodeWord = (ushort)((*((short*)p)) >> (int)((i & 1) << 2) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) >> (int)((i & 1) << 2) & 0x0fff);
				} else if (bits_per_tag == 7) { 
					// 3 dirbits per tag, 24 bits per bucket
					p = pbuckets + (i << 1) + i;
					oldCodeWord = (ushort)(*((short*)p) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) & 0x0fff);
				} else if (bits_per_tag == 8) { 
					// 4 dirbits per tag, 28 bits per bucket
					p = pbuckets + ((28 * i) >> 3);
					oldCodeWord = (ushort)((*((short*)p) >> (int)((i & 1) << 2)) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) >> (int)((i & 1) << 2) & 0x0fff);
				} else if (bits_per_tag == 9) { 
					// 5 dirbits per tag, 32 bits per bucket
					p = pbuckets + (i * 4);
					oldCodeWord = (ushort)(*((ushort*)p) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) & 0x0fff);
				} else if (bits_per_tag == 13) {
					// 9 dirbits per tag,  48 bits per bucket
					p = pbuckets + (i * 6);
					oldCodeWord = (ushort)(*((ushort*)p) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) & 0x0fff);
				} else if (bits_per_tag == 17) {
					// 13 dirbits per tag, 64 bits per bucket
					p = pbuckets + (i << 3);
					oldCodeWord = (ushort)(*((ushort*)p) & 0x0fff);
					*((ushort*)p) = (ushort)((newCodeWord) & 0x0fff);
				}
			}
			DPRINTF ("PackedTable.ModifyBucket done");
		}

		private void IncrStashPosition (ref int pos)
		{
			pos ++;
			if (pos >= MAX_STASH_LEN)
				pos = 0;
		}
	}
}

