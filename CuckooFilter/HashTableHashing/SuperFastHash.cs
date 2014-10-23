
/***** BEGIN LICENSE BLOCK *****
 * Version: MPL 1.1/GPL 2.0/LGPL 2.1
 *
 * The contents of this file are subject to the Mozilla Public License Version
 * 1.1 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 * http://www.mozilla.org/MPL/
 *
 * Software distributed under the License is distributed on an "AS IS" basis,
 * WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License
 * for the specific language governing rights and limitations under the
 * License.
 *
 * The Original Code is HashTableHashing.SuperFastHash.
 *
 * The Initial Developer of the Original Code is
 * Davy Landman.
 * Portions created by the Initial Developer are Copyright (C) 2009
 * the Initial Developer. All Rights Reserved.
 *
 * Contributor(s):
 *
 *
 * Alternatively, the contents of this file may be used under the terms of
 * either the GNU General Public License Version 2 or later (the "GPL"), or
 * the GNU Lesser General Public License Version 2.1 or later (the "LGPL"),
 * in which case the provisions of the GPL or the LGPL are applicable instead
 * of those above. If you wish to allow use of your version of this file only
 * under the terms of either the GPL or the LGPL, and not to allow others to
 * use your version of this file under the terms of the MPL, indicate your
 * decision by deleting the provisions above and replace them with the notice
 * and other provisions required by the GPL or the LGPL. If you do not delete
 * the provisions above, a recipient may use your version of this file under
 * the terms of any one of the MPL, the GPL or the LGPL.
 *
 * ***** END LICENSE BLOCK ***** */
using System;
using System.Runtime.InteropServices;

/// <summary>
/// http://landman-code.blogspot.com.es/2009/02/c-superfasthash-and-murmurhash2.html
/// </summary>
namespace HashTableHashing
{
	public class SuperFastHashSimple : IHashAlgorithm
	{
		public uint Hash (byte[] dataToHash)
		{
			int dataLength = dataToHash.Length;
			if (dataLength == 0)
				return 0;
			uint hash = Convert.ToUInt32 (dataLength);
			int remainingBytes = dataLength & 3; // mod 4
			int numberOfLoops = dataLength >> 2; // div 4
			int currentIndex = 0;
			while (numberOfLoops > 0) {
				hash += BitConverter.ToUInt16 (dataToHash, currentIndex);
				uint tmp = (uint)(BitConverter.ToUInt16 (dataToHash, currentIndex + 2) << 11) ^ hash;
				hash = (hash << 16) ^ tmp;
				hash += hash >> 11;
				currentIndex += 4;
				numberOfLoops--;
			}

			switch (remainingBytes) {
			case 3:
				hash += BitConverter.ToUInt16 (dataToHash, currentIndex);
				hash ^= hash << 16;
				hash ^= ((uint)dataToHash [currentIndex + 2]) << 18;
				hash += hash >> 11;
				break;
			case 2:
				hash += BitConverter.ToUInt16 (dataToHash, currentIndex);
				hash ^= hash << 11;
				hash += hash >> 17;
				break;
			case 1:
				hash += dataToHash [currentIndex];
				hash ^= hash << 10;
				hash += hash >> 1;
				break;
			default:
				break;
			}

			/* Force "avalanching" of final 127 bits */
			hash ^= hash << 3;
			hash += hash >> 5;
			hash ^= hash << 4;
			hash += hash >> 17;
			hash ^= hash << 25;
			hash += hash >> 6;

			return hash;
		}
	}

	public class SuperFastHashInlineBitConverter : IHashAlgorithm
	{
		public uint Hash (byte[] dataToHash)
		{
			int dataLength = dataToHash.Length;
			if (dataLength == 0)
				return 0;
			uint hash = (uint)dataLength;
			int remainingBytes = dataLength & 3; // mod 4
			int numberOfLoops = dataLength >> 2; // div 4
			int currentIndex = 0;
			while (numberOfLoops > 0) {
				hash += (UInt16)(dataToHash [currentIndex++] | dataToHash [currentIndex++] << 8);
				uint tmp = (uint)((uint)(dataToHash [currentIndex++] | dataToHash [currentIndex++] << 8) << 11) ^ hash;
				hash = (hash << 16) ^ tmp;
				hash += hash >> 11;
				numberOfLoops--;
			}

			switch (remainingBytes) {
			case 3:
				hash += (UInt16)(dataToHash [currentIndex++] | dataToHash [currentIndex++] << 8);
				hash ^= hash << 16;
				hash ^= ((uint)dataToHash [currentIndex]) << 18;
				hash += hash >> 11;
				break;
			case 2:
				hash += (UInt16)(dataToHash [currentIndex++] | dataToHash [currentIndex] << 8);
				hash ^= hash << 11;
				hash += hash >> 17;
				break;
			case 1: 
				hash += dataToHash [currentIndex];
				hash ^= hash << 10;
				hash += hash >> 1;
				break;
			default:
				break;
			}

			/* Force "avalanching" of final 127 bits */
			hash ^= hash << 3;
			hash += hash >> 5;
			hash ^= hash << 4;
			hash += hash >> 17;
			hash ^= hash << 25;
			hash += hash >> 6;

			return hash;
		}
	}

