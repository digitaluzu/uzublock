using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// Configuration used in creation of a block world.
	/// </summary>
	public class BlockWorldConfig
	{
		public Vector3 BlockSize { get; set; }
		
		/// <summary>
		/// Number of blocks per chunk.
		/// The smaller the chunk size, the better the chunk Rebuild performance.
		/// However, too many small chunks will incur update and memory and draw call performance hits.
		/// </summary>
		public Uzu.VectorI3 ChunkSizeInBlocks { get; set; }
		
		/// <summary>
		/// Maximum number of chunks to rebuild per frame
		/// Set to <= 0 to not limit the number.
		/// </summary>
		public int MaxChunkRebuildCountPerFrame { get; set; }
		
		/// <summary>
		/// Maximum number of visible blocks that will exist in a chunk.
		/// If this parameter is not set from outside, the default value will be used.
		/// This only affects the number of visible block faces - the block states
		/// and collision data is not affected.
		/// </summary>
		public int MaxVisibleBlockFaceCountPerChunk { get; set; }
		
		public BlockDesc[] BlockDescs {
			get { return _blockDescs; }
			set {
	#if UNITY_EDITOR
				{
					// Verify size is adequate.
					if (value.Length < (int)BlockType.MAX_COUNT) {
						Debug.LogError ("Invalid block desc list. Must contain at least [" + (int)BlockType.MAX_COUNT + "] entries.");
						return;
					}
					// Verify contents.
					foreach (BlockDesc desc in value) {
						if (desc == null) {
							Debug.LogError ("Invalid (null) desc contained in block desc list.");
							return;
						}
					}
				}
	#endif
				_blockDescs = value;
			}
		}
		
		public delegate void OnChunkLoadDelegate (BlockWorldChunkLoadContext context);
	
		public delegate void OnChunkUnloadDelegate (BlockWorldChunkUnloadContext context);
		
		/// <summary>
		/// Called when a chunk is stream in and loaded.
		/// </summary>
		public OnChunkLoadDelegate OnChunkLoad { get; set; }
		
		/// <summary>
		/// Called when a chunk is stream out and unloaded.
		/// </summary>
		public OnChunkUnloadDelegate OnChunkUnload { get; set; }
	
		private BlockDesc[] _blockDescs;
	}
	
	/// <summary>
	/// Context passed to callback when chunk is loaded in.
	/// </summary>
	public struct BlockWorldChunkLoadContext
	{
		public BlockWorld BlockWorld { get; set; }
	
		public Uzu.VectorI3 ChunkIndex { get; set; }
		
		public BlockContainer Blocks { get; set; }
	}
	
	/// <summary>
	/// Context passed to callback when chunk is unloaded.
	/// </summary>
	public struct BlockWorldChunkUnloadContext
	{
		public BlockWorld BlockWorld { get; set; }
	
		public Uzu.VectorI3 ChunkIndex { get; set; }
	}
	
	/// <summary>
	/// Iterator class for chunks.
	/// </summary>
	public struct ChunkIterator
	{
		private Dictionary<Uzu.VectorI3, Chunk>.Enumerator _it;
	
		public ChunkIterator (Dictionary<Uzu.VectorI3, Chunk>.Enumerator beginIt)
		{
			_it = beginIt;
		}
		
		/// <summary>
		/// Progress to the next value, returning false if finished.
		/// </summary>
		public bool MoveNext ()
		{
			return _it.MoveNext ();
		}
	
		public Chunk CurrentChunk {
			get { return _it.Current.Value; }
		}
		
		public Uzu.VectorI3 CurrentChunkIndex {
			get { return _it.Current.Key; }
		}
	}
	
	/// <summary>
	/// Represents an entire block world.
	/// Block worlds contain chunks, which in turn contain the actual blocks.
	/// All block related functionality (block world setup, collision detection, etc.)
	/// are exposed through the block world interface.
	/// </summary>
	public class BlockWorld : Uzu.BaseBehaviour
	{
		private BlockWorldConfig _config;
		private List<Chunk> _idleChunks = new List<Chunk> ();
		private Dictionary<Uzu.VectorI3, Chunk> _chunksToLoad = new Dictionary<Uzu.VectorI3, Chunk> ();
		private Dictionary<Uzu.VectorI3, Chunk> _activeChunks = new Dictionary<Uzu.VectorI3, Chunk> ();
		
		public BlockWorldConfig Config {
			get { return _config; }
		}
		
		/// <summary>
		/// Returns an iterator to the currently active chunks.
		/// </summary>
		public ChunkIterator GetActiveChunksIterator ()
		{
			return new ChunkIterator (_activeChunks.GetEnumerator ());
		}
		
		/// <summary>
		/// Perform initialization.
		/// </summary>
		public void Initialize (BlockWorldConfig config)
		{
			_config = config;
			
			// Calculate max block count per chunk.
			{
				int maxFaceCount = _config.MaxVisibleBlockFaceCountPerChunk;
				if (maxFaceCount == 0) {
					// Assume every other block is set, which means that every block face is exposed.
					// This should be the worst possible case.
					const int FACE_COUNT_PER_BLOCK = 6;
					maxFaceCount = (Uzu.VectorI3.ElementProduct (_config.ChunkSizeInBlocks) * FACE_COUNT_PER_BLOCK) / 2;
				}
				_config.MaxVisibleBlockFaceCountPerChunk = maxFaceCount;
			}
			
			InternalInitialize ();
		}
		
		#region Loading.	
		/// <summary>
		/// Loads a chunk at the given index.
		/// </summary>
		public void LoadChunk (Uzu.VectorI3 chunkIndex)
		{
			// Find an available chunk to use, or create a new one if we have to.
			Chunk chunk;
			{
				if (_idleChunks.Count > 0) {
					int lastIndex = _idleChunks.Count - 1;
					chunk = _idleChunks [lastIndex];
					_idleChunks.RemoveAt (lastIndex);
				} else {
					GameObject chunkGO = new GameObject (string.Empty, typeof(Chunk));
					chunk = chunkGO.GetComponent<Chunk> ();
				}
				
				PrepareChunk (chunk, chunkIndex);
			}
			
			// Duplicate load.
			if (_chunksToLoad.ContainsKey (chunkIndex)) {
				Debug.LogError (chunk.name + " is already requesting load.");
				return;
			}
			
			// Queue up to load.
			_chunksToLoad.Add (chunkIndex, chunk);
		}
	
		/// <summary>
		/// Unloads a chunk at the given index.
		/// </summary>
		public void UnloadChunk (Uzu.VectorI3 chunkIndex)
		{
			Chunk chunk;
			
			// Currently in load queue, so just remove it.
			if (_chunksToLoad.TryGetValue (chunkIndex, out chunk)) {
				_idleChunks.Add (chunk);
				return;
			}		
			
			if (!_activeChunks.TryGetValue (chunkIndex, out chunk)) {
				Debug.LogError ("Chunk at index [" + chunkIndex + "] is not loaded, so unable to unload.");
				return;
			}
			
			if (_idleChunks.Contains (chunk)) {
				Debug.LogError ("Attempting to return " + chunk.name + " to available pool, but it already exists.");
				return;
			}
			
			// Trigger callback.
			{
				BlockWorldChunkUnloadContext unloadContext = new BlockWorldChunkUnloadContext ();
				unloadContext.BlockWorld = this;
				unloadContext.ChunkIndex = chunkIndex;
				if (_config.OnChunkUnload != null) {
					_config.OnChunkUnload (unloadContext);
				}
			}
			
			// Cleanup.
			chunk.TearDown ();
			_activeChunks.Remove (chunkIndex);
			
			// Return to pool for re-use.
			_idleChunks.Add (chunk);
		}
		
		#region Implementation.
		private void PrepareChunk (Chunk chunk, Uzu.VectorI3 chunkIndex)
		{
			// Set name to make it easy to read.
			chunk.name = "Chunk_" + chunkIndex.x + "_" + chunkIndex.y + "_" + chunkIndex.z;
			
			Vector3 chunkOffsetPos = chunkIndex * _config.ChunkSizeInBlocks * _config.BlockSize;
			
			// Make chunk a child of world.
			GameObject chunkGO = chunk.gameObject;
			chunkGO.transform.parent = CachedXform;
			chunkGO.transform.localPosition = chunkOffsetPos;
			
			// Perform initialization.
			chunk.Initialize (_config);
		}
		#endregion
		#endregion
		
		#region Index and position utilities.
		private bool GetChunk (Uzu.VectorI3 blockWorldIndex, out Chunk chunk, out Uzu.VectorI3 blockChunkIndex)
		{
			if (IsValidWorldBlockIndex (blockWorldIndex)) {
				Uzu.VectorI3 chunkIndex = blockWorldIndex / _config.ChunkSizeInBlocks;
				if (_activeChunks.TryGetValue (chunkIndex, out chunk)) {
					blockChunkIndex = blockWorldIndex - (chunkIndex * _config.ChunkSizeInBlocks);
					return true;
				}
			}
			
			chunk = null;
			blockChunkIndex = Uzu.VectorI3.zero;
			return false;
		}
		
		public Uzu.VectorI3 GetBlockWorldIndex (Vector3 worldPos)
		{
			Vector3 blockSize = _config.BlockSize;
			Vector3 relPos = worldPos - CachedXform.position;
			return new Uzu.VectorI3 (relPos.x / blockSize.x, relPos.y / blockSize.y, relPos.z / blockSize.z);
		}
		
		/// <summary>
		/// Gets the base position of the specified blockWorldIndex.
		/// </summary>
		public Vector3 GetBlockBasePosition (Uzu.VectorI3 blockWorldIndex)
		{
			Vector3 blockSize = _config.BlockSize;
			Vector3 relPos = blockWorldIndex * blockSize;
			return relPos + CachedXform.position;
		}
		
		/// <summary>
		/// Gets the center position of the specified blockWorldIndex.
		/// </summary>
		public Vector3 GetBlockCenterPosition (Uzu.VectorI3 blockWorldIndex)
		{
			Vector3 blockCenterOffset = _config.BlockSize / 2.0f;
			return GetBlockBasePosition (blockWorldIndex) + blockCenterOffset;
		}
		
		/// <summary>
		/// Gets the bounding volume of the specified blockWorldIndex.
		/// </summary>
		public void GetBlockBounds (Uzu.VectorI3 blockWorldIndex, out Bounds bounds)
		{
			bounds = new Bounds (GetBlockCenterPosition (blockWorldIndex), _config.BlockSize);
		}
		
		/// <summary>
		/// Gets the type of block at specified blockWorldIndex.
		/// </summary>
		public BlockType GetBlockType (Uzu.VectorI3 blockWorldIndex)
		{
			Chunk chunk;
			Uzu.VectorI3 blockChunkIndex;
			if (GetChunk (blockWorldIndex, out chunk, out blockChunkIndex)) {
				return chunk.GetBlockType (blockChunkIndex);
			}
			return BlockType.EMPTY;
		}
		
		public void SetBlockType (Uzu.VectorI3 blockWorldIndex, BlockType blockType)
		{
			Chunk chunk;
			Uzu.VectorI3 blockChunkIndex;
			if (GetChunk (blockWorldIndex, out chunk, out blockChunkIndex)) {
				chunk.SetBlockType (blockChunkIndex, blockType);
			}
		}
		
		public void SetBlockColor (Uzu.VectorI3 blockWorldIndex, BlockColor blockColor)
		{
			Chunk chunk;
			Uzu.VectorI3 blockChunkIndex;
			if (GetChunk (blockWorldIndex, out chunk, out blockChunkIndex)) {
				chunk.SetBlockColor (blockChunkIndex, blockColor);
			}
		}
		#endregion
	
		#region Collision detection.
		/// <summary>
		/// The results of a detected collision.
		/// </summary>
		public struct CollisionResult
		{
			public Vector3 Position;
			public Vector3 Normal;
			public Uzu.VectorI3 BlockWorldIndex;
		}
		
		/// <summary>
		/// Performs line collision check on block world.
		/// </summary>
		public bool CheckCollision (Vector3 fromPos, Vector3 toPos)
		{
			CollisionResult result;
			return CheckCollision (fromPos, toPos, out result);
		}
			
		/// <summary>
		/// Performs line collision check on block world.
		/// </summary>
		public bool CheckCollision (Vector3 fromPos, Vector3 toPos, out CollisionResult result, bool ignoreEmpty = false)
		{
			result = new CollisionResult ();
			
			Vector3 dir = Vector3.Normalize (toPos - fromPos);
			
			// Get start index and verify within world bounds.
			Uzu.VectorI3 fromBlockWorldIdx = GetBlockWorldIndex (fromPos);
			if (!IsValidWorldBlockIndex (fromBlockWorldIdx)) {
				return false;
			}
			
			// If start index originates in solid box, treat as collision.
			if (ignoreEmpty || GetBlockType (fromBlockWorldIdx) != BlockType.EMPTY) {
				result.BlockWorldIndex = fromBlockWorldIdx;
				result.Position = fromPos;
				result.Normal = Vector3.forward;	// When originating in a solid box, hitNormal has no meaning.
				return true;
			}
			
			// Get end index and clamp to world bounds.
			Uzu.VectorI3 toBlockWorldIdx = GetBlockWorldIndex (toPos);
			toBlockWorldIdx = Uzu.VectorI3.MaxPerElement (toBlockWorldIdx, Uzu.VectorI3.zero);
			
			Uzu.VectorI3 currentBlockWorldIdx = fromBlockWorldIdx;
			Vector3 currentPos = fromPos;
			
			while (true) {
				// We made it to the destination without any collisions.
				if (currentBlockWorldIdx == toBlockWorldIdx) {
					break;
				}
				
				// Get the next block index along direction vector.
				CollisionResult nextResult;
				if (!GetNeighborBlockWorldIndex (currentBlockWorldIdx, currentPos, dir, out nextResult)) {
	#if true
					Debug.Log (fromBlockWorldIdx.ToString ());
					Debug.Log (toBlockWorldIdx.ToString ());
					Debug.LogError ("Infinite loop detected.");
	#endif
					break;
				}
				
				// We've reached the edge of the world and haven't found a collision.
				if (!IsValidWorldBlockIndex (nextResult.BlockWorldIndex)) {
					break;
				}
	
				// Is there a collision with the next block?
				if (ignoreEmpty || GetBlockType (nextResult.BlockWorldIndex) != BlockType.EMPTY) {
					result = nextResult;
					return true;
				}
				
				// Progress to next index.
				currentBlockWorldIdx = nextResult.BlockWorldIndex;
				currentPos = nextResult.Position;
			}
			
			// No collision.
			return false;
		}
		#endregion
		
		/// <summary>
		/// Is the given blockWorldIndex within the world bounds?
		/// </summary>
		public bool IsValidWorldBlockIndex (Uzu.VectorI3 blockWorldIndex)
		{
			return !(Uzu.VectorI3.AnyLower (blockWorldIndex, Uzu.VectorI3.zero));
		}
		
		#region Implementation.
		#region Normalized block faces. Used for collision detection.
		private Vector3[] _xNegFaceVertices;
		private Vector3[] _xPosFaceVertices;
		private Vector3[] _zNegFaceVertices;
		private Vector3[] _zPosFaceVertices;
		private Vector3[] _yNegFaceVertices;
		private Vector3[] _yPosFaceVertices;
		#endregion
		
		/// <summary>
		/// Perform any setup work.
		/// </summary>
		private void InternalInitialize ()
		{
			#region Normalized block faces.
			// Used for collision detection.
			// Gives the normalized coordinates of a block looking from the center point of the block.
			// These coordinates are then offset by a block base position in order to give
			// the actual face coordinates of any block in the world.
			{
				// X
				{
					float blockSizeX = _config.BlockSize.x;
					
					_xNegFaceVertices = new Vector3[] {
						new Vector3 (0.0f, 0.0f, 0.0f),// * blockSizeX,
						new Vector3 (0.0f, 1.0f, 0.0f) * blockSizeX,
						new Vector3 (0.0f, 1.0f, 1.0f) * blockSizeX,
						new Vector3 (0.0f, 0.0f, 1.0f) * blockSizeX,
					};
					_xPosFaceVertices = new Vector3[] {
						new Vector3 (1.0f, 0.0f, 0.0f) * blockSizeX,
						new Vector3 (1.0f, 0.0f, 1.0f) * blockSizeX,
						new Vector3 (1.0f, 1.0f, 1.0f) * blockSizeX,
						new Vector3 (1.0f, 1.0f, 0.0f) * blockSizeX,
					};
				}
				
				// Y
				{
					float blockSizeY = _config.BlockSize.y;
					
					_yNegFaceVertices = new Vector3[] {
						new Vector3 (1.0f, 0.0f, 1.0f) * blockSizeY,
						new Vector3 (1.0f, 0.0f, 0.0f) * blockSizeY,
						new Vector3 (0.0f, 0.0f, 0.0f),// * blockSizeY,
						new Vector3 (0.0f, 0.0f, 1.0f) * blockSizeY,
					};
					_yPosFaceVertices = new Vector3[] {
						new Vector3 (1.0f, 1.0f, 0.0f) * blockSizeY,
						new Vector3 (1.0f, 1.0f, 1.0f) * blockSizeY,
						new Vector3 (0.0f, 1.0f, 1.0f) * blockSizeY,
						new Vector3 (0.0f, 1.0f, 0.0f) * blockSizeY,
					};
				}
				
				// Z
				{
					float blockSizeZ = _config.BlockSize.z;
					
					_zNegFaceVertices = new Vector3[] {
						new Vector3 (1.0f, 0.0f, 0.0f) * blockSizeZ,
						new Vector3 (1.0f, 1.0f, 0.0f) * blockSizeZ,
						new Vector3 (0.0f, 1.0f, 0.0f) * blockSizeZ,
						new Vector3 (0.0f, 0.0f, 0.0f),// * blockSizeZ,
					};
					_zPosFaceVertices = new Vector3[] {
						new Vector3 (0.0f, 0.0f, 1.0f) * blockSizeZ,
						new Vector3 (0.0f, 1.0f, 1.0f) * blockSizeZ,
						new Vector3 (1.0f, 1.0f, 1.0f) * blockSizeZ,
						new Vector3 (1.0f, 0.0f, 1.0f) * blockSizeZ,
					};
				}
			}
			#endregion
		}
		
		private void Update ()
		{		
			// We relegate the updating of chunks to the world (as opposed to each chunk),
			// to allow the world to have control of chunk execution.
			// For example, the world could easily cap the number of chunks
			// that get rebuilt in a single frame, which allows finer control
			// of performance.
			
			// Load chunks.
			{
				ChunkIterator it = new ChunkIterator (_chunksToLoad.GetEnumerator ());
				while (it.MoveNext()) {
					Uzu.VectorI3 chunkIndex = it.CurrentChunkIndex;
					Chunk chunk = it.CurrentChunk;
					
					// Trigger callback.
					BlockWorldChunkLoadContext loadContext = new BlockWorldChunkLoadContext ();
					loadContext.BlockWorld = this;
					loadContext.ChunkIndex = chunkIndex;
					loadContext.Blocks = chunk.Blocks;
					if (_config.OnChunkLoad != null) {
						_config.OnChunkLoad (loadContext);
						chunk.RequestRebuild ();
					}
					
					// Add to active chunk list.
					_activeChunks.Add (chunkIndex, chunk);
				}
				_chunksToLoad.Clear ();
			}
			
			// Rebuild dirty chunks.
			{
				int chunkRebuildCount = 0;
				ChunkIterator it = new ChunkIterator (_activeChunks.GetEnumerator ());
				while (it.MoveNext()) {
					Chunk chunk = it.CurrentChunk;
					if (chunk.DoesNeedRebuild) {					
						chunk.Rebuild ();
						
						// Cap the number of chunks we rebuild this frame.
						chunkRebuildCount++;
						if (chunkRebuildCount == _config.MaxChunkRebuildCountPerFrame) {
							break;
						}
					}
				}
			}
		}
		
		private bool GetNeighborBlockWorldIndex (Uzu.VectorI3 blockWorldIndex, Vector3 startPos, Vector3 dir, out CollisionResult result)
		{
			// If we use "hitPos" as-is, collision checks start to go weird.
			// Could it be cascading an out parameter in C# is causing problems???
			// I have no idea, so I'm changing this variable name and just assigning the result at the end.
			Vector3 hitPos2;
			result = new CollisionResult ();
			
			// Clamp startPos to be within the current block index.
			Bounds blockBounds;
			GetBlockBounds (blockWorldIndex, out blockBounds);
			startPos = Uzu.Math.Clamp (startPos, blockBounds.min, blockBounds.max);
			
			// For the end position, we want a point that is definitely outside of the current block.
			Vector3 endPos = startPos + dir * Uzu.Math.VectorMaxElement (_config.BlockSize) * 2.0f;
			
			Vector3 blockBasePos = GetBlockBasePosition (blockWorldIndex);
			
			while (true) {
				if (dir.x > 0.0f) {
					Vector3 a = blockBasePos + _xPosFaceVertices [0];
					Vector3 b = blockBasePos + _xPosFaceVertices [1];
					Vector3 c = blockBasePos + _xPosFaceVertices [2];
					Vector3 d = blockBasePos + _xPosFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.right;
						result.Normal = Vector3.left;
						break;
					}
				} else if (dir.x < 0.0f) {
					Vector3 a = blockBasePos + _xNegFaceVertices [0];
					Vector3 b = blockBasePos + _xNegFaceVertices [1];
					Vector3 c = blockBasePos + _xNegFaceVertices [2];
					Vector3 d = blockBasePos + _xNegFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.left;
						result.Normal = Vector3.right;
						break;
					}
				}
				
				if (dir.z > 0.0f) {
					Vector3 a = blockBasePos + _zPosFaceVertices [0];
					Vector3 b = blockBasePos + _zPosFaceVertices [1];
					Vector3 c = blockBasePos + _zPosFaceVertices [2];
					Vector3 d = blockBasePos + _zPosFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.forward;
						result.Normal = Vector3.back;
						break;
					}
				} else if (dir.z < 0.0f) {
					Vector3 a = blockBasePos + _zNegFaceVertices [0];
					Vector3 b = blockBasePos + _zNegFaceVertices [1];
					Vector3 c = blockBasePos + _zNegFaceVertices [2];
					Vector3 d = blockBasePos + _zNegFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.back;
						result.Normal = Vector3.forward;
						break;
					}
				}
				
				if (dir.y > 0.0f) {
					Vector3 a = blockBasePos + _yPosFaceVertices [0];
					Vector3 b = blockBasePos + _yPosFaceVertices [1];
					Vector3 c = blockBasePos + _yPosFaceVertices [2];
					Vector3 d = blockBasePos + _yPosFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.up;
						result.Normal = Vector3.down;
						break;
					}
				} else if (dir.y < 0.0f) {
					Vector3 a = blockBasePos + _yNegFaceVertices [0];
					Vector3 b = blockBasePos + _yNegFaceVertices [1];
					Vector3 c = blockBasePos + _yNegFaceVertices [2];
					Vector3 d = blockBasePos + _yNegFaceVertices [3];
					if (Uzu.Math.IntersectLineQuad (startPos, endPos, a, b, c, d, out hitPos2)) {
						result.BlockWorldIndex = blockWorldIndex + Uzu.VectorI3.down;
						result.Normal = Vector3.up;
						break;
					}
				}
				
				// TODO: this case is sometimes happening at edge of world. Need to fix.
				// If we get here, none of the intersection checks passed.
	#if false
				Debug.LogError ("No intersection detected.");
				Debug.Log ("startPos: " + startPos.ToString ());
				Debug.Log ("endPos: " + endPos.ToString ());
				Debug.Log ("blockIdx: " + blockWorldIndex.ToString ());
				Debug.Log ("dir: " + dir.ToString ());
	#endif
				return false;
			}
			
			result.Position = hitPos2;
			return true;
		}
		#endregion
	}
}