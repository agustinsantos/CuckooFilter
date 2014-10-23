using System;

namespace CuckooFilter
{
	public class PackedTable : Table
	{
		public PackedTable (uint bits_per_tag, uint num): base(bits_per_tag, num)
		{
 
		}

		#region Table implementation


		public override uint SizeInBytes ()
		{
			throw new NotImplementedException ();
		}

		public override uint SizeInTags ()
		{
			throw new NotImplementedException ();
		}

		public override bool InsertTagToBucket (uint i, uint tag, bool kickout, out uint oldtag)
		{
			throw new NotImplementedException ();
		}

		public override  bool FindTagInBuckets (uint i1, uint i2, uint tag)
		{
			throw new NotImplementedException ();
		}

		public override  bool DeleteTagFromBucket (uint i, uint tag)
		{
			throw new NotImplementedException ();
		}

		public override string Info ()
		{
			throw new NotImplementedException ();
		}
		#endregion
	}
}

