using Disassembler;
using Disassembler.CPU;
using Disassembler.MZ;

internal class Program
{
	private static void Main(string[] args)
	{
		if (File.Exists("civ.exe"))
		{
			if (!File.Exists("civ.bak"))
			{
				File.Move("civ.exe", "civ.bak");
				UnpackDOSEXE("civ.bak");
			}
			else
			{
				Console.WriteLine("Will not overwrite civ.bak");
			}
		}
		else
		{
			Console.WriteLine("CIV.EXE does not exist. Put CivUnpack.exe in a directory with CIV.EXE and run it.");
		}
	}

	private static void UnpackDOSEXE(string path)
	{
		MZExecutable mzEXE = new MZExecutable(path);
		ushort usSegment1 = 0x5409;
		ushort usSegment2 = 0x1234;

		CPURegisters oRegisters1 = new CPURegisters();
		Memory oMemory1 = UnpackEXE(mzEXE, usSegment1, oRegisters1);

		CPURegisters oRegisters2 = new CPURegisters();
		Memory oMemory2 = UnpackEXE(mzEXE, usSegment2, oRegisters2);

		byte[] buffer1 = oMemory1.Blocks[3].Data;
		byte[] buffer2 = oMemory2.Blocks[3].Data;
		int iLength1 = buffer1.Length;
		int iLength2 = buffer1.Length;
		uint uiLastEmptyByte = 0x652d; // As defined by EXE startup code; (uint)(iLength1 - 1);

		if (iLength1 != iLength2)
			throw new Exception("Blocks are of different size");

		/*for (int i = iLength1 - 1; i >= 0; i--)
		{
			if (buffer1[i] != 0 || buffer2[i] != 0)
			{
				uiLastEmptyByte = (uint)(i + 1);
				break;
			}
		}*/

		Console.WriteLine("Last empty byte in the last block: 0x{0:x8}", uiLastEmptyByte);
		MemoryRegion.AlignBlock(ref uiLastEmptyByte); // we want this to be aligned

		Console.WriteLine("Joining the blocks");
		iLength1 = oMemory1.Blocks[2].Data.Length;
		iLength2 = (int)(oMemory2.Blocks[2].Data.Length + uiLastEmptyByte);
		oMemory1.Blocks[2].Resize(iLength2);
		oMemory2.Blocks[2].Resize(iLength2);

		Array.Copy(oMemory1.Blocks[3].Data, 0, oMemory1.Blocks[2].Data, iLength1, uiLastEmptyByte);
		oMemory1.Blocks.RemoveAt(3);
		Array.Copy(oMemory2.Blocks[3].Data, 0, oMemory2.Blocks[2].Data, iLength1, uiLastEmptyByte);
		oMemory2.Blocks.RemoveAt(3);

		buffer1 = oMemory1.Blocks[2].Data;
		buffer2 = oMemory2.Blocks[2].Data;

		// decrease minimum allocation by additional size that was added to EXE
		mzEXE.MinimumAllocation -= (ushort)(uiLastEmptyByte >> 4);

		CompareBlocksAndReconstructEXE(mzEXE, buffer1, buffer2, usSegment1, usSegment2);
		mzEXE.InitialSS = (ushort)(oRegisters1.SS.Word - usSegment1);
		mzEXE.InitialSP = oRegisters1.SP.Word;
		mzEXE.InitialIP = oRegisters1.IP.Word;
		mzEXE.InitialCS = (ushort)(oRegisters1.CS.Word - usSegment1); // - 0x10; // account for PSP (0x10)

		Console.WriteLine("Block validation passed");

		// write new EXE

		mzEXE.WriteToFile("civ.exe");
	}

