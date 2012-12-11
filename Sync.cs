using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace johnshope.Sync {

    public class Sync {

        static readonly TimeSpan dt = TimeSpan.FromMinutes(1); // minimal file time resolution

        public static CopyMode Mode { get { return Program.Mode; } }
        public static string Log { get { return Program.Log; } }
        public static bool Verbose { get { return Program.Verbose; } }

		class FailureInfo { public FileOrDirectory File; public Exception Exception; }

		static Queue<FailureInfo> Failures = new Queue<FailureInfo>();

		public static void Failure(FileOrDirectory file, Exception ex) { lock (Failures) Failures.Enqueue(new FailureInfo { File=file, Exception = ex }); johnshope.Sync.Log.Exception(ex); }
		public static void Failure(FileOrDirectory file, Exception ex, FtpClient ftp) { lock (Failures) Failures.Enqueue(new FailureInfo { File=file, Exception = ex }); johnshope.Sync.Log.Exception(ftp, ex); }

		static IDirectory Root(FileOrDirectory fd) { if (fd.Parent == null) return (IDirectory)fd; else return Root(fd.Parent); }


		static List<FailureInfo> MyFailures(IDirectory sroot, IDirectory droot) {
			List<FailureInfo> list = new List<FailureInfo>();

			lock (Failures) {
				int n = Failures.Count;
				while (n-- > 0) {
					var failure = Failures.Dequeue();
					var root = Root(failure.File);
					if (root == sroot || root == droot) list.Add(failure);
					else Failures.Enqueue(failure);
				}
			}
			return list;
		}

		public static void RetryFailures(IDirectory sroot, IDirectory droot) {

			var list = MyFailures(sroot, droot);
			
			if (list.Count > 0) {
				johnshope.Sync.Log.YellowText("####    Retry failed transfers...");
				var set = new HashSet<IDirectory>();
				foreach (var failure in list) {
					IDirectory dir;
					if (failure is IDirectory) dir = (IDirectory)failure.File;
					else dir = (IDirectory)failure.File.Parent;

					if (!set.Contains(dir.Source)) {
						set.Add(dir.Source);
						Directory(dir.Source, dir.Destination);
					}
				}

				johnshope.Sync.Log.YellowText("####    Summary of errors:");
				foreach (var failure in list) johnshope.Sync.Log.Exception(failure.Exception);

				johnshope.Sync.Log.YellowText("####    Failed transfers:");
				foreach (var failure in list) johnshope.Sync.Log.RedText("Failed transfer: " + failure.File.RelativePath);
			}

			list = MyFailures(sroot, droot); // dequeue recurrant failures.
		}

        public static void Directory(IDirectory sdir, IDirectory ddir) {

			if (ddir == null || sdir == null) return;

			sdir.Source = ddir.Source = sdir;
			sdir.Destination = ddir.Destination = ddir;

			int con;

			if (sdir is LocalDirectory && ddir is LocalDirectory) con = 1;
			else {
				con = Math.Max(FtpConnections.Count(sdir.Url), FtpConnections.Count(ddir.Url));
				if (ddir is FtpDirectory) {
					((FtpDirectory)ddir).TransferProgress = true;
				} else if (sdir is FtpDirectory) {
					((FtpDirectory)sdir).TransferProgress = true;
				}
			}
			if (con == 0) con = 1;
			var list = sdir.List().Where(file => !johnshope.Sync.Paths.Match(Program.ExcludePatterns, file.RelativePath)).ToList();
			var dlist = ddir.List();
			//ddir.CreateDirectory(null);

			Parallel.ForEach<FileOrDirectory>(list, new ParallelOptions { MaxDegreeOfParallelism = con }, 
				(src) => {
					FileOrDirectory dest = null;
					lock(dlist) { if (dlist.Contains(src.Name)) dest = dlist[src.Name]; }
					if (dest != null && dest.Class != src.Class && (src.Changed > dest.Changed || Mode == CopyMode.Clone)) ddir.Delete(dest);
					if (src.Class == ObjectClass.File) {
						/*if (Verbose && dest != null) {
							johnshope.Sync.Log.CyanText(src.Name + ":    " + src.Changed.ToShortDateString() + "-" + src.Changed.ToShortTimeString() + " => " +
								dest.Changed.ToShortDateString() + "-" + dest.Changed.ToShortTimeString());
						}*/
						if (dest == null || ((Mode == CopyMode.Update || Mode == CopyMode.Add) && src.Changed > dest.Changed) || (Mode == CopyMode.Clone && (src.Changed > dest.Changed + dt))) {
							using (var s = sdir.ReadFile(src)) {
								ddir.WriteFile(s, src);
							}
						}
					} else {
						if (dest == null) Directory((IDirectory)src, ddir.CreateDirectory(src));
						else Directory((IDirectory)src, (IDirectory)dest);
					}
					lock (dlist) { dlist.Remove(src.Name); }
				});
			if (Mode != CopyMode.Add) {
				foreach (var dest in dlist) ddir.Delete(dest);
			}
		}

        public static void Directory(Uri src, Uri dest) {
            try {
                var start = DateTime.Now;
                FtpConnections.Allocate(src);
                FtpConnections.Allocate(dest);

                // messages
                if (src.Scheme == "ftp" || src.Scheme == "ftps") {
                    var ftp = FtpConnections.Open(ref src);
                    johnshope.Sync.Log.Text("Source host: " + src.Authority + "    Server Time:" + ftp.ServerTimeString);
                    FtpConnections.Pass(ftp);
                }
                if (dest.Scheme == "ftp" || dest.Scheme == "ftps") {
                    var ftp = FtpConnections.Open(ref dest);
                    johnshope.Sync.Log.Text("Destination host: " + dest.Authority + "    Server Time:" + ftp.ServerTimeString);
                    FtpConnections.Pass(ftp);
                }

                johnshope.Sync.Log.Text(string.Format("Mode: {0}; Log: {1}; Verbose: {2}, Exclude: {3}", Mode, Log, Verbose, Program.ExcludePatterns));
                johnshope.Sync.Log.Text("");

				var sdir = johnshope.Sync.Directory.Parse(src);
				var ddir = johnshope.Sync.Directory.Parse(dest);
                
				Directory(sdir, ddir);

				RetryFailures(sdir, ddir);

                FtpConnections.Close();

                johnshope.Sync.Log.Summary(DateTime.Now - start);
            } catch (Exception ex) {
                johnshope.Sync.Log.Exception(ex);
            }
        }
    }
}
