using UnityEngine;
using System.Collections.Generic;

namespace Uzu
{
	/// <summary>
	/// All block types.
	/// </summary>
	public enum BlockType : byte
	{
		EMPTY = 0,
		
		/// <summary>
		/// A solid block, that when touched, causes death.
		/// </summary>
		SOLID,
		
		MAX_COUNT,
	};
	
	public struct BlockColor
	{
		public Color32 Color {
			get;
			set;
		}
		
		public BlockColor (Color color)
		{
			Color = color;
		}
		
		public static implicit operator BlockColor (Color color)
		{
			return new BlockColor (color);
		}
		
		public static bool operator == (BlockColor lhs, BlockColor rhs)
		{
			Color32 lc = lhs.Color;
			Color32 rc = rhs.Color;
			return lc.r == rc.r && lc.g == rc.g && lc.b == rc.b && lc.a == rc.a;
		}
		
		public static bool operator != (BlockColor lhs, BlockColor rhs)
		{
			return !(lhs == rhs);
		}
		
		public override bool Equals (object obj)
		{
			if (obj.GetType () == typeof(BlockColor)) {
				BlockColor c = (BlockColor)obj;
				return this == c;
			}
	
			return false;
		}
		
		public override int GetHashCode ()
		{
			return base.GetHashCode ();
		}
	};
	
	/// <summary>
	/// A block description.
	/// One description is stored for each block type.
	/// </summary>
	[System.Serializable]
	public class BlockDesc
	{
		[SerializeField]
		private Color _color;
		[SerializeField]
		private Material _material;
		
		public Color Color { get { return _color; } set { _color = value; } }
	
		public Material Material { get { return _material; } set { _material = value; } }
		
		/// <summary>
		/// Allows user-specified ignore faces to be set on a per-block-type basis.
		/// For example, if CyBlockFaceFlag.Back is specified, all blocks of this
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
	
		public BlockColor Color { get; set; }
		
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
		public BlockContainer (Uzu.VectorI3 countXYZ)
		{
			_countXYZ = countXYZ;
			_count = Uzu.VectorI3.ElementProduct (_countXYZ);
			_countYZ = _countXYZ.y * _countXYZ.z;
			_blocks = new Block[_count];
			
			// Initialize.
			for (int i = 0; i < _count; i++) {
				_blocks [i] = new Block ();
			}
		}
		
		public Uzu.VectorI3 CountXYZ {
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
		private Uzu.VectorI3 _countXYZ;
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