using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Starksoft.Net.Ftp;

namespace johnshope.Sync {

	public class FtpStream: PipeStream {

		public FtpClient Client { get; set; }
		public string Path { get; set; }
		public long Size { get; set; }
		DateTime start;
		public FtpStream() : base() { start = DateTime.Now; }

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			//FtpConnections.Pass(Client);
			Log.Download(Path, Size, DateTime.Now - start);
		}
	}

}
