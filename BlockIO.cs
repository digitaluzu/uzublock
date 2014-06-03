using UnityEngine;
using System.Collections;
using System.IO;

namespace Uzu
{
	public static class BlockIO
	{
		public static void WriteFile (string filePath, byte[] data)
		{
			using (FileStream resourceFile = new FileStream (filePath, FileMode.Create, FileAccess.Write)) {
				Debug.Log ("Writing (" + ToByteStr (data.Length) + "): " + filePath);

				resourceFile.Write (data, 0, data.Length);
			}
		}

		public static byte[] ReadFile (string filePath)
		{
			using (MemoryStream ms = new MemoryStream()) {
				using (FileStream resourceFile = new FileStream (filePath, FileMode.Open, FileAccess.Read)) {
					Debug.Log ("Reading (" + ToByteStr (resourceFile.Length) + "): " + filePath);

					byte[] data = new byte[resourceFile.Length];
					resourceFile.Read (data, 0, (int)resourceFile.Length);
					ms.Write (data, 0, (int)resourceFile.Length);

					return ms.ToArray ();
				}
			}
		}

		#region Implementation.
		private static string ToByteStr (long byteCount)
		{
			long kbCount = byteCount / 1024;

			if (kbCount > 0) {
				byteCount -= (kbCount * 1024);
				return kbCount + "KB, " + byteCount + "B";
			}

			return byteCount + "B";
		}
		#endregion
	}
}
