using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// Configuration for the block world controller.
	/// </summary>
	public struct BlockWorldControllerConfig
	{
		public BlockWorld TargetBlockWorld { get; set; }
	
		public Uzu.VectorI3 LoadedChunkCount { get; set; }
	}
	
	/// <summary>
	/// Controls block world loading and unloading management.
	/// 
	/// TODO:
	///  - expand boundary with overlapping to prevent any floating point flickering on chunk borders.
	/// </summary>
	public class BlockWorldController : Uzu.BaseBehaviour
	{
		/// <summary>
		/// Gets a copy of the config used to originally initialize this block world controller.
		/// </summary>
		public BlockWorldControllerConfig Config {
			get { return _config; }
		}
		
		/// <summary>
		/// Perform initialization.
		/// </summary>
		public void Initialize (BlockWorldControllerConfig config)
		{
	#if UNITY_EDITOR
			if (Uzu.VectorI3.AnyEqual (config.LoadedChunkCount, Uzu.VectorI3.zero)) {
				Debug.LogWarning ("Zero dimension LoadedChunkCount. No chunks will be loaded.");
			}
	#endif

			// If the object is being re-initialized, clear current contents.
			if (_loadedChunks != null) {
				ForceReload ();
			}
			
			_isDirty = true;
			_config = config;
			_loadedChunks = new Uzu.FixedList<Uzu.VectorI3> (Uzu.VectorI3.ElementProduct (_config.LoadedChunkCount));
		}

		private BlockWorldControllerConfig _config;
		private bool _isDirty;
		private Vector3 _currentPosition;
		private Uzu.FixedList<Uzu.VectorI3> _loadedChunks;
		private Uzu.VectorI3? _previousMinChunkIndex = null;
	
		public Vector3 CurrentPosition {
			get { return _currentPosition; }
			set {
				// Ignore update if coordinates are nearby.
				Vector3 newPosition = value;
				if (Mathf.Approximately (Vector3.SqrMagnitude (newPosition - _currentPosition), 0.0f)) {
					return;
				}
				
				_isDirty = true;
				_currentPosition = newPosition;
			}
		}
		
		/// <summary>
		/// Forces current active blocks to be unloaded and then reloaded.
		/// </summary>
		public void ForceReload ()
		{
			_isDirty = true;

			// Perform unloading immediately to prevent any weird update
			// order bugs between BlockWorldController and BlockWorld.
			{
				BlockWorld blockWorld = _config.TargetBlockWorld;

				for (int i = 0; i < _loadedChunks.Count; i++) {
					Uzu.VectorI3 chunkIndex = _loadedChunks [i];
					blockWorld.UnloadChunk (chunkIndex);
				}
				_loadedChunks.Clear ();
				_previousMinChunkIndex = null;
			}
		}
		
		private void Update ()
		{
			// Dirty flag.
			{
				// No need to update.
				if (!_isDirty) {
					return;
				}
				_isDirty = false;
			}
			
			BlockWorld blockWorld = _config.TargetBlockWorld;
			
			Uzu.VectorI3 loadedChunkCount = _config.LoadedChunkCount;
			Uzu.VectorI3 minChunkIndex = GetLoadedMinChunkIndex ();
			Uzu.VectorI3 maxChunkIndex = minChunkIndex + loadedChunkCount - Uzu.VectorI3.one;
			
			// Unload.
			{
				if (_previousMinChunkIndex.HasValue && _previousMinChunkIndex.Value != minChunkIndex) {
					for (int x = 0; x < loadedChunkCount.x; x++) {
						for (int y = 0; y < loadedChunkCount.y; y++) {
							for (int z = 0; z < loadedChunkCount.z; z++) {
								Uzu.VectorI3 chunkIndex = _previousMinChunkIndex.Value + new Uzu.VectorI3 (x, y, z);
								
								// If previous index is not within newest index range, unload.
								if (Uzu.VectorI3.AnyLower (chunkIndex, minChunkIndex) ||
									Uzu.VectorI3.AnyGreater (chunkIndex, maxChunkIndex)) {
									int index = _loadedChunks.FindIndex (chunkIndex);
									if (index != _loadedChunks.Count) {
										blockWorld.UnloadChunk (chunkIndex);
										_loadedChunks.RemoveAt (index);
									}
								}
							}
						}
					}
				}
			}
			
			// Load.
			{
				Uzu.VectorI3 worldMinChunkIndex = Vector3.zero;
				Uzu.VectorI3 prevMaxChunkIndex = _previousMinChunkIndex.HasValue ? (_previousMinChunkIndex.Value + loadedChunkCount - Uzu.VectorI3.one) : Uzu.VectorI3.zero;
				for (int x = 0; x < loadedChunkCount.x; x++) {
					for (int y = 0; y < loadedChunkCount.y; y++) {
						for (int z = 0; z < loadedChunkCount.z; z++) {
							Uzu.VectorI3 chunkIndex = minChunkIndex + new Uzu.VectorI3 (x, y, z);
							
							// Only process indices within world bounds.
							if (Uzu.VectorI3.AnyLower (chunkIndex, worldMinChunkIndex)) {
								continue;
							}
							
							// If new index was not within previous range, load.
							if (!_previousMinChunkIndex.HasValue ||
								Uzu.VectorI3.AnyLower (chunkIndex, _previousMinChunkIndex.Value) ||
								Uzu.VectorI3.AnyGreater (chunkIndex, prevMaxChunkIndex)) {
								blockWorld.LoadChunk (chunkIndex);
								_loadedChunks.Add (chunkIndex);
							}
						}
					}
				}
			}

			_previousMinChunkIndex = minChunkIndex;
		}
		
		private Uzu.VectorI3 GetChunkIndex (Vector3 worldPos)
		{
			BlockWorld blockWorld = _config.TargetBlockWorld;
			Vector3 chunkSize = blockWorld.Config.ChunkSizeInBlocks * blockWorld.Config.BlockSize;
			Vector3 relPos = worldPos - CachedXform.position;
			
			// Handles rounding for negative indices.
			if (relPos.x < 0.0f) {
				relPos.x -= chunkSize.x;
			}
			if (relPos.y < 0.0f) {
				relPos.y -= chunkSize.y;
			}
			if (relPos.z < 0.0f) {
				relPos.z -= chunkSize.z;
			}
			
			return new Uzu.VectorI3 (relPos.x / chunkSize.x, relPos.y / chunkSize.y, relPos.z / chunkSize.z);
		}
		
		private Uzu.VectorI3 GetLoadedMinChunkIndex ()
		{
			Uzu.VectorI3 loadedChunkCount = _config.LoadedChunkCount;
			Uzu.VectorI3 centerChunkIndex = GetChunkIndex (CurrentPosition);
			return centerChunkIndex - new Uzu.VectorI3 (loadedChunkCount.x / 2, loadedChunkCount.y / 2, loadedChunkCount.z / 2);
		}
	}
}
