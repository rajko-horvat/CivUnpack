using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Disassembler.CPU
{
	public class Memory
	{
		private List<MemoryBlock> aBlocks = new List<MemoryBlock>();
		private List<MemoryRegion> aMemoryRegions = new List<MemoryRegion>();

		public Memory()
		{
		}

		public List<MemoryBlock> Blocks
		{
			get { return aBlocks; } 
		}

		public List<MemoryRegion> MemoryRegions
		{
			get
			{
				return this.aMemoryRegions;
			}
		}

		public bool HasAccess(ushort segment, ushort offset, MemoryFlagsEnum access)
		{
			return this.HasAccess(MemoryRegion.ToLinearAddress(segment, offset), access);
		}

		public bool HasAccess(uint address, MemoryFlagsEnum access)
		{
			for (int i = 0; i < this.aMemoryRegions.Count; i++)
			{
				if (this.aMemoryRegions[i].CheckBounds(address))
				{
					if ((this.aMemoryRegions[i].AccessFlags & access) != access)
						return true;
					else
						return false;
				}
			}

			return false;
		}

		public byte ReadByte(ushort segment, ushort offset)
		{
			return this.ReadByte(MemoryRegion.ToLinearAddress(segment, offset));
		}

		public byte ReadByte(uint address)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Read))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
			}

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address))
				{
					return this.aBlocks[i].ReadByte(address);
				}
			}

			Console.WriteLine("Attempt to read byte at 0x{0:x8}", address);
			return 0;
		}

		public ushort ReadWord(ushort segment, ushort offset)
		{
			return this.ReadWord(MemoryRegion.ToLinearAddress(segment, offset));
		}

		public ushort ReadWord(uint address)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Read))
			{
				Console.WriteLine("Attempt to read from protected area at 0x{0:x8}", address);
				return 0;
			}

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address, 2))
				{
					return this.aBlocks[i].ReadWord(address);
				}
			}

			Console.WriteLine("Attempt to read word at 0x{0:x8}", address);
			return 0;
		}

		public void WriteByte(ushort segment, ushort offset, byte value)
		{
			this.WriteByte(MemoryRegion.ToLinearAddress(segment, offset), value);
		}

		public void WriteByte(uint address, byte value)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Write))
			{
				Console.WriteLine("Attempt to write to protected area at 0x{0:x8}", address);
				return;
			}

			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address))
				{
					this.aBlocks[i].WriteByte(address, value);
					bFound = true;
					break;
				}
			}

			if (!bFound)
				Console.WriteLine("Attempt to write byte 0x{0:x2} at 0x{1:x8}", value, address);
		}

		public void WriteWord(ushort segment, ushort offset, ushort value)
		{
			this.WriteWord(MemoryRegion.ToLinearAddress(segment, offset), value);
		}

		public void WriteWord(uint address, ushort value)
		{
			if (this.HasAccess(address, MemoryFlagsEnum.Write))
			{
				Console.WriteLine("Attempt to write to protected area at 0x{0:x8}", address);
				return;
			}

			bool bFound = false;
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.CheckBounds(address, 2))
				{
					this.aBlocks[i].WriteWord(address, value);
					bFound = true;
					break;
				}
			}

			if (!bFound)
				Console.WriteLine("Attempt to write word 0x{0:x4} at 0x{1:x8}", value, address);
		}

		public void WriteBlock(ushort segment, ushort offset, byte[] srcData, int pos, int length)
		{
			WriteBlock(MemoryRegion.ToLinearAddress(segment, offset), srcData, pos, length);
		}

		public void WriteBlock(uint address, byte[] srcData, int pos, int length) 
		{
			for (int i = 0; i < length; i++)
			{
				WriteByte((uint)(address + i), srcData[pos + i]);
			}
		}

		public bool ResizeBlock(ushort segment, ushort para)
		{
			uint uiAddress = MemoryRegion.ToLinearAddress(segment, 0);
			int iSize = (int)para << 4;

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.Start == uiAddress)
				{
					// check for overlapping
					for (int j = 0; j < this.aBlocks.Count; j++)
					{
						if (j != i && this.aBlocks[j].Region.CheckOverlap(uiAddress, iSize))
							return false;
					}

					// found the block
					this.aBlocks[i].Resize(iSize);
					return true;
				}
			}

			return false;
		}

		public bool AllocateBlock(int size, out ushort segment)
		{
			uint uiFreeMin = 0;
			uint uiFreeMax = 0xb0000;

			// just allocate next available block, don't search between blocks for now
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.End >= uiFreeMin)
				{
					uiFreeMin = this.aBlocks[i].Region.End + 1;
				}
			}

			// make sure that iFreeMin is 16 byte aligned
			MemoryRegion.AlignBlock(ref uiFreeMin);

			// is there enough room for allocation
			if (uiFreeMax - uiFreeMin < size)
			{
				segment = (ushort)(((uiFreeMax - uiFreeMin) >> 4) & 0xffff);
				return false;
			}

			// allocate block
			segment = (ushort)((uiFreeMin >> 4) & 0xffff);
			MemoryBlock mem = new MemoryBlock(uiFreeMin, size);
			this.aBlocks.Add(mem);

			return true;
		}

		public bool AllocateParagraphs(ushort size, out ushort segment)
		{
			int iSize = (int)size << 4;
			uint uiFreeMin = 0;
			uint uiFreeMax = 0xb0000;

			// just allocate next available block, don't search between blocks for now
			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.End >= uiFreeMin)
				{
					uiFreeMin = this.aBlocks[i].Region.End + 1;
				}
			}

			// make sure that iFreeMin is 16 byte aligned
			MemoryRegion.AlignBlock(ref uiFreeMin);

			// is enough room for allocation
			if (uiFreeMax - uiFreeMin < iSize)
			{
				segment = (ushort)(((uiFreeMax - uiFreeMin) >> 4) & 0xffff);
				return false;
			}

			// allocate block
			segment = (ushort)((uiFreeMin >> 4) & 0xffff);
			MemoryBlock mem = new MemoryBlock(uiFreeMin, iSize);
			this.aBlocks.Add(mem);

			return true;
		}

		public bool FreeBlock(ushort segment)
		{
			uint uiAddress = MemoryRegion.ToLinearAddress(segment, 0);

			for (int i = 0; i < this.aBlocks.Count; i++)
			{
				if (this.aBlocks[i].Region.Start == uiAddress)
				{
					// found the block
					this.aBlocks.RemoveAt(i);
					return true;
				}
			}

			return false;
		}
	}
}
