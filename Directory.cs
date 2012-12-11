using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace johnshope.Sync {

	public class Directory {

		public static IDirectory Parse(Uri url) {
			if (url.IsFile || !url.ToString().Contains(':')) return new LocalDirectory(null, url);
			else return new FtpDirectory(null, url);
		}

	}
}
