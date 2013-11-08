using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// Configuration for creating a ChunkMesh.
	/// </summary>
	public struct ChunkMeshCreationConfig
	{
		/// <summary>
		/// The maximum number of faces that will be visible for this chunk.
		/// If the actual number of faces exceeds this number, the exceeding faces will not be rendered.
		/// </summary>
		public int MaxVisibileFaceCount { get; set; }	
	}
	
	/// <summary>
	/// Container for all the information we need to construct a mesh.
	/// </summary>
	public class ChunkMeshDesc
	{	
		public FixedList<Vector3> VertexList;
		public FixedList<Vector3> NormalList;
		public FixedList<Color32> ColorList;
		public FixedList<Vector2> UVList;
		
		public ChunkMeshDesc (ChunkMeshCreationConfig config)
		{
			int maxVerticesPerFace = 4;
			int maxNormalsPerFace = maxVerticesPerFace;
			int maxColorsPerFace = maxVerticesPerFace;
			int maxUVsPerFace = maxVerticesPerFace;
			
			VertexList = new FixedList<Vector3> (maxVerticesPerFace * config.MaxVisibileFaceCount);
			NormalList = new FixedList<Vector3> (maxNormalsPerFace * config.MaxVisibileFaceCount);
			ColorList = new FixedList<Color32> (maxColorsPerFace * config.MaxVisibileFaceCount);
			UVList = new FixedList<Vector2> (maxUVsPerFace * config.MaxVisibileFaceCount);
		}
		
		public void Clear ()
		{
			VertexList.Clear ();
			NormalList.Clear ();
			ColorList.Clear ();
			UVList.Clear ();
		}
	}
	
	/// <summary>
	/// Container for all the information we need to construct a subMesh.
	/// </summary>
	public class ChunkSubMeshDesc
	{	
		public FixedList<int> IndexList;
		private int[] _previousCounts = new int[(int)ListIndex.MAX_COUNT];
		
		private enum ListIndex
		{
			INDEX,
			MAX_COUNT,
		}
		
		public ChunkSubMeshDesc (ChunkMeshCreationConfig config)
		{
			int maxIndicesPerFace = 6;
			
			IndexList = new FixedList<int> (maxIndicesPerFace * config.MaxVisibileFaceCount);
		}
		
		public void Clear ()
		{
			// Store the list sizes before clear.
			_previousCounts [(int)ListIndex.INDEX] = IndexList.Count;
			
			IndexList.Clear ();
		}
		
		/// <summary>
		/// Fill the arrays to max capacity with default data
		/// to prevent garbage from appearing in buffer.
		/// </summary>
		public void FillToCapacity ()
		{
			// In order to prevent garbage from filling the buffers,
			// we need to clear only up until the previous size of each
			// of the buffers.
			
			{
				int from = IndexList.Count;
				int to = _previousCounts [(int)ListIndex.INDEX];
				for (int i = from; i < to; i++) {
					IndexList [i] = 0;
				}
			}
		}
	}
}