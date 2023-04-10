using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.CPU
{
	[Flags]
	public enum MemoryFlagsEnum
	{
		None = 0,
		Write = 1,
		Read = 2
	}

	public class MemoryRegion
	{
		private uint iStart;
		private int iSize;
		private MemoryFlagsEnum eAccessFlags;

		public MemoryRegion(ushort segment, ushort offset, int size)
			: this(MemoryRegion.ToLinearAddress(segment, offset), size, MemoryFlagsEnum.None)
		{
		}

		public MemoryRegion(ushort segment, ushort offset, int size, MemoryFlagsEnum access)
			: this(MemoryRegion.ToLinearAddress(segment, offset), size, access)
		{
		}

		public MemoryRegion(uint start, int size)
			: this(start, size, MemoryFlagsEnum.None)
		{
		}

		public MemoryRegion(uint start, int size, MemoryFlagsEnum access)
		{
			this.iStart = start;
			this.iSize = size;
			this.eAccessFlags = access;
		}

		public MemoryFlagsEnum AccessFlags
		{
			get { return eAccessFlags; }
			set { eAccessFlags = value; }
		}

		public uint Start
		{
			get
			{
				return this.iStart;
			}
		}

		public int Size
		{
			get
			{
				return this.iSize;
			}
		}

		public uint End
		{
			get
			{
				return (uint)(this.iStart + this.iSize - 1);
			}
		}

		public bool CheckBounds(ushort segment, ushort offset)
		{
			return this.CheckBounds(MemoryRegion.ToLinearAddress(segment, offset), 1);
		}

		public bool CheckBounds(ushort segment, ushort offset, int size)
		{
			return this.CheckBounds(MemoryRegion.ToLinearAddress(segment, offset), size);
		}

		public bool CheckBounds(uint address)
		{
			return this.CheckBounds(address, 1);
		}

		public bool CheckBounds(uint address, int size)
		{
			if (address >= this.iStart && address + size - 1 < this.iStart + this.iSize)
			{
				return true;
			}

			return false;
		}

		public bool CheckOverlap(ushort segment, ushort offset, int size)
		{
			return this.CheckOverlap(MemoryRegion.ToLinearAddress(segment, offset), size);
		}

		public bool CheckOverlap(uint address, int size)
		{
			if (address >= this.iStart || address < this.iStart + this.iSize ||
				(address + size - 1) >= this.iStart || (address + size - 1) < this.iStart + this.iSize)
			{
				return true;
			}
			return false;
		}

		public uint MapAddress(ushort segment, ushort offset)
		{
			return this.MapAddress(MemoryRegion.ToLinearAddress(segment, offset));
		}

		public uint MapAddress(uint address)
		{
			return (uint)(address - this.iStart);
		}

		public static uint ToLinearAddress(ushort segment, ushort offset)
		{
			// 1MB limit!
			return ((uint)((uint)segment << 4) + (uint)offset) & 0xfffff;
		}

		public static void AlignBlock(ref uint address)
		{
			if ((address & 0xf) != 0)
			{
				address &= 0xffff0;
				address += 0x10;
			}
		}
	}
}