	private static void CompareBlocksAndReconstructEXE(MZExecutable exe, byte[] block1, byte[] block2, ushort segment1, ushort segment2)
	{
		if (block1.Length != block2.Length)
		{
			Console.WriteLine("Block 1 length is not equal to Block 2 length");
			return;
		}

		// also, reconstruct relocation items
		int iSegment = 0;
		int iOffset = 0;

		exe.Relocations.Clear();
		
		// we are comparing words
		for (int i = 0; i < block1.Length; i++)
		{
			if (block1[i] != block2[i])
			{
				if (block1[i + 1] != block2[i + 1])
				{
					// compare absolute offsets, not relative ones
					ushort usWord1 = (ushort)((ushort)block1[i] | (ushort)((ushort)block1[i + 1] << 8));
					usWord1 -= segment1;
					//usWord1 -= 0x10; // account for PSP (0x10)
					ushort usWord2 = (ushort)((ushort)block2[i] | (ushort)((ushort)block2[i + 1] << 8));
					usWord2 -= segment2;
					//usWord2 -= 0x10; // account for PSP (0x10)

					if (usWord1 != usWord2)
					{
						Console.WriteLine("Segment 0x{0:x4} not equal to 0x{1:x4} at 0x{2:x4}", usWord1, usWord2, i);
					}
					block1[i] = (byte)(usWord1 & 0xff);
					block1[i + 1] = (byte)((usWord1 & 0xff00) >> 8);
					block2[i] = (byte)(usWord2 & 0xff);
					block2[i + 1] = (byte)((usWord2 & 0xff00) >> 8);
					exe.Relocations.Add(new MZRelocationItem((ushort)iSegment, (ushort)iOffset));
					iOffset++;
					i++;
				}
				else
				{
					Console.WriteLine("Block 1 word is not equal in size to Block 2 at 0x{0:x4}", i);
					return;
				}
			}
			iOffset++;
			if (iOffset > 0xffff)
			{
				iSegment += 0x1000;
				iOffset -= 0x10000;
			}
		}

		exe.Data = new byte[block1.Length];
		Array.Copy(block1, exe.Data, block1.Length);
	}

