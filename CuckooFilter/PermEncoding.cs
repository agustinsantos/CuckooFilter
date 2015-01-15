#define NO_DEBUG_ENCODE

using System;

namespace CuckooFilter
{
	public class PermEncoding
	{

		/// <summary>
		/// unpack one 2-byte number to four 4-bit numbers
		/// </summary>
		/// <param name="in_">In_.</param>
		/// <param name="out_">Out_.</param>
		private static void Unpack (ushort in_, byte[] out_)
		{
			out_[0] = (byte)(in_ & 0x000f);
			out_[2] = (byte)((in_>>4) & 0x000f);
			out_[1] = (byte)((in_>>8) & 0x000f);
			out_[3] = (byte)((in_>>12) & 0x000f);
		}

		/// <summary>
		/// pack four 4-bit numbers to one 2-byte number
		/// </summary>
		/// <param name="in_">In_.</param>
		private static ushort Pack (byte[] in_)
		{
			ushort in1 = (ushort)((in_[0] & 0x000f) | ((in_[2]<<4) & 0x00f0));
			ushort in2 = (ushort)(((in_[1]<<8) & 0x0f00) | ((in_[3]<<12) & 0xf000));
			return (ushort)(in1|in2);

		}

		public PermEncoding ()
		{
			byte[] dst = new byte[4];
			ushort idx = 0;
			GenTables (0, 0, dst, ref idx);
		}

		public const uint N_ENTS = 3876;
		public ushort[] dec_table = new ushort[N_ENTS];
		public ushort[] enc_table = new ushort[1 << 16];

		public void Decode (ushort codeword, byte[] lowbits)
		{
			Unpack (dec_table [codeword], lowbits);
		}

		public ushort Encode (byte[] lowbits)
		{
#if DEBUG_ENCODE
			Console.WriteLine ("Perm.encode");
			for (int i = 0; i < 4; i++) {
				Console.WriteLine ("encode lowbits[{0}]={1}", i, lowbits [i]);
			}
			Console.WriteLine ("pack(lowbits) = {0:x}", Pack (lowbits));
			Console.WriteLine ("enc_table[{0:x}]={1}", Pack (lowbits), enc_table [Pack (lowbits)]);
#endif
			return enc_table [Pack (lowbits)];
		}

		public void  GenTables (byte base_, int k, byte[] dst, ref ushort idx)
		{
			for (byte i = base_; i < 16; i++) {
				/* for fast comparison in binary_search in little-endian machine */
				dst [k] = i;	
				if (k + 1 < 4) {
					GenTables (i, k + 1, dst, ref idx);
				} else {
					dec_table [idx] = Pack (dst);
					enc_table [Pack (dst)] = idx;
#if DEBUG_ENCODE	
					Console.WriteLine ("enc_table[0x{0:X04}]=0x{1:X04}\t0x{2:X02} 0x{3:X02} 0x{4:X02} 0x{5:X02}", 
						                  Pack (dst), idx, dst [0], dst [1], dst [2], dst [3]);
#endif
					idx ++;
				}
			}
		}


		internal static void TestPermEcoding()
		{
			//PermEncoding enc = new PermEncoding();

			//byte[] src = new byte[4]{0x03, 0x08, 0x05, 0x0A};
			byte[] src = new byte[4]{0x0F, 0x0F, 0x0F, 0x0F};
			ushort rst = PermEncoding.Pack(src);
			Console.WriteLine ("Rst=0x{0:X04}\t0x{1:X02} 0x{2:X02} 0x{3:X02} 0x{4:X02}", rst, src [0], src [1], src [2], src [3]);

			byte[] dst = new byte[4];
			PermEncoding.Unpack(rst, dst);
			Console.WriteLine ("Rst=0x{0:X04}\t0x{1:X02} 0x{2:X02} 0x{3:X02} 0x{4:X02}", rst, dst [0], dst [1], dst [2], dst [3]);


		}

	}
}

