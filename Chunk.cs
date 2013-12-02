using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// A single "chunk" within the block world.
	/// Each chunk represents a single mesh.
	/// Large chunks will require more time to rebuild due to the large number of vertices contained.
	/// Small chunks take less time to rebuild, but this will increase the number of meshes (draw calls) for the scene.
	/// Chunks with multiple materials are split into submeshes.
	/// </summary>
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	public class Chunk : BaseBehaviour
	{
		/// <summary>
		/// Does this chunk need to be rebuilt?
		/// </summary>
		public bool DoesNeedRebuild { get { return _doesNeedRebuild; } }
	
		/// <summary>
		/// Request that this chunk be rebuilt.
		/// </summary>
		public void RequestRebuild ()
		{
			_doesNeedRebuild = true;
		}
	
		/// <summary>
		/// Gets the blocks associated with this chunk.
		/// </summary>
		public BlockContainer Blocks { get { return _blocks; } }
	
		/// <summary>
		/// Performs initialization.
		/// Called when a chunk is first added to the world.
		/// </summary>
		public void Initialize (BlockWorldConfig worldConfig)
		{
			_config = worldConfig;
		
			if (_blocks == null) {
				_blocks = new BlockContainer (_config.ChunkSizeInBlocks);
			}
		
			if (_meshDesc == null) {
				_meshDesc = new ChunkMeshDesc (GetChunkMeshCreationConfig ());
			}
		}
	
		/// <summary>
		/// Performs chunk cleanup.
		/// </summary>
		public void TearDown ()
		{
			_mesh.Clear ();
		
			_doesNeedRebuild = false;
		}
	
		/// <summary>
		/// Rebuilds this chunk's mesh.
		/// </summary>
		public void Rebuild ()
		{
			//using (ScopedSystemTimer timer = new ScopedSystemTimer("Chunk Rebuild Time"))
			{
				// Rebuild batches in case materials have changed.
				RebuildBatches ();
			
				Vector3 blockSize = _config.BlockSize;
				VectorI3 count = _blocks.CountXYZ;
				int countX = count.x;
				int countY = count.y;
				int countZ = count.z;
				int lastX = countX - 1;
				int lastY = countY - 1;
				int lastZ = countZ - 1;
			
				int createdFaceCount = 0;
			
				// For each block...
				int currentIndex = 0;
				for (int x = 0; x < countX; x++) {
					for (int y = 0; y < countY; y++) {
						for (int z = 0; z < countZ; z++) {
							Block thisBlock = _blocks [currentIndex];
						
							// Skip empty blocks.
							if (thisBlock.IsEmpty) {
								++currentIndex;
								continue;
							}
						
							BlockDesc blockDesc = _config.BlockDescs [(int)thisBlock.Type];
							
							#region Ignore hidden faces.
							BlockFaceFlag activeFaces = FACEFLAG_FULL_MASK;
						
							// Apply user-specified ignore faces.
							activeFaces &= ~blockDesc.IgnoreFaces;
						
							// Disabled shared faces.
							if (x > 0 &&
								!_blocks [x - 1, y, z].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Left;
							}
							if (x < lastX &&
								!_blocks [x + 1, y, z].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Right;
							}
						
							if (y > 0 &&
								!_blocks [x, y - 1, z].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Bottom;
							}
							if (y < lastY &&
								!_blocks [x, y + 1, z].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Top;
							}
						
							if (z > 0 &&
								!_blocks [x, y, z - 1].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Front;
							}
							if (z < lastZ &&
								!_blocks [x, y, z + 1].IsEmpty) {
								activeFaces &= ~BlockFaceFlag.Back;
							}
							#endregion
							
							if (createdFaceCount > _config.MaxVisibleBlockFaceCountPerChunk) {
								Debug.Log ("Visibile face count exceeded maximum count of [" + _config.MaxVisibleBlockFaceCountPerChunk + "]. Ignoring faces.");
								break;
							}
						
							// Create the block.
							int subMeshIndex = _subMeshLUT [(int)thisBlock.Type];
							ChunkSubMeshDesc subMeshDesc = _subMeshDescs [subMeshIndex];
							Vector3 blockOffset = new Vector3 (x * blockSize.x, y * blockSize.y, z * blockSize.z);
							createdFaceCount += CreateBlock (blockDesc, blockOffset, blockSize, activeFaces, _meshDesc, subMeshDesc, thisBlock.Color);
						
							++currentIndex;
						}
					}
				}
			
				// Construct mesh.
				{
					// Fill the arrays out w/ default data so that no garbage remains.
					{
						for (int i = 0; i < _subMeshDescs.Count; i++) {
							_subMeshDescs [i].FillToCapacity ();
						}
					}
				
					_mesh.vertices = _meshDesc.VertexList.ToArray ();
					_mesh.normals = _meshDesc.NormalList.ToArray ();
					_mesh.colors32 = _meshDesc.ColorList.ToArray ();
					_mesh.uv = _meshDesc.UVList.ToArray ();
				
					for (int i = 0; i < _subMeshDescs.Count; i++) {
						ChunkSubMeshDesc subMeshDesc = _subMeshDescs [i];
						_mesh.SetTriangles (subMeshDesc.IndexList.ToArray (), i);
					}
				
					_mesh.Optimize ();
				}
			
				// Reset flag.
				_doesNeedRebuild = false;
			}
		}
	
		/// <summary>
		/// Gets the block type of a given block index.
		/// </summary>
		public BlockType GetBlockType (VectorI3 blockChunkIndex)
		{
			Block block = _blocks [blockChunkIndex.x, blockChunkIndex.y, blockChunkIndex.z];
			return block.Type;
		}
	
		/// <summary>
		/// Changes the block type of a given block index.
		/// </summary>
		public void SetBlockType (VectorI3 blockChunkIndex, BlockType blockType)
		{
			Block block = _blocks [blockChunkIndex.x, blockChunkIndex.y, blockChunkIndex.z];
		
			// Ignore if already the same.
			if (block.Type == blockType) {
				return;
			}
		
			block.Type = blockType;
		
			// Mark as dirty.
			RequestRebuild ();
		}
	
		/// <summary>
		/// Changes the block color of a given block index.
		/// </summary>
		public void SetBlockColor (VectorI3 blockChunkIndex, Color32 blockColor)
		{
			Block block = _blocks [blockChunkIndex.x, blockChunkIndex.y, blockChunkIndex.z];
		
			// Ignore if already the same.
			{
				Color32 oldColor = block.Color;
				if (oldColor.r == blockColor.r &&
					oldColor.g == blockColor.g &&
					oldColor.b == blockColor.b &&
					oldColor.a == blockColor.a) {
					return;
				}
			}
		
			block.Color = blockColor;
		
			// Mark as dirty.
			RequestRebuild ();
		}
		
		#region Implementation.
		private BlockContainer _blocks;
		private BlockWorldConfig _config;
		private Mesh _mesh;
		private MeshRenderer _meshRenderer;
		private ChunkMeshDesc _meshDesc;
		private SmartList<ChunkSubMeshDesc> _subMeshDescs = new SmartList<ChunkSubMeshDesc> ();
		private SmartList<ChunkSubMeshDesc> _subMeshDescPool = new SmartList<ChunkSubMeshDesc> ();
		private FixedList<int> _subMeshLUT = new FixedList<int> ((int)BlockType.MAX_COUNT);
		private bool _doesNeedRebuild;
		private BlockFaceFlag FACEFLAG_FULL_MASK = (BlockFaceFlag)0xFF;
		
		/// <summary>
		/// Work array to prevent garbage generation.
		/// </summary>
		private SmartList<Material> _materialBatchesWork = new SmartList<Material> ();
		
#if false
		public void OnDrawGizmosSelected()
		{			
			float blockSize = _config.BlockSize.x;
			float halfBlockSize = blockSize * 0.5f;
			VectorI3 count = _blocks.CountXYZ;
			
			#region Draw block type ids.
			for (int x = 0; x < count.x; x++) {
				for (int y = 0; y < count.y; y++) {
					for (int z = 0; z < count.z; z++) {
						Block thisBlock = _blocks [x, y, z];
						
						Vector3 blockOffset = new Vector3 (x * blockSize + halfBlockSize, y * blockSize + halfBlockSize, z * blockSize + halfBlockSize);
						Dbg.DrawText(CachedXform.localPosition + blockOffset, ((int)thisBlock.BlockType).ToString());
						break;
					}
				}
			}
			#endregion
		}
#endif
	
		/// <summary>
		/// Creates a single block at the given base position.
		/// Returns the number of generated faces.
		/// </summary>
		private static int CreateBlock (BlockDesc blockDesc, Vector3 basePos, Vector3 blockSize, BlockFaceFlag activeFacesFlag, ChunkMeshDesc meshDesc, ChunkSubMeshDesc subMeshDesc, Color32 blockColor)
		{
			int faceCount = 0;
		
			// All faces are hidden, so we don't need to do anything.
			if (activeFacesFlag == 0) {
				return faceCount;
			}
		
			float x = basePos.x;
			float y = basePos.y;
			float z = basePos.z;
			
			FixedList<Vector3> vList = meshDesc.VertexList;
			FixedList<int> iList = subMeshDesc.IndexList;
			FixedList<Vector3> nList = meshDesc.NormalList;
			FixedList<Color32> cList = meshDesc.ColorList;
			FixedList<Vector2> uvList = meshDesc.UVList;
			
			// Vertices.
			Vector3 v0 = new Vector3 (x, y, z);
			Vector3 v1 = new Vector3 (x + blockSize.x, y, z);
			Vector3 v2 = new Vector3 (x + blockSize.x, y + blockSize.y, z);
			Vector3 v3 = new Vector3 (x, y + blockSize.y, z);
			Vector3 v4 = new Vector3 (x + blockSize.x, y, z + blockSize.z);
			Vector3 v5 = new Vector3 (x, y, z + blockSize.z);
			Vector3 v6 = new Vector3 (x, y + blockSize.y, z + blockSize.z);
			Vector3 v7 = new Vector3 (x + blockSize.x, y + blockSize.y, z + blockSize.z);
		
			// Normals.
			Vector3 up = Vector3.up;
			Vector3 down = Vector3.down;
			Vector3 front = Vector3.back;
			Vector3 back = Vector3.forward;
			Vector3 left = Vector3.left;
			Vector3 right = Vector3.right;
		
			// UVs.
			Vector2 uv00 = Vector2.zero;
			Vector2 uv10 = new Vector2 (1.0f, 0.0f);
			Vector2 uv11 = Vector2.one;
			Vector2 uv01 = new Vector2 (0.0f, 1.0f);
		
			// Starting index offset (offset by # of vertices).
			int baseIdx = vList.Count;
		
			#region Polygon construction.
			if ((activeFacesFlag & BlockFaceFlag.Front) != 0) {
				vList.Add (v0);
				vList.Add (v1);
				vList.Add (v2);
				vList.Add (v3);
			
				nList.Add (front);
				nList.Add (front);
				nList.Add (front);
				nList.Add (front);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				iList.Add (baseIdx + 3);
				iList.Add (baseIdx + 1);
				iList.Add (baseIdx + 0);
			
				iList.Add (baseIdx + 3);
				iList.Add (baseIdx + 2);
				iList.Add (baseIdx + 1);
			
				faceCount++;
			}
		
			if ((activeFacesFlag & BlockFaceFlag.Right) != 0) {
				vList.Add (v1);
				vList.Add (v4);
				vList.Add (v7);
				vList.Add (v2);
			
				nList.Add (right);
				nList.Add (right);
				nList.Add (right);
				nList.Add (right);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				int idxOffset = 4 * faceCount;
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
				iList.Add (baseIdx + 0 + idxOffset);
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 2 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
			
				faceCount++;
			}
		
			if ((activeFacesFlag & BlockFaceFlag.Back) != 0) {
				vList.Add (v4);
				vList.Add (v5);
				vList.Add (v6);
				vList.Add (v7);
			
				nList.Add (back);
				nList.Add (back);
				nList.Add (back);
				nList.Add (back);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				int idxOffset = 4 * faceCount;
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
				iList.Add (baseIdx + 0 + idxOffset);
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 2 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
			
				faceCount++;
			}
		
			if ((activeFacesFlag & BlockFaceFlag.Left) != 0) {
				vList.Add (v5);
				vList.Add (v0);
				vList.Add (v3);
				vList.Add (v6);
			
				nList.Add (left);
				nList.Add (left);
				nList.Add (left);
				nList.Add (left);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				int idxOffset = 4 * faceCount;
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
				iList.Add (baseIdx + 0 + idxOffset);
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 2 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
			
				faceCount++;
			}
		
			if ((activeFacesFlag & BlockFaceFlag.Top) != 0) {
				vList.Add (v3);
				vList.Add (v2);
				vList.Add (v7);
				vList.Add (v6);
			
				nList.Add (up);
				nList.Add (up);
				nList.Add (up);
				nList.Add (up);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				int idxOffset = 4 * faceCount;
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
				iList.Add (baseIdx + 0 + idxOffset);
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 2 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
			
				faceCount++;
			}
		
			if ((activeFacesFlag & BlockFaceFlag.Bottom) != 0) {
				vList.Add (v5);
				vList.Add (v4);
				vList.Add (v1);
				vList.Add (v0);
			
				nList.Add (down);
				nList.Add (down);
				nList.Add (down);
				nList.Add (down);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			
				int idxOffset = 4 * faceCount;
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
				iList.Add (baseIdx + 0 + idxOffset);
			
				iList.Add (baseIdx + 3 + idxOffset);
				iList.Add (baseIdx + 2 + idxOffset);
				iList.Add (baseIdx + 1 + idxOffset);
			
				faceCount++;
			}
		
			// Set colors per vertex.
			int vertCount = faceCount * 4;
			for (int i = 0; i < vertCount; ++i) {
				cList.Add (blockColor);
			}
			#endregion
		
			return faceCount;
		}
	
		/// <summary>
		/// Rebuilds the batches associated w/ this chunk.
		/// Used for material batching and subMesh division.
		/// </summary>
		private void RebuildBatches ()
		{
			// Clean up existing batch info.
			{
				// Return to pool for re-use.
				// Push in reverse order so that we don't flip back and forth each time.
				for (int i = _subMeshDescs.Count - 1; i >= 0; i--) {
					ChunkSubMeshDesc subMeshDesc = _subMeshDescs [i];
					subMeshDesc.Clear ();
					_subMeshDescPool.Add (subMeshDesc);
				}
			
				_mesh.Clear ();
				_meshDesc.Clear ();			
				_subMeshDescs.Clear ();
				_subMeshLUT.Clear ();
			}
			
			// Calculate the # of batches.
			{			
				// Reset LUT to default state.
				{
					const int INVALID_BATCH_INDEX = -1;
					for (int i = 0; i < (int)BlockType.MAX_COUNT; i++) {
						_subMeshLUT.Add (INVALID_BATCH_INDEX);
					}
				}
			
				// For each block...
				int count = _blocks.Count;
				for (int i = 0; i < count; i++) {
					Block block = _blocks [i];
					
					// Empty blocks don't use materials.
					if (block.IsEmpty) {
						continue;
					}
					
					int blockTypeInt = (int)block.Type;
					BlockDesc blockDesc = _config.BlockDescs [blockTypeInt];
						
					// Does a batch for this block desc already exist?
					bool doesBatchExist = false;
					for (int j = 0; j < _materialBatchesWork.Count; j++) {
						if (_materialBatchesWork [j] == blockDesc.Material) {
							// Add to main LUT.
							int batchIndex = j;
							_subMeshLUT [blockTypeInt] = batchIndex;
						
							doesBatchExist = true;					
							break;
						}
					}
				
					// New batch discovered.
					if (!doesBatchExist) {
						int batchIndex = _materialBatchesWork.Count;
						_subMeshLUT [blockTypeInt] = batchIndex;
						_materialBatchesWork.Add (blockDesc.Material);						
					}
				}
			}
			
			int batchCount = _materialBatchesWork.Count;

			// No materials used (empty chunk).
			if (batchCount == 0) {
				return;
			}
			
			// One subMesh per batch.
			_mesh.subMeshCount = batchCount;

			_meshRenderer.materials = _materialBatchesWork.ToArray ();
			
			// Create mesh descs.
			{
				ChunkMeshCreationConfig config = GetChunkMeshCreationConfig ();
		
				for (int i = 0; i < batchCount; i++) {
					ChunkSubMeshDesc subMeshDesc;
				
					// Take from pool if available.
					if (_subMeshDescPool.Count > 0) {
						int lastIndex = _subMeshDescPool.Count - 1;
						subMeshDesc = _subMeshDescPool [lastIndex];
						_subMeshDescPool.RemoveAt (lastIndex);
					} else {
						subMeshDesc = new ChunkSubMeshDesc (config);
					}
					
					Dbg.Assert (subMeshDesc != null);
					_subMeshDescs.Add (subMeshDesc);
				}
			}
	
#if UNITY_EDITOR
			{
				int expectedBatchCount = _materialBatchesWork.Count;
				int actualBatchCount = _subMeshDescs.Count;
				if (actualBatchCount != expectedBatchCount) {
					Debug.LogError("Invalid batch count: " + actualBatchCount + "/" + expectedBatchCount);
				}
			}
#endif
			
			// Clear for re-use next frame.
			_materialBatchesWork.Clear ();
		}
		
		private ChunkMeshCreationConfig GetChunkMeshCreationConfig ()
		{
			const int FACE_COUNT_PER_BLOCK = 6;
			ChunkMeshCreationConfig config = new ChunkMeshCreationConfig ();
		
			// Add 1 block as buffer.
			config.MaxVisibileFaceCount = _config.MaxVisibleBlockFaceCountPerChunk + FACE_COUNT_PER_BLOCK;
		
			return config;
		}
	
		protected override void Awake ()
		{
			base.Awake ();
		
			// Setup mesh to be used for rendering this chunk.
			{
				_mesh = new Mesh ();
				MeshFilter meshFilter = GetComponent<MeshFilter> ();
				meshFilter.mesh = _mesh;
			
				_meshRenderer = GetComponent<MeshRenderer> ();
				_meshRenderer.castShadows = false;
				_meshRenderer.receiveShadows = false;
			
				_mesh.MarkDynamic ();
			}
		}
		#endregion
	}
}