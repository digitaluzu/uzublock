using UnityEngine;
using System.Collections;
using System.IO;

namespace Uzu
{
	public static class BlockReader
	{
		public static BlockFormat.Data Read (byte[] data)
		{
			using (MemoryStream stream = new MemoryStream (data)) {
				using (BinaryReader reader = new BinaryReader (stream)) {
					return ReadImpl (reader);
				}
			}
		}

		#region Implementation.
		private static BlockFormat.Data ReadImpl (BinaryReader reader)
		{
			// Verify file integrity.
			{
				uint magicNumber = reader.ReadUInt32 ();
				if (magicNumber != BlockFormat.MagicNumber) {
					Debug.LogError ("Invalid file format (corrupt magic number): " + magicNumber);
					return null;
				}
			}

			uint version = reader.ReadUInt32 ();

			// Handle support of multiple released data versions.
			// Deprecate support for versions as necessary.
			switch (version) {
			case 2:
				return ReadVersion_2 (version, reader);
			default:
				Debug.LogError ("Unsupported version: " + version);
				break;
			}

			return null;
		}

		private static BlockFormat.Data ReadVersion_2 (uint version, BinaryReader reader)
		{
			BlockFormat.Header header = new BlockFormat.Header ();

			{
				header.version = version;
				header.count = new VectorI3 (reader.ReadInt32 (), reader.ReadInt32 (), reader.ReadInt32 ());
			}

			BlockFormat.Data data = new BlockFormat.Data ();

			{
				VectorI3 xyz = header.count;

				{
					int totalCount = VectorI3.ElementProduct (xyz);
					data._states = new bool[totalCount];
					data._colors = new BlockFormat.RGB[totalCount];
				}

				int cnt = 0;
				for (int x = 0; x < xyz.x; x++) {
					for (int y = 0; y < xyz.y; y++) {
						for (int z = 0; z < xyz.z; z++) {
							data._states [cnt] = reader.ReadBoolean ();
							data._colors [cnt] = new BlockFormat.RGB (reader.ReadByte (), reader.ReadByte (), reader.ReadByte ());
							cnt++;
						}
					}
				}
			}

			return data;
		}
		#endregion
	}
}
