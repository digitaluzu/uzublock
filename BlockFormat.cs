﻿using UnityEngine;
using System.Collections;

namespace Uzu
{
	public static class BlockFormat
	{
		public const int CURRENT_VERSION = 1;

		static public string Extension {
			get { return "blk"; }
		}

		public class Header
		{
			public int version;
			public VectorI3 count;
		}

		public class Data
		{
			public bool[] _states;
			public RGB[] _colors;
		}

		public struct RGB
		{
			public byte r;
			public byte g;
			public byte b;

			public RGB (Color32 color)
			{
				r = color.r;
				g = color.g;
				b = color.b;
			}
			
			public RGB (byte inR, byte inG, byte inB)
			{
				r = inR;
				g = inG;
				b = inB;
			}

			public Color32 ToColor32 ()
			{
				return new Color32 (r, g, b, 255);
			}
		}
	}
}