using System;
using System.Diagnostics;

namespace CuckooFilter
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Stopwatch sw = new Stopwatch();

			sw.Start();
			uint total_items = 1000000;

			// Create a cuckoo filter where each item is of type uint and
			// use 12 bits for each item:
			//    CuckooFilter<uint> filter = new CuckooFilter<uint>(total_items, 12);
			// To enable semi-sorting, define the storage of cuckoo filter to be
			// PackedTable, accepting keys of uint type and making 13 bits
			// for each key:
			//   CuckooFilter<uint> filter = CuckooFilter<uint> (total_items, 13, true);
			CuckooFilter<ulong> filter = new CuckooFilter<ulong> (total_items, 12);

			// Insert items to this cuckoo filter
			uint num_inserted = 0;
			for (ulong i = 0; i < total_items; i++, num_inserted++) {
				if (filter.Add (i) != Status.Ok) {
					break;
				}
			}
			// Check if previously inserted items are in the filter, expected
			// true for all items
			for (uint i = 0; i < num_inserted; i++) {
				Debug.Assert (filter.Contain (i) == Status.Ok);
			}


			// Check non-existing items, a few false positives expected
			uint total_queries = 0;
			uint false_queries = 0;
			for (ulong i = total_items; i < 2 * total_items; i++) {
				if (filter.Contain (i) == Status.Ok) {
					false_queries++;
					//Debug.WriteLine("False Positive : " + i);
				}
				total_queries++;
			}
			sw.Stop();
			Console.WriteLine("Elapsed Time={0}",sw.Elapsed);

			// Output the measured false positive rate
			Console.WriteLine ("False positive rate is {0:F4}%, false queries {1}, total queries {2}", 100.0 * false_queries / total_queries, false_queries, total_queries);
			Console.WriteLine (filter.Info());

            Console.WriteLine("Press Enter to finish");
            Console.ReadLine();
		}
	}
}
