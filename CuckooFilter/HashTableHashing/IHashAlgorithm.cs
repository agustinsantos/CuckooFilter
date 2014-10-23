using System;

namespace HashTableHashing
{
	public interface IHashAlgorithm
	{
		uint Hash (byte[] data);
	}

	public interface ISeededHashAlgorithm : IHashAlgorithm
	{
		uint Hash (byte[] data, uint seed);
	}
}

