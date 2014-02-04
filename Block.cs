using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// All system block types.
	/// 
	/// To customize in the application:
	/// 
	/// 1). For applications that only need two block types
	/// 	(empty and solid), just use Uzu.BlockType as-is,
	/// 
	/// 2). For applications that have more than two block types,
	/// 	new block types can be defined like so:
	/// 
	/// 	public enum MyBlockType : byte
	/// 	{
	/// 		MY_BLOCK_TYPE_0 = Uzu.BlockType.USER_START_VALUE,
	/// 		MY_BLOCK_TYPE_1,
	/// 		...
	/// 	}
	/// 	
	/// 	The EMPTY block type will always be defined by the system.
	/// 	If user block types are defined, the default system SOLID
	/// 	block type will be overwritten by the first user block type.
	/// </summary>
	public enum BlockType : byte
	{
		/// <summary>
		/// An empty block.
		/// </summary>
		EMPTY = 0,
		/// <summary>
		/// A basic non-empty block.
		/// </summary>
		SOLID,
		/// <summary>
		/// The first user-defined block type start value.
		/// </summary>
		USER_START_VALUE = EMPTY + 1,
	};
	
	/// <summary>
	/// A block description.
	/// One description is stored for each block type.
	/// </summary>
	public class BlockDesc
	{
		public Material Material { get; set; }
		
		/// <summary>
		/// Allows user-specified ignore faces to be set on a per-block-type basis.
		/// For example, if BlockFaceFlag.Back is specified, all blocks of this
		/// type will have their back faces (z-axis) ignored.
		/// Depending on the type of game, this is useful if the movement directions
		/// of the player are limited.
		/// </summary>
		public BlockFaceFlag IgnoreFaces { get; set; }
	}
	
	/// <summary>
	/// A single block as represented in the block world.
	/// Beward of adding more members to this, as memory usage will explode ^3.
	/// </summary>
	public class Block
	{
		public BlockType Type { get; set; }
	
		public Color32 Color { get; set; }
		
		/// <summary>
		/// Is this block an "EMPTY" block type?
		/// </summary>
		public bool IsEmpty { get { return Type == BlockType.EMPTY; } }
	}
	
	/// <summary>
	/// Helper class for managing configurations of blocks.
	/// </summary>
	public class BlockContainer
	{	
		public BlockContainer (VectorI3 countXYZ)
		{
			_countXYZ = countXYZ;
			_count = VectorI3.ElementProduct (_countXYZ);
			_countYZ = _countXYZ.y * _countXYZ.z;
			_blocks = new Block[_count];
			
			// Initialize.
			for (int i = 0; i < _count; i++) {
				_blocks [i] = new Block ();
			}
		}
		
		public VectorI3 CountXYZ {
			get { return _countXYZ; }
		}
		
		public int Count {
			get { return _count; }
		}
	
		public Block this [int i] {
			get { return _blocks [i]; }
			set { _blocks [i] = value; }
		}
		
		/// <summary>
		/// Allows indexing into the container using (x,y,z) tuple.
		/// Indexing via a tuple requires the calculation of the index into
		/// the buffer. Because of this overhead, use flat indexing (blocks[i])
		/// whenever possible.
		/// </summary>
		public Block this [int x, int y, int z] {
			get {
				int index = x * _countYZ + y * _countXYZ.z + z;
				return _blocks [index];
			}
			set {
				int index = x * _countYZ + y * _countXYZ.z + z;
				_blocks [index] = value;
			}
		}
		
		/// <summary>
		/// Resets all blocks in this container
		/// to their 'empty' state.
		/// </summary>
		public void ResetAllBlocks ()
		{
			for (int i = 0; i < _count; i++) {
				_blocks [i].Type = BlockType.EMPTY;
			}
		}
		
		private Block[] _blocks;
		private VectorI3 _countXYZ;
		private int _count;
		private int _countYZ;
	}
	
	/// <summary>
	/// Used for ignoring block faces, and other
	/// operation the require definition of block (cube) faces.
	/// </summary>
	[System.Flags]
	public enum BlockFaceFlag : byte
	{
		Front =  1 << 0,
		Back =	 1 << 1,
		Right =  1 << 2,
		Left =	 1 << 3,
		Top =	 1 << 4,
		Bottom = 1 << 5,
	}
}