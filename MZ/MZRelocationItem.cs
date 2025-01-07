using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler.MZ
{
	/// <summary>
	/// MZ relocation entry
	/// </summary>
	public struct MZRelocationItem
	{
		private ushort usSegment;
		private ushort usOffset;

		public MZRelocationItem(ushort segment, ushort offset)
		{
			this.usSegment = segment;
			this.usOffset = offset;
		}

		/// <summary>
		/// The segment of the relocated address
		/// </summary>
		public ushort Segment
		{
			get => this.usSegment;
		}

		/// <summary>
		/// The offset of the relocated address
		/// </summary>
		public ushort Offset
		{
			get => this.usOffset;
		}

		public uint AbsoluteAddress
		{
			get => (uint)(((uint)this.usSegment << 4) + this.usOffset);
		}

		public override string ToString()
		{
			return $"{this.usSegment:x4}:{this.usOffset:x4}";
		}
	}
}