	private static Memory UnpackEXE(MZExecutable exe, ushort startSegment, CPURegisters r)
	{
		Memory oMemory = new Memory();
		ushort usPSPSegment;
		ushort usMZSegment;
		ushort usDataSegment;
		uint uiMZEXELength = (uint)exe.Data.Length;
		MemoryRegion.AlignBlock(ref uiMZEXELength);

		if (startSegment < 0x10)
			throw new Exception("starting segment must be greater than 0x10");

		// blank start segment
		oMemory.AllocateParagraphs((ushort)(startSegment - 0x10), out usPSPSegment);
		oMemory.MemoryRegions.Add(new MemoryRegion(0, (startSegment - 0x10) << 4, MemoryFlagsEnum.None));

		// PSP segment
		oMemory.AllocateParagraphs(0x10, out usPSPSegment);
		oMemory.MemoryRegions.Add(new MemoryRegion(usPSPSegment, 0x100, MemoryFlagsEnum.None));

		// EXE segment
		oMemory.AllocateBlock((int)uiMZEXELength, out usMZSegment);
		oMemory.WriteBlock(usMZSegment, 0, exe.Data, 0, exe.Data.Length);

		// data segment
		oMemory.AllocateParagraphs((ushort)exe.MinimumAllocation, out usDataSegment);

		// decode EXE packer
		r.SP.Word = (ushort)exe.InitialSP;
		r.DS.Word = usPSPSegment;
		r.ES.Word = usPSPSegment;
		r.SS.Word = (ushort)(exe.InitialSS + usMZSegment);
		r.CS.Word = (ushort)(exe.InitialCS + usMZSegment);
		bool bDFlag = false;

		// Decoding phase 1

		// 2b0d:0010	mov ax, es
		r.AX.Word = r.ES.Word;
		// 2b0d:0012	add ax, 10h
		r.AX.Word += 0x10;
		// 2b0d:0015	push cs; pop ds
		r.DS.Word = r.CS.Word;
		// 2b0d:0017	mov word_2B0D_4, ax
		oMemory.WriteWord(r.DS.Word, 0x4, r.AX.Word);
		// 2b0d:001A	add     ax, word_2B0D_C
		r.AX.Word += oMemory.ReadWord(r.DS.Word, 0xc);
		// 2b0d:001E	mov es, ax
		r.ES.Word = r.AX.Word;
		// 2b0d:0020	mov cx, word_2B0D_6
		r.CX.Word = oMemory.ReadWord(r.DS.Word, 0x6);
		// 2b0d:0024	mov di, cx
		r.DI.Word = r.CX.Word;
		// 2b0d:0026	dec di
		r.DI.Word--;
		// 2b0d:0027	mov si, di
		r.SI.Word = r.DI.Word;
		// 2b0d:0029	std
		bDFlag = true;
		// 2b0d:002A	rep movsb
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, oMemory.ReadByte(r.DS.Word, r.SI.Word));
			if (bDFlag)
			{
				r.DI.Word--;
				r.SI.Word--;
			}
			else
			{
				r.DI.Word++;
				r.SI.Word++;
			}
			r.CX.Word--;
		}
		// 2b0d:002C	push    ax
		// 2b0d:002D	mov ax, 32h; push ax
		// 2b0d:0031	retf

		//oMemory.WriteWord(r.SS.Word, (ushort)(r.SP.Word - 2), r.AX.Word);
		//oMemory.WriteWord(r.SS.Word, (ushort)(r.SP.Word - 4), 0x32);
		r.CS.Word = r.AX.Word;
		r.IP.Word = 0x32;

		//oMemory.MemoryRegions.Add(new MemoryRegion(r.AX.Word, 0x32, 0x10f - 0x32, MemoryFlagsEnum.Read));
		//Console.WriteLine("Protecting block at 0x{0:x8}, size 0x{1:x4}", MemoryRegion.ToAbsolute(r.AX.Word, 0x32), 0x10f - 0x32);

		// Decoding phase 2

		// CS:IP = AX:0x32

		// 0x0032	MOV BX, ES
		r.BX.Word = r.ES.Word;
		// 0x0034	MOV AX, DS
		r.AX.Word = r.DS.Word;
		// 0x0036	DEC AX
		r.AX.Word--;
		// 0x0037	MOV DS, AX
		r.DS.Word = r.AX.Word;
		// 0x0039	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x003b	MOV DI, 0xf
		r.DI.Word = 0xf;
		// 0x003e	MOV CX, 0x10
		r.CX.Word = 0x10;
		// 0x0041	MOV AL, 0xff
		r.AX.Low = 0xff;
		// 0x0043	REPE SCASB
		while (r.CX.Word != 0)
		{
			byte res = (byte)(((int)r.AX.Low - (int)oMemory.ReadByte(r.ES.Word, r.DI.Word)) & 0xff);
			if (bDFlag)
			{
				r.DI.Word--;
			}
			else
			{
				r.DI.Word++;
			}
			r.CX.Word--;

			if (res != 0)
				break;
		}
		// 0x0045	INC DI
		r.DI.Word++;
		// 0x0046	MOV SI, DI
		r.SI.Word = r.DI.Word;
		// 0x0048	MOV AX, BX
		r.AX.Word = r.BX.Word;
		// 0x004a	DEC AX
		r.AX.Word--;
		// 0x004b	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x004d	MOV DI, 0xf
		r.DI.Word = 0xf;

		l50:
		// 0x0050	MOV CL, 0x4
		r.CX.Low = 0x4;
		// 0x0052	MOV AX, SI
		r.AX.Word = r.SI.Word;
		// 0x0054	NOT AX
		r.AX.Word = (ushort)(~r.AX.Word);
		// 0x0056	SHR AX, CL
		r.AX.Word = (ushort)(r.AX.Word >> r.CX.Low);
		// 0x0058	JE + 0x9
		if (r.AX.Word == 0) goto l63;
		// 0x005a	MOV DX, DS
		r.DX.Word = r.DS.Word;
		// 0x005c	SUB DX, AX
		r.DX.Word -= r.AX.Word;
		// 0x005e	MOV DS, DX
		r.DS.Word = r.DX.Word;
		// 0x0060	OR SI, 0xfff0
		r.SI.Word |= 0xfff0;

		l63:
		// 0x0063	MOV AX, DI
		r.AX.Word = r.DI.Word;
		// 0x0065	NOT AX
		r.AX.Word = (ushort)(~r.AX.Word);
		// 0x0067	SHR AX, CL
		r.AX.Word = (ushort)(r.AX.Word >> r.CX.Low);
		// 0x0069	JE + 0x9
		if (r.AX.Word == 0) goto l74;
		// 0x006b	MOV DX, ES
		r.DX.Word = r.ES.Word;
		// 0x006d	SUB DX, AX
		r.DX.Word -= r.AX.Word;
		// 0x006f	MOV ES, DX
		r.ES.Word = r.DX.Word;
		// 0x0071	OR DI, 0xfff0
		r.DI.Word |= 0xfff0;

		l74:
		// 0x0074	LODSB
		r.AX.Low = oMemory.ReadByte(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word--;
		}
		else
		{
			r.SI.Word++;
		}
		// 0x0075	MOV DL, AL
		r.DX.Low = r.AX.Low;
		// 0x0077	DEC SI
		r.SI.Word--;
		// 0x0078	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x0079	MOV CX, AX
		r.CX.Word = r.AX.Word;
		// 0x007b	INC SI
		r.SI.Word++;
		// 0x007c	MOV AL, DL
		r.AX.Low = r.DX.Low;
		// 0x007e	AND AL, 0xfe
		r.AX.Low &= 0xfe;
		// 0x0080	CMP AL, 0xb0
		// 0x0082	JNE + 0x6
		if (r.AX.Low != 0xb0) goto l8a;
		// 0x0084	LODSB
		r.AX.Low = oMemory.ReadByte(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word--;
		}
		else
		{
			r.SI.Word++;
		}
		// 0x0085	REPE STOSB
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, r.AX.Low);
			if (bDFlag)
			{
				r.DI.Word--;
			}
			else
			{
				r.DI.Word++;
			}
			r.CX.Word--;
		}
		// 0x0087	JMP + 0x7
		goto l90;
		// 0x0089	NOP

		l8a:
		// 0x008a	CMP AL, 0xb2
		// 0x008c	JNE + 0x6b
		if (r.AX.Low != 0xb2) goto lf9;
		// 0x008e	REPE MOVSB
		while (r.CX.Word != 0)
		{
			oMemory.WriteByte(r.ES.Word, r.DI.Word, oMemory.ReadByte(r.DS.Word, r.SI.Word));
			if (bDFlag)
			{
				r.DI.Word--;
				r.SI.Word--;
			}
			else
			{
				r.DI.Word++;
				r.SI.Word++;
			}
			r.CX.Word--;
		}

		l90:
		// 0x0090	MOV AL, DL
		r.AX.Low = r.DX.Low;
		// 0x0092	TEST AL, 0x1
		// 0x0094	JE - 0x46
		if ((r.AX.Low & 1) == 0) goto l50;
		// 0x0096	MOV SI, 0x125
		r.SI.Word = 0x125;
		// 0x0099	PUSH CS
		// 0x009a	POP DS
		r.DS.Word = r.CS.Word;
		// 0x009b	MOV BX, [0x4]
		r.BX.Word = oMemory.ReadWord(r.DS.Word, 0x4);
		// 0x009f	CLD
		bDFlag = false;
		// 0x00a0	XOR DX, DX
		r.DX.Word = 0;

		la2:
		// 0x00a2	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x00a3	MOV CX, AX
		r.CX.Word = r.AX.Word;
		// 0x00a5	JCXZ + 0x13
		if (r.CX.Word == 0) goto lba;
		// 0x00a7	MOV AX, DX
		r.AX.Word = r.DX.Word;
		// 0x00a9	ADD AX, BX
		r.AX.Word += r.BX.Word;
		// 0x00ab	MOV ES, AX
		r.ES.Word = r.AX.Word;

		lad:
		// 0x00ad	LODSW
		r.AX.Word = oMemory.ReadWord(r.DS.Word, r.SI.Word);
		if (bDFlag)
		{
			r.SI.Word -= 2;
		}
		else
		{
			r.SI.Word += 2;
		}
		// 0x00ae	MOV DI, AX
		r.DI.Word = r.AX.Word;
		// 0x00b0	CMP DI, 0xffff
		// 0x00b3	JE + 0x11
		if (r.DI.Word == 0xffff) goto lc6;
		// 0x00b5	ADD ES:[DI], BX
		oMemory.WriteWord(r.ES.Word, r.DI.Word, (ushort)(oMemory.ReadWord(r.ES.Word, r.DI.Word) + r.BX.Word));

		lb8:
		// 0x00b8	LOOP - 0xd
		r.CX.Word--;
		if (r.CX.Word != 0) goto lad;

		lba:
		// 0x00ba	CMP DX, 0xf000
		// 0x00be	JE + 0x16
		if (r.DX.Word == 0xf000) goto ld6;
		// 0x00c0	ADD DX, 0x1000
		r.DX.Word += 0x1000;
		// 0x00c4	JMP - 0x24
		goto la2;

		lc6:
		// 0x00c6	MOV AX, ES
		r.AX.Word = r.ES.Word;
		// 0x00c8	INC AX
		r.AX.Word++;
		// 0x00c9	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00cb	SUB DI, 0x10
		r.DI.Word -= 0x10;
		// 0x00ce	ADD ES:[DI], BX
		oMemory.WriteWord(r.ES.Word, r.DI.Word, (ushort)(oMemory.ReadWord(r.ES.Word, r.DI.Word) + r.BX.Word));
		// 0x00d1	DEC AX
		r.AX.Word--;
		// 0x00d2	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00d4	JMP - 0x1e
		goto lb8;

		ld6:
		// 0x00d6	MOV AX, BX
		r.AX.Word = r.BX.Word;
		// 0x00d8	MOV DI, [0x8]
		r.DI.Word = oMemory.ReadWord(r.DS.Word, 0x8);
		// 0x00dc	MOV SI, [0xa]
		r.SI.Word = oMemory.ReadWord(r.DS.Word, 0xa);
		// 0x00e0	ADD SI, AX
		r.SI.Word += r.AX.Word;
		// 0x00e2	ADD [0x2], AX
		oMemory.WriteWord(r.DS.Word, 0x2, (ushort)(oMemory.ReadWord(r.DS.Word, 0x2) + r.AX.Word));
		// 0x00e6	SUB AX, 0x10
		r.AX.Word -= 0x10;
		// 0x00e9	MOV DS, AX
		r.DS.Word = r.AX.Word;
		// 0x00eb	MOV ES, AX
		r.ES.Word = r.AX.Word;
		// 0x00ed	MOV BX, 0x0
		r.BX.Word = 0;
		// 0x00f0	CLI
		// 0x00f1	MOV SS, SI
		r.SS.Word = r.SI.Word;
		// 0x00f3	MOV SP, DI
		r.SP.Word = r.DI.Word;
		// 0x00f5	STI
		// 0x00f6	JMP far CS:[BX]
		r.IP.Word = oMemory.ReadWord(r.CS.Word, r.BX.Word);
		r.CS.Word = oMemory.ReadWord(r.CS.Word, (ushort)(r.BX.Word + 2));
		goto finished;

		lf9:
		throw new Exception("Error decoding file");

		finished:
		return oMemory;
	}
}