using Disassembler.CPU;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Disassembler
{
	public class CPURegisters
	{
		private CPURegister rAX = new CPURegister(0);
		private CPURegister rBX = new CPURegister(0);
		private CPURegister rCX = new CPURegister(0);
		private CPURegister rDX = new CPURegister(0);
		private CPURegister rDI = new CPURegister(0);
		private CPURegister rSI = new CPURegister(0);
		private CPURegister rSP = new CPURegister(0);
		private CPURegister rBP = new CPURegister(0);
		private CPURegister rIP = new CPURegister(0);

		private CPURegister rCS = new CPURegister(0);
		private CPURegister rDS = new CPURegister(0);
		private CPURegister rES = new CPURegister(0);
		private CPURegister rSS = new CPURegister(0);

		public CPURegisters()
		{ }

		public CPURegister AX
		{
			get { return this.rAX; }
		}

		public CPURegister BX
		{
			get { return this.rBX; }
		}

		public CPURegister CX
		{
			get { return this.rCX; }
		}


		public CPURegister DX
		{
			get { return this.rDX; }
		}

		public CPURegister DI
		{
			get { return this.rDI; }
		}

		public CPURegister SI
		{
			get { return this.rSI; }
		}

		public CPURegister SP
		{
			get { return this.rSP; }
		}

		public CPURegister BP
		{
			get { return this.rBP; }
		}

		public CPURegister IP
		{
			get { return this.rIP; }
		}

		public CPURegister CS
		{
			get { return this.rCS; }
		}

		public CPURegister SS
		{
			get { return this.rSS; }
		}

		public CPURegister DS
		{
			get { return this.rDS; }
		}

		public CPURegister ES
		{
			get { return this.rES; }
		}

	}
}
