using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CuckooFilter
{
	// status returned by a cuckoo filter operation
	public enum Status
	{
		Ok = 0,
		NotFound = 1,
		NotEnoughSpace = 2,
		NotSupported = 3,
	}

	/// <summary>
	/// A cuckoo filter class exposes a Bloomier filter interface,
	/// providing methods of Add, Delete, Contain. It takes three
	/// template parameters:
	///   ItemType:  the type of item you want to insert
	///   bits_per_item: how many bits each item is hashed into
	///   TableType: the storage of table, SingleTable by default, and
	/// PackedTable to enable semi-sorting 
	/// </summary>
	public class CuckooFilter<ItemType>
		where ItemType: struct
	{
		// maximum number of cuckoo kicks before claiming failure
		public const uint kMaxCuckooCount = 500;
		// Storage of items
		//TableType<bits_per_item> table_;
		private Table table_;
		// Number of items stored
		private uint num_items_ = 0;
		private uint bits_per_item;

		struct VictimCache
		{
			public uint index;
			public uint tag;
			public bool used;
		}

		private VictimCache victim_;
		private IBytesProvider<ItemType> bytesProvider;

		public CuckooFilter (uint max_num_keys, uint bits_per_item, bool usePackedTable = false)
		{
			bytesProvider = BytesProvider<ItemType>.Default;
			this.bits_per_item = bits_per_item;

			uint assoc = 4;
			uint num_buckets = PrimitiveHelpers.Upperpower2 (max_num_keys / assoc);
			double frac = (double)max_num_keys / num_buckets / assoc;
			if (frac > 0.96) {
				num_buckets <<= 1;
			}
			victim_.used = false;
			if (usePackedTable)
				table_ = new PackedTable (bits_per_item, num_buckets);
			else
				table_ = new SingleTable (bits_per_item, num_buckets);
		}
		// Add an item to the filter.
		public Status Add (ItemType item)
		{
			uint i;
			uint tag;

			if (victim_.used) {
				return Status.NotEnoughSpace;
			}

			GenerateIndexTagHash (item, out i, out tag);
			return AddImpl (i, tag);
		}
		// Report if the item is inserted, with false positive rate.
		public Status Contain (ItemType key)
		{
			bool found = false;
			uint i1, i2;
			uint tag;

			GenerateIndexTagHash (key, out i1, out tag);
			i2 = AltIndex (i1, tag);

			Debug.Assert (i1 == AltIndex (i2, tag));

			found = victim_.used && (tag == victim_.tag) && 
				(i1 == victim_.index || i2 == victim_.index);

			if (found || table_.FindTagInBuckets (i1, i2, tag)) {
				return Status.Ok;
			} else {
				return Status.NotFound;
			}
		}
		// Delete an key from the filter
		public Status Delete (ItemType key)
		{
			uint i1, i2;
			uint tag;

			GenerateIndexTagHash (key, out i1, out tag);
			i2 = AltIndex (i1, tag);

			if (table_.DeleteTagFromBucket (i1, tag)) {
				num_items_--;
				goto TryEliminateVictim;
			} else if (table_.DeleteTagFromBucket (i2, tag)) {
				num_items_--;
				goto TryEliminateVictim;
			} else if (victim_.used && tag == victim_.tag &&
				(i1 == victim_.index || i2 == victim_.index)) {
				//num_items_--;
				victim_.used = false;
				return Status.Ok;
			} else {
				return Status.NotFound;
			}
			TryEliminateVictim:
			if (victim_.used) {
				victim_.used = false;
				uint i = victim_.index;
				uint tag2 = victim_.tag;
				AddImpl (i, tag2);
			}
			return Status.Ok;
		}

		/* methods for providing stats  */
		// summary infomation
		public string Info ()
		{
			StringBuilder ss = new StringBuilder();
			ss.Append("CuckooFilter Status:\n");
			ss.Append("\t\t" + table_.Info() + "\n");
			ss.Append("\t\tKeys stored: " + Size() + "\n");
			ss.Append("\t\tLoad facotr: " + LoadFactor() + "\n");
			ss.Append("\t\tHashtable size: " + (table_.SizeInBytes() >> 10));
			ss.Append(" KB\n");
			if (Size() > 0) {
				ss.Append("\t\tbit/key:   " + BitsPerItem() + "\n");
			} else {
				ss.Append("\t\tbit/key:   N/A\n");
			}
			return ss.ToString();
		}

		// number of current inserted items;
		public uint Size ()
		{
			return num_items_;
		}
		// size of the filter in bytes.
		public uint SizeInBytes ()
		{
			return table_.SizeInBytes ();
		}

		private uint IndexHash (uint hv)
		{
			return hv % table_.NumBuckets;
		}

		private uint TagHash (uint hv)
		{
			uint tag;
			tag = (uint)(hv & ((1 << (int)bits_per_item) - 1));
			tag += ((tag == 0) ? 1u : 0u);
			return tag;
		}

		private  void GenerateIndexTagHash (ItemType item,
		                                    out uint index,
		                                    out uint tag)
		{
			byte[] bytes = bytesProvider.GetBytes (item);
			byte[] hashed_key = HashUtils.SHA1Hash (bytes);
			//ulong hv = *((ulong*)hashed_key);

			//index = IndexHash ((uint)(hv >> 32));
			//tag = TagHash ((uint)(hv & 0xFFFFFFFF));
			index = IndexHash (hashed_key.GetUInt32 (4));
			tag = TagHash (hashed_key.GetUInt32 (0));
		}

		private uint AltIndex (uint index, uint tag)
		{
			// NOTE(binfan): originally we use:
			// index ^ HashUtil::BobHash((const void*) (&tag), 4)) & table_->INDEXMASK;
			// now doing a quick-n-dirty way:
			// 0x5bd1e995 is the hash constant from MurmurHash2
			return IndexHash ((uint)(index ^ (tag * 0x5bd1e995)));
		}

		private Status AddImpl (uint i, uint tag)
		{
			uint curindex = i;
			uint curtag = tag;
			uint oldtag;

			for (uint count = 0; count < kMaxCuckooCount; count++) {
				bool kickout = count > 0;
				oldtag = 0;
				if (table_.InsertTagToBucket (curindex, curtag, kickout, out oldtag)) {
					num_items_++;
					return Status.Ok;
				}
				if (kickout) {
					curtag = oldtag;
				}
				curindex = AltIndex (curindex, curtag);
			}

			victim_.index = curindex;
			victim_.tag = curtag;
			victim_.used = true;
			return Status.Ok;
		}
		// load factor is the fraction of occupancy
		private double LoadFactor ()
		{
			return 1.0 * Size () / table_.SizeInTags ();
		}

		private double BitsPerItem ()
		{
			return 8.0 * table_.SizeInBytes () / Size ();
		}
	}
}