	public class SuperFastHashUInt16Hack : IHashAlgorithm
	{
		[StructLayout(LayoutKind.Explicit)]
		// no guarantee this will remain working
		struct BytetoUInt16Converter
		{
			[FieldOffset(0)]
			public Byte[] Bytes;
			[FieldOffset(0)]
			public UInt16[] UInts;
		}

		public uint Hash (byte[] dataToHash)
		{
			int dataLength = dataToHash.Length;
			if (dataLength == 0)
				return 0;
			uint hash = (uint)dataLength;
			int remainingBytes = dataLength & 3; // mod 4
			int numberOfLoops = dataLength >> 2; // div 4
			int currentIndex = 0;
			UInt16[] arrayHack = new BytetoUInt16Converter { Bytes = dataToHash }.UInts;
			while (numberOfLoops > 0) {
				hash += arrayHack [currentIndex++];
				uint tmp = (uint)(arrayHack [currentIndex++] << 11) ^ hash;
				hash = (hash << 16) ^ tmp;
				hash += hash >> 11;
				numberOfLoops--;
			}
			currentIndex *= 2; // fix the length
			switch (remainingBytes) {
			case 3:
				hash += (UInt16)(dataToHash [currentIndex++] | dataToHash [currentIndex++] << 8);
				hash ^= hash << 16;
				hash ^= ((uint)dataToHash [currentIndex]) << 18;
				hash += hash >> 11;
				break;
			case 2:
				hash += (UInt16)(dataToHash [currentIndex++] | dataToHash [currentIndex] << 8);
				hash ^= hash << 11;
				hash += hash >> 17;
				break;
			case 1:
				hash += dataToHash [currentIndex];
				hash ^= hash << 10;
				hash += hash >> 1;
				break;
			default:
				break;
			}

			/* Force "avalanching" of final 127 bits */
			hash ^= hash << 3;
			hash += hash >> 5;
			hash ^= hash << 4;
			hash += hash >> 17;
			hash ^= hash << 25;
			hash += hash >> 6;

			return hash;
		}
	}

	public class SuperFastHashUnsafe : IHashAlgorithm
	{
		public unsafe uint Hash (byte[] dataToHash)
		{
			int dataLength = dataToHash.Length;
			if (dataLength == 0)
				return 0;
			uint hash = (uint)dataLength;
			int remainingBytes = dataLength & 3; // mod 4
			int numberOfLoops = dataLength >> 2; // div 4

			fixed (byte* firstByte = &(dataToHash[0])) {
				/* Main loop */
				UInt16* data = (UInt16*)firstByte;
				for (; numberOfLoops > 0; numberOfLoops--) {
					hash += *data;
					uint tmp = (uint)(*(data + 1) << 11) ^ hash;
					hash = (hash << 16) ^ tmp;
					data += 2;
					hash += hash >> 11;
				}
				switch (remainingBytes) {
				case 3:
					hash += *data;
					hash ^= hash << 16;
					hash ^= ((uint)(*(((Byte*)(data)) + 2))) << 18;
					hash += hash >> 11;
					break;
				case 2:
					hash += *data;
					hash ^= hash << 11;
					hash += hash >> 17;
					break;
				case 1: 
					hash += *((Byte*)data);
					hash ^= hash << 10;
					hash += hash >> 1;
					break;
				default:
					break;
				}
			}

			/* Force "avalanching" of final 127 bits */
			hash ^= hash << 3;
			hash += hash >> 5;
			hash ^= hash << 4;
			hash += hash >> 17;
			hash ^= hash << 25;
			hash += hash >> 6;

			return hash;
		}
	}
}

