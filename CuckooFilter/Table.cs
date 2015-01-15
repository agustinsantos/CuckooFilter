using System;

namespace CuckooFilter
{
	public abstract class Table
	{
		public Table(uint bits_per_item, uint num )
		{
			this.num_buckets = num;
			this.bits_per_tag = bits_per_item;
		}

		protected readonly uint num_buckets;
		protected readonly uint bits_per_tag;

		public uint BitsPerTag {
			get {
				return bits_per_tag;
			}
		}

		public uint NumBuckets {
			get {
				return num_buckets;
			}
		}

		public abstract uint SizeInBytes ();

		public abstract uint SizeInTags ();

		public abstract bool InsertTagToStash (uint i, uint tag);

		public abstract bool InsertTagToBucket (uint i, uint tag, bool kickout, out uint oldtag);

		public abstract bool FindTagInBuckets (uint i1, uint i2, uint tag);

		public abstract bool DeleteTagFromBucket (uint i, uint tag);
	
		public abstract string Info();
	}
}

