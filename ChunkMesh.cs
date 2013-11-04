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
		public Uzu.FixedList<Vector3> VertexList;
		public Uzu.FixedList<Vector3> NormalList;
		public Uzu.FixedList<Color> ColorList;
		public Uzu.FixedList<Vector2> UVList;
		private int[] _previousCounts = new int[(int)ListIndex.MAX_COUNT];
		
		private enum ListIndex
		{
			VERTEX,
			NORMAL,
			COLOR,
			UV,
			MAX_COUNT,
		}
		
		public ChunkMeshDesc (ChunkMeshCreationConfig config)
		{
			int maxVerticesPerFace = 4;
			int maxNormalsPerFace = maxVerticesPerFace;
			int maxColorsPerFace = maxVerticesPerFace;
			int maxUVsPerFace = maxVerticesPerFace;
			
			VertexList = new Uzu.FixedList<Vector3> (maxVerticesPerFace * config.MaxVisibileFaceCount);
			NormalList = new Uzu.FixedList<Vector3> (maxNormalsPerFace * config.MaxVisibileFaceCount);
			ColorList = new Uzu.FixedList<Color> (maxColorsPerFace * config.MaxVisibileFaceCount);
			UVList = new Uzu.FixedList<Vector2> (maxUVsPerFace * config.MaxVisibileFaceCount);
		}
		
		public void Clear ()
		{
			// Store the list sizes before clear.
			_previousCounts [(int)ListIndex.VERTEX] = VertexList.Count;
			_previousCounts [(int)ListIndex.NORMAL] = NormalList.Count;
			_previousCounts [(int)ListIndex.COLOR] = ColorList.Count;
			_previousCounts [(int)ListIndex.UV] = UVList.Count;
			
			VertexList.Clear ();
			NormalList.Clear ();
			ColorList.Clear ();
			UVList.Clear ();
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
				int from = VertexList.Count;
				int to = _previousCounts [(int)ListIndex.VERTEX];
				for (int i = from; i < to; i++) {
					VertexList [i] = Vector3.zero;
				}
			}
			{
				int from = NormalList.Count;
				int to = _previousCounts [(int)ListIndex.NORMAL];
				for (int i = from; i < to; i++) {
					NormalList [i] = Vector3.zero;
				}
			}
			{
				int from = ColorList.Count;
				int to = _previousCounts [(int)ListIndex.COLOR];
				for (int i = from; i < to; i++) {
					ColorList [i] = Color.white;
				}
			}
			{
				int from = UVList.Count;
				int to = _previousCounts [(int)ListIndex.UV];
				for (int i = from; i < to; i++) {
					UVList [i] = Vector2.zero;
				}
			}
		}
	}
	
	/// <summary>
	/// Container for all the information we need to construct a subMesh.
	/// </summary>
	public class ChunkSubMeshDesc
	{	
		public Uzu.FixedList<int> IndexList;
		private int[] _previousCounts = new int[(int)ListIndex.MAX_COUNT];
		
		private enum ListIndex
		{
			INDEX,
			MAX_COUNT,
		}
		
		public ChunkSubMeshDesc (ChunkMeshCreationConfig config)
		{
			int maxIndicesPerFace = 6;
			
			IndexList = new Uzu.FixedList<int> (maxIndicesPerFace * config.MaxVisibileFaceCount);
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