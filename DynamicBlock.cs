using UnityEngine;
using System.Collections;

namespace Uzu
{
	/// <summary>
	/// Config for the creation of a single dynamic block.
	/// </summary>
	public struct DynamicBlockCreationConfig
	{
		public Vector3 StartPosition { get; set; }
		
		public Vector3 EndPosition { get; set; }
	
		public Vector3 StartScale { get; set; }
	
		public Vector3 EndScale { get; set; }
	
		public Color StartColor { get; set; }
	
		public Color EndColor { get; set; }
	
		public float Duration { get; set; }
		
		public DynamicBlockEvent.OnDynamicBlockDieDelegate OnDynamicBlockDie { get; set; }
	
		public DynamicBlockEvent.DynamicBlockDieContext DynamicBlockDieContext { get; set; }
	}
	
	/// <summary>
	/// Events for the lifetime of a single dynamic block.
	/// </summary>
	public static class DynamicBlockEvent
	{
		/// <summary>
		/// Callback that is triggered when a particle dies.
		/// </summary>
		public delegate void OnDynamicBlockDieDelegate (DynamicBlockDieContext userData);
		
		/// <summary>
		/// Derive from this in order to pass in your own paremters.
		/// </summary>
		public class DynamicBlockDieContext
		{
			
		}
	}
}