using IRB.VirtualCPU;

internal class Program
{
	private static void Main(string[] args)
	{
		Console.WriteLine($"CivUnpack version 1.2 (DOS Civilization (1991) EXE Unpacker) by R. Horvat");

		/*UnpackDOSEXE("CIV1.EXE", "CIV1_NEW.EXE"); // OK
		UnpackDOSEXE("CIV2.EXE", "CIV2_NEW.EXE"); // OK
		UnpackDOSEXE("CIV3.EXE", "CIV3_NEW.EXE"); // OK
		UnpackDOSEXE("CIV4.EXE", "CIV4_NEW.EXE"); // OK
		UnpackDOSEXE("CIV5.EXE", "CIV5_NEW.EXE"); // OK //*/

		//UnpackDOSEXE("CL.EXE", "CL_NEW.EXE");

		if (File.Exists("CIV.EXE"))
		{
			if (!File.Exists("CIV.bak"))
			{
				MZExecutable newEXE = new MZExecutable("CIV.EXE");

				// actually unpack the EXE
				UnpackEXE(newEXE);

				// make backup of the old file
				File.Move("CIV.EXE", "CIV.bak");
				
				// write new unpacked EXE
				newEXE.Write("CIV.EXE");

				Console.WriteLine("CIV.EXE successfully unpacked");
			}
			else
			{
				Console.WriteLine("Will not overwrite CIV.bak");
			}
		}
		else
		{
			Console.WriteLine("CIV.EXE does not exist. Put CivUnpack.exe in a directory with CIV.EXE and run it.");
		}//*/
	}

	private static void UnpackDOSEXE(string inputPath, string outputPath)
	{
		MZExecutable newEXE = new MZExecutable(inputPath);

		// actually unpack the EXE
		UnpackEXE(newEXE);

		// write new unpacked EXE
		newEXE.Write(outputPath);
	}

