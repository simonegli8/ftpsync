using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Starksoft.Net.Ftp;

namespace johnshope.Sync {

	public class FtpDirectory: FileOrDirectory, IDirectory {

        Uri url;
        public Uri Url { get { return url; } set { url = value; } }

		public bool TransferProgress { get; set; }

		public FtpDirectory(FileOrDirectory parent, Uri url) {
            Parent = parent;
			if (url.Scheme != "ftp" && url.Scheme != "ftps") throw new NotSupportedException();
			Url = url;
			Name = url.File();
			Class = ObjectClass.Directory;
			Changed = DateTime.Now.AddDays(2);
			if (parent is FtpDirectory) TransferProgress = ((FtpDirectory)parent).TransferProgress;
		}

		public IDirectory Source { get; set; }
		public IDirectory Destination { get; set; }

		bool UseCompression { get { return Url.Query.Contains("compress"); } }

		public DirectoryListing List() {
			var ftp = FtpConnections.Open(ref url);
			try {
				ftp.FileTransferType = TransferType.Ascii;
				var list = ftp.GetDirList().Select(fi => fi.ItemType == FtpItemType.Directory ? new FtpDirectory(this, Url.Relative(fi.Name)) : new FileOrDirectory { Name = fi.Name, Class = ObjectClass.File, Changed = fi.Modified, Size = fi.Size, Parent = this }).ToList();
				return new DirectoryListing(list);
			} catch (Exception ex) {
				Sync.Failure(this, ex, ftp);
			} finally {
				FtpConnections.Pass(ftp);
			}
			return new DirectoryListing();
		}

		static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

		class ProgressData {
			public string Path;
			public long Size;
			public long Transferred;
			public TimeSpan ElapsedTime;
		}

		Dictionary<FtpClient, ProgressData> progress = new Dictionary<FtpClient,ProgressData>();
		public void ShowProgress(object sender, TransferProgressEventArgs a) {
			if (TransferProgress) {
				var ftp = (FtpClient)sender;
				var p = progress[ftp];
				p.Transferred += a.BytesTransferred;
				if (a.ElapsedTime - p.ElapsedTime > Interval) {
					Log.Progress(p.Path, p.Size, p.Transferred, a.ElapsedTime);
					Log.Text(ftp.dw.TotalMilliseconds.ToString());
					Log.Text(ftp.dr.TotalMilliseconds.ToString());
					p.ElapsedTime = a.ElapsedTime;
				}
			}	
		}

		public void WriteFile(System.IO.Stream file, FileOrDirectory src) {
			if (file == null) return;

			var ftp = FtpConnections.Open(ref url);
			try {
				if (ftp.FileTransferType != TransferType.Binary) ftp.FileTransferType = TransferType.Binary;
				var path = Url.Path() + "/" + src.Name;
				if (TransferProgress) {
					progress[ftp] = new ProgressData { ElapsedTime = new TimeSpan(0), Path = path, Size = src.Size };
					ftp.TransferProgress += ShowProgress;
				}
				var start = DateTime.Now;
				ftp.PutFile(file, src.Name, FileAction.Create);
				ftp.SetDateTime(src.Name, src.ChangedUtc);
			
				Log.Upload(path, src.Size, DateTime.Now - start);
			} catch (Exception e) {
				Sync.Failure(src, e, ftp);
			} finally {
				if (TransferProgress) {
					ftp.TransferProgress -= ShowProgress;
					progress.Remove(ftp);
				}
				FtpConnections.Pass(ftp);
			}
		}

		public System.IO.Stream ReadFile(FileOrDirectory src) {
			var ftp = FtpConnections.Open(ref url);
			try {
				if (ftp.FileTransferType != TransferType.Binary) ftp.FileTransferType = TransferType.Binary;
				var file = new FtpStream();
				file.Client = ftp;
				file.Path = Url.Path() + "/" + src.Name;
				file.Size = src.Size;
				Task.Factory.StartNew(() => {
					try {
						if (TransferProgress) {
							progress[ftp] = new ProgressData { ElapsedTime = new TimeSpan(0), Path = file.Path, Size = src.Size };
							ftp.TransferProgress += ShowProgress;
						}
						using (var f = file) { ftp.GetFile(src.Name, f, false); }
					
					} catch (Exception ex) {
						Sync.Failure(src, ex, ftp);
						file.Exception(ex);
					} finally {
						if (TransferProgress) {
							ftp.TransferProgress -= ShowProgress;
							progress.Remove(ftp);
						}
					}
				});
				return file;	
			} catch (Exception e) {
				Sync.Failure(src, e, ftp);
            } finally {
                FtpConnections.Pass(ftp);
			}
			return null;
		}

		public void DeleteFile(FileOrDirectory dest) {
			var ftp = FtpConnections.Open(ref url);
			try {
				ftp.DeleteFile(dest.Name);
			} catch (Exception ex) {
				Sync.Failure(dest, ex, ftp);
			} finally {
				FtpConnections.Pass(ftp);
			}
		}

		public void DeleteDirectory(FileOrDirectory dest) {
			var dir = (FtpDirectory)dest;
			int con = FtpConnections.Count(dir.url);
			if (con == 0) con = 1;
			var list = dir.List();
			FtpClient ftp = null;
			try {

				Parallel.ForEach<FtpDirectory>(list.OfType<FtpDirectory>(), new ParallelOptions { MaxDegreeOfParallelism = con }, (d) => { d.DeleteDirectory(d); });

				ftp = FtpConnections.Open(ref dir.url);
				foreach (var file in list.Where(f => f.Class == ObjectClass.File)) ftp.DeleteFile(file.Name);

				ftp.ChangeDirectoryUp();
				ftp.DeleteDirectory(dest.Name);
			} catch (Exception ex) {
				Sync.Failure(dest, ex, ftp);
			} finally {
				FtpConnections.Pass(ftp);
			}
		}

		public void Delete(FileOrDirectory dest) {
			if (dest.Class == ObjectClass.File) DeleteFile(dest);
			else DeleteDirectory(dest);
		}

		public IDirectory CreateDirectory(FileOrDirectory dest) {
			var ftp = FtpConnections.Open(ref url);
			try {
				var path = ftp.CorrectPath(Url.Path());
				if (dest != null) path = path + "/" + dest.Name;
				//var curpath = ftp.CurrentDirectory;
				//var ps = path.Split('/');
				//var cs = curpath.Split('/');
				//var j = cs.Length-1;	
                //var i = Math.Min(ps.Length, j+1);
                //while (j > i-1) { ftp.ChangeDirectoryUp(); j--; }
                //while (j > 0 && ps[j] != cs[j]) { ftp.ChangeDirectoryUp(); j--; i = j+1; }
				
				//while (i < ps.Length) { str.Append("/"); str.Append(ps[i++]); }

				//var dir = str.ToString();
				ftp.MakeDirectory(path);
				
				//if (url.Query()["old"] != null) ftp.ChangeDirectoryMultiPath(path);
				//else ftp.ChangeDirectory(path);

				if (dest != null) return new FtpDirectory(this, Url.Relative(dest.Name));
				else return this;
			} catch (Exception ex) {
				Sync.Failure(dest, ex, ftp);
			} finally {
				FtpConnections.Pass(ftp);
			}
			return null;
		}

	}
}
