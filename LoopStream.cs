using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace FleetCommand
{
	public class LoopStream : WaveStream

	{
		private readonly WaveStream sourceStream;

		public LoopStream(WaveStream source)
		{
			this.sourceStream = source;
		}

		public override WaveFormat WaveFormat => sourceStream.WaveFormat;
		public override long Length => long.MaxValue;
		public override long Position
		{
			get => sourceStream.Position;
			set => sourceStream.Position = value;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int read = sourceStream.Read(buffer, offset, count);
			if (read == 0)
			{
				sourceStream.Position = 0;
				read = sourceStream.Read(buffer, offset, count);
			}
			return read;
		}

	}
}