	private static void UnpackEXE(MZExecutable exe)
	{
		int MZEXELength = exe.Data.Length;

		if ((MZEXELength & 0xf) != 0)
		{
			MZEXELength += (16 - (MZEXELength & 0xf));
		}
		MZEXELength >>= 4;

		int baseAddress = exe.InitialCS * 16;
		ushort newIP = ReadUInt16(exe.Data, baseAddress + 0x0);
		ushort newCS = ReadUInt16(exe.Data, baseAddress + 0x2);
		ushort packerSize = ReadUInt16(exe.Data, baseAddress + 0x6);
		ushort newSP = ReadUInt16(exe.Data, baseAddress + 0x8);
		ushort newSS = ReadUInt16(exe.Data, baseAddress + 0xa);
		ushort packerCS = ReadUInt16(exe.Data, baseAddress + 0xc);
		int currentTablePosition;

		if (exe.Data[baseAddress + 0x96] == 0xbe)
		{
			currentTablePosition = (exe.InitialCS << 4) + ReadUInt16(exe.Data, baseAddress + 0x97);
		}
		else if (exe.Data[baseAddress + 0x9e] == 0xbe)
		{
			currentTablePosition = (exe.InitialCS << 4) + ReadUInt16(exe.Data, baseAddress + 0x9f);
		}
		else
		{
			throw new Exception("Can't find relocation table address");
		}

		int checkPackerAddress = baseAddress + exe.InitialIP;

		if (exe.Data[checkPackerAddress++] != 0x8C && exe.Data[checkPackerAddress++] != 0xC0 && 
			exe.Data[checkPackerAddress++] != 0x05 && exe.Data[checkPackerAddress++] != 0x10 && 
			exe.Data[checkPackerAddress++] != 0x00 && exe.Data[checkPackerAddress++] != 0x0E && 
			exe.Data[checkPackerAddress++] != 0x1F && exe.Data[checkPackerAddress++] != 0xA3 && 
			exe.Data[checkPackerAddress++] != 0x04 && exe.Data[checkPackerAddress++] != 0x00 && 
			exe.Data[checkPackerAddress++] != 0x03 && exe.Data[checkPackerAddress++] != 0x06 && 
			exe.Data[checkPackerAddress++] != 0x0C && exe.Data[checkPackerAddress++] != 0x00 && 
			exe.Data[checkPackerAddress++] != 0x8E && exe.Data[checkPackerAddress++] != 0xC0 && 
			exe.Data[checkPackerAddress++] != 0x8B && exe.Data[checkPackerAddress++] != 0x0E && 
			exe.Data[checkPackerAddress++] != 0x06 && exe.Data[checkPackerAddress++] != 0x00 && 
			exe.Data[checkPackerAddress++] != 0x8B && exe.Data[checkPackerAddress++] != 0xF9 && 
			exe.Data[checkPackerAddress++] != 0x4F && exe.Data[checkPackerAddress++] != 0x8B && 
			exe.Data[checkPackerAddress++] != 0xF7 && exe.Data[checkPackerAddress++] != 0xFD && 
			exe.Data[checkPackerAddress++] != 0xF3 && exe.Data[checkPackerAddress++] != 0xA4 && 
			exe.Data[checkPackerAddress++] != 0x50 && exe.Data[checkPackerAddress++] != 0xB8 && 
			exe.Data[checkPackerAddress++] != 0x32 && exe.Data[checkPackerAddress++] != 0x00 && 
			exe.Data[checkPackerAddress++] != 0x50 && exe.Data[checkPackerAddress++] != 0xCB)
		{
			throw new Exception("The file is missing proper unpack header");
		}

		#region Decode relocation table

		exe.Relocations.Clear();

		for (int i = 0; i < 16; i++)
		{
			int tableEntryCount = ReadUInt16(exe.Data, currentTablePosition);
			currentTablePosition += 2;

			for (int j = 0; j < tableEntryCount; j++)
			{
				exe.Relocations.Add(new MZRelocationItem((ushort)(i << 12), ReadUInt16(exe.Data, currentTablePosition)));
				currentTablePosition += 2;
			}
		}
		#endregion

		#region Decode data

		// resize destination array to the required size
		byte[] dataBuffer = exe.Data;
		Array.Resize<byte>(ref dataBuffer, packerCS << 4);
		exe.Data = dataBuffer;

		int sourceAddress = ((exe.InitialCS - 1) << 4) + 0xf;
		int destinationAddress = ((packerCS - 1) << 4) + 0xf;

		for (int i = 0; i < 16; i++)
		{
			byte value = exe.Data[sourceAddress];
			sourceAddress--;

			if (value != 0xff)
				break;
		}
		sourceAddress++;

		byte blockSignature = 0;

		while ((blockSignature & 1) == 0)
		{
			blockSignature = exe.Data[sourceAddress];
			sourceAddress -= 2;

			int blockSize = ReadUInt16(exe.Data, sourceAddress);
			sourceAddress -= 2;

			sourceAddress++;

			if ((blockSignature & 0xfe) == 0xb0)
			{
				byte blockValue = exe.Data[sourceAddress];
				sourceAddress--;

				while (blockSize != 0)
				{
					exe.Data[destinationAddress] = blockValue;
					destinationAddress--;
					blockSize--;
				}
			}
			else if ((blockSignature & 0xfe) == 0xb2)
			{
				while (blockSize != 0)
				{
					exe.Data[destinationAddress] = exe.Data[sourceAddress];
					destinationAddress--;
					sourceAddress--;
					blockSize--;
				}
			}
			else
			{
				throw new Exception("Encoded data is corrupted");
			}
		}
		#endregion

		// Extract the new data and stack segment to adjust the program size
		// We don't actuall need this, it decreases the EXE file slightly, but compiler put these paddings for a reason

		/*baseAddress = newCS * 16;
		ushort newDS = ReadUInt16(exe.Data, baseAddress + newIP + 0x2);

		if (ReadUInt16(exe.Data, baseAddress + newIP + 0x4f) == 0x14 &&
			exe.Data[baseAddress + 0x7b] == 0x16 && exe.Data[baseAddress + 0x7c] == 0x7 &&
			exe.Data[baseAddress + 0x7d] == 0xfc && exe.Data[baseAddress + 0x7e] == 0xbf)
		{
			// we have the correct offset
			ushort cutoffOffset = ReadUInt16(exe.Data, baseAddress + 0x7f);
			int cutoffAddress = (newDS << 4) + cutoffOffset;

			if ((cutoffOffset & 0xf) != 0)
			{
				cutoffAddress += 16 - (cutoffOffset & 0xf);
			}

			if (cutoffAddress != exe.Data.Length)
			{
				dataBuffer = exe.Data;
				Array.Resize<byte>(ref dataBuffer, cutoffAddress);
				exe.Data = dataBuffer;
			}

			exe.MinimumAllocation = (ushort)(exe.MinimumAllocation + (MZEXELength - (exe.Data.Length >> 4)));
		}
		else
		{
			throw new Exception("File encoded data is corrupted");
		}*/

		exe.MinimumAllocation = (ushort)(exe.MinimumAllocation + (MZEXELength - (exe.Data.Length >> 4)));
		exe.InitialCS = newCS;
		exe.InitialIP = newIP;
		exe.InitialSP = newSP;
		exe.InitialSS = newSS;

		// remove leftovers from an old relocation table(s)
		exe.AdditionalHeaderDataAfterRelocationTable = new byte[0];

		for (int i = 0; i < exe.Overlays.Count; i++)
		{
			MZExecutable overlay = exe.Overlays[i];

			overlay.AdditionalHeaderDataAfterRelocationTable = new byte[0];
		}
	}

	public static ushort ReadUInt16(byte[] buffer, int position)
	{
		return (ushort)((int)buffer[position] | ((int)buffer[position + 1] << 8));
	}

	public static void WriteUInt16(byte[] buffer, int position, ushort value)
	{
		buffer[position] = (byte)(value & 0xff);
		buffer[position + 1] = (byte)((value & 0xff00) >> 8);
	}
}