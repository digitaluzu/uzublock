using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// Config for the creation of a dynamic block spawner.
	/// </summary>
	public struct DynamicBlockSpawnerConfig
	{
		/// <summary>
		/// The material to be used by the dynamic blocks.
		/// </summary>
		public Material Material { get; set; }
	
		/// <summary>
		/// The maximum number of dynamic blocks that will be managed.
		/// </summary>
		public int MaxBlockCount { get; set; }
	}

	/// <summary>
	/// Manages 'dynamic' blocks.
	/// Blocks are grouped together in a single mesh for performance.
	/// </summary>
	[RequireComponent(typeof(MeshFilter))]
	[RequireComponent(typeof(MeshRenderer))]
	public class DynamicBlockSpawner : BaseBehaviour
	{	
		/// <summary>
		/// Perform initialization.
		/// </summary>
		public void Initialize (DynamicBlockSpawnerConfig config)
		{
			// Assign material.
			MeshRenderer meshRenderer = GetComponent<MeshRenderer> ();
			Destroy (meshRenderer.material);
			meshRenderer.material = config.Material;
		
			// Mesh buffer creation.
			{
				ChunkMeshCreationConfig meshConfig = new ChunkMeshCreationConfig ();
				meshConfig.MaxVisibileFaceCount = config.MaxBlockCount * FACE_COUNT_PER_BLOCK;
				_meshDesc = new ChunkMeshDesc (meshConfig);
				_meshIndicesDesc = new ChunkSubMeshDesc (meshConfig);
			}
		
			// Block desc allocation.
			{
				_activeBlockDescs = new FixedList<DynamicBlockDesc> (config.MaxBlockCount);
				_availableBlockDescs = new FixedList<DynamicBlockDesc> (config.MaxBlockCount);
				
				for (int i = 0; i < _availableBlockDescs.Capacity; i++) {
					_availableBlockDescs.Add (new DynamicBlockDesc ());
				}
			}
		}
	
		/// <summary>
		/// Spawns a new dynamic block.
		/// </summary>
		public void SpawnBlock (DynamicBlockCreationConfig config)
		{
			if (_availableBlockDescs.Count == 0) {
				Debug.LogWarning ("Cannot spawn any more dynamic blocks. Capacity [" + _activeBlockDescs.Capacity + "] reached.");
				return;
			}
			
			// Get an available block from the pool.
			DynamicBlockDesc blockDesc;
			{
				int lastIndex = _availableBlockDescs.Count - 1;
				blockDesc = _availableBlockDescs [lastIndex];
				_availableBlockDescs.RemoveAt (lastIndex);
			}
			
			Dbg.Assert (blockDesc != null);
			
			// Set up the desc.
			{
				blockDesc.Config = config;
				blockDesc.ElapsedTime = 0.0f;
			}
			
			// Activate.
			_activeBlockDescs.Add (blockDesc);
		}
		
		/// <summary>
		/// Clears all dynamic blocks.
		/// </summary>
		public void ClearBlocks ()
		{
			_mesh.Clear ();
			_meshDesc.Clear ();
			_meshIndicesDesc.Clear ();
			_meshIndicesDesc.FillToCapacity ();
			
			// Reset block descs - return to available pool.
			{
				for (int i = 0; i < _activeBlockDescs.Count; i++) {
					_availableBlockDescs.Add (_activeBlockDescs [i]);
				}
				_activeBlockDescs.Clear ();
			}
		}
			
		#region Implementation.
		private const int FACE_COUNT_PER_BLOCK = 6;
		private Mesh _mesh;
		private ChunkMeshDesc _meshDesc;
		private ChunkSubMeshDesc _meshIndicesDesc;
		private FixedList<DynamicBlockDesc> _activeBlockDescs;
		private FixedList<DynamicBlockDesc> _availableBlockDescs;
		
		private class DynamicBlockDesc
		{
			public DynamicBlockCreationConfig Config { get; set; }

			public float ElapsedTime { get; set; }
		}
		
		private void Update ()
		{
			// Skip all processing if there are no active blocks.
			if (_activeBlockDescs.Count == 0) {
				return;
			}

			// Clear the mesh for rebuilding.
			{
				_mesh.Clear ();
				_meshDesc.Clear ();
				_meshIndicesDesc.Clear ();
			}
		
			// Rebuild the mesh for all the active blocks.
			{
				for (int i = 0; i < _activeBlockDescs.Count; /*i++*/) {
					DynamicBlockDesc blockDesc = _activeBlockDescs [i];
	
					// Expired block?
					if (blockDesc.ElapsedTime >= blockDesc.Config.Duration) {
						// Deactivate.
						{
							// Remove from active list - swap with last item.
							{
								int lastIndex = _activeBlockDescs.Count - 1;
								_activeBlockDescs [i] = _activeBlockDescs [lastIndex];
								_activeBlockDescs.RemoveAt (lastIndex);
							}
							
							// Move back to available pool.
							_availableBlockDescs.Add (blockDesc);
						}
						
						// Trigger callback.
						if (blockDesc.Config.OnDynamicBlockDie != null) {
							blockDesc.Config.OnDynamicBlockDie (blockDesc.Config.DynamicBlockDieContext);
						}
					
						continue;
					}
				
					// Add the block to the mesh.
					CreateBlock (_meshDesc, _meshIndicesDesc, blockDesc);
				
					// Progress time.
					blockDesc.ElapsedTime += Time.deltaTime;
					
					i++;
				}
			}
		
			// Mesh creation.
			{
				// Fill arrays out with default data so that no garbage remains.
				{
					_meshIndicesDesc.FillToCapacity ();
				}
			
				_mesh.vertices = _meshDesc.VertexList.ToArray ();
				_mesh.normals = _meshDesc.NormalList.ToArray ();
				_mesh.colors32 = _meshDesc.ColorList.ToArray ();
				_mesh.uv = _meshDesc.UVList.ToArray ();
			
				_mesh.triangles = _meshIndicesDesc.IndexList.ToArray ();
			
				_mesh.Optimize ();
			}
		}
	
		private static void CreateBlock (ChunkMeshDesc meshDesc, ChunkSubMeshDesc meshIndicesDesc, DynamicBlockDesc blockDesc)
		{
			DynamicBlockCreationConfig config = blockDesc.Config;
		
			float t = blockDesc.ElapsedTime / config.Duration;
		
			Vector3 size = config.StartScale * (1.0f - t) + config.EndScale * t;
			Vector3 centerPos = config.StartPosition * (1.0f - t) + config.EndPosition * t;
			Vector3 pos = centerPos - (size * 0.5f);
			
			FixedList<Vector3> vList = meshDesc.VertexList;
			FixedList<Vector3> nList = meshDesc.NormalList;
			FixedList<Color32> cList = meshDesc.ColorList;
			FixedList<Vector2> uvList = meshDesc.UVList;
			FixedList<int> iList = meshIndicesDesc.IndexList;
			
			// Vertices.
			Vector3 v0 = new Vector3 (pos.x, pos.y, pos.z);
			Vector3 v1 = new Vector3 (pos.x + size.x, pos.y, pos.z);
			Vector3 v2 = new Vector3 (pos.x + size.x, pos.y + size.y, pos.z);
			Vector3 v3 = new Vector3 (pos.x, pos.y + size.y, pos.z);
			Vector3 v4 = new Vector3 (pos.x + size.x, pos.y, pos.z + size.z);
			Vector3 v5 = new Vector3 (pos.x, pos.y, pos.z + size.z);
			Vector3 v6 = new Vector3 (pos.x, pos.y + size.y, pos.z + size.z);
			Vector3 v7 = new Vector3 (pos.x + size.x, pos.y + size.y, pos.z + size.z);
		
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
		
			// Color.
			Color blockColor = (config.StartColor * (1.0f - t)) + (config.EndColor * t);
		
			// Starting index offset (offset by # of vertices).
			int baseIdx = vList.Count;
		
			#region Polygon construction.
			// Front.
			{
				vList.Add (v0);
				vList.Add (v1);
				vList.Add (v2);
				vList.Add (v3);
			
				nList.Add (front);
				nList.Add (front);
				nList.Add (front);
				nList.Add (front);
			
				iList.Add (baseIdx + 3);
				iList.Add (baseIdx + 1);
				iList.Add (baseIdx + 0);
			
				iList.Add (baseIdx + 3);
				iList.Add (baseIdx + 2);
				iList.Add (baseIdx + 1);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Right.
			{
				vList.Add (v1);
				vList.Add (v4);
				vList.Add (v7);
				vList.Add (v2);
			
				nList.Add (right);
				nList.Add (right);
				nList.Add (right);
				nList.Add (right);
			
				iList.Add (baseIdx + 7);
				iList.Add (baseIdx + 5);
				iList.Add (baseIdx + 4);
			
				iList.Add (baseIdx + 7);
				iList.Add (baseIdx + 6);
				iList.Add (baseIdx + 5);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Back.
			{
				vList.Add (v4);
				vList.Add (v5);
				vList.Add (v6);
				vList.Add (v7);
			
				nList.Add (back);
				nList.Add (back);
				nList.Add (back);
				nList.Add (back);
			
				iList.Add (baseIdx + 11);
				iList.Add (baseIdx + 9);
				iList.Add (baseIdx + 8);
			
				iList.Add (baseIdx + 11);
				iList.Add (baseIdx + 10);
				iList.Add (baseIdx + 9);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Left.
			{
				vList.Add (v5);
				vList.Add (v0);
				vList.Add (v3);
				vList.Add (v6);
			
				nList.Add (left);
				nList.Add (left);
				nList.Add (left);
				nList.Add (left);
			
				iList.Add (baseIdx + 15);
				iList.Add (baseIdx + 13);
				iList.Add (baseIdx + 12);
			
				iList.Add (baseIdx + 15);
				iList.Add (baseIdx + 14);
				iList.Add (baseIdx + 13);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Top.
			{
				vList.Add (v3);
				vList.Add (v2);
				vList.Add (v7);
				vList.Add (v6);
			
				nList.Add (up);
				nList.Add (up);
				nList.Add (up);
				nList.Add (up);
			
				iList.Add (baseIdx + 19);
				iList.Add (baseIdx + 17);
				iList.Add (baseIdx + 16);
			
				iList.Add (baseIdx + 19);
				iList.Add (baseIdx + 18);
				iList.Add (baseIdx + 17);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Bottom.
			{
				vList.Add (v5);
				vList.Add (v4);
				vList.Add (v1);
				vList.Add (v0);
			
				nList.Add (down);
				nList.Add (down);
				nList.Add (down);
				nList.Add (down);
			
				iList.Add (baseIdx + 23);
				iList.Add (baseIdx + 21);
				iList.Add (baseIdx + 20);
			
				iList.Add (baseIdx + 23);
				iList.Add (baseIdx + 22);
				iList.Add (baseIdx + 21);
			
				uvList.Add (uv00);
				uvList.Add (uv10);
				uvList.Add (uv11);
				uvList.Add (uv01);
			}
		
			// Set colors per vertex.
			const int vertCount = 4 * FACE_COUNT_PER_BLOCK;
			for (int i = 0; i < vertCount; i++) {
				cList.Add (blockColor);
			}
			#endregion
		}
	
		protected override void Awake ()
		{
			base.Awake ();
		
			// Get the mesh for rendering.
			{
				_mesh = new Mesh ();
				MeshFilter meshFilter = GetComponent<MeshFilter> ();
				meshFilter.mesh = _mesh;
			
				_mesh.MarkDynamic ();
			}
		}
		#endregion
	}
}