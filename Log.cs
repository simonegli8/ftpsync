using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace johnshope.Sync {

	public class Log {

		public static int Uploads = 0;
		public static int Downloads = 0;
		public static long UploadSize = 0;
		public static long DownloadSize = 0;
		public static int Errors = 0;

		const int KB = 1024;
		const int MB = 1024 * KB;
		const int GB = 1024 * MB;
		const int MaxLogSize = 10*MB;

		public static object Lock = new object();
		static bool checkDir = true;

		public static string Size(long size) {
			if (size > GB) return string.Format("{0:F2} GB", size / (1.0 * GB));
			if (size > 100 * KB) return string.Format("{0:F2} MB", size / (1.0 * MB));
			return string.Format("{0:F0} KB", size / (1.0 * KB));
		}

		public static void Debug(string text) {
			if (Sync.Verbose) Text(text);
		}

		public static void LogText(string text, bool newline) {
			lock (Lock) {
				if (Sync.Log != null) {
					try {
						if (checkDir) {
							checkDir = false;
							var dir = Path.GetDirectoryName(Sync.Log);
							if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
						}
						if (newline) text = text + "\r\n";
						System.IO.File.AppendAllText(Sync.Log, text, UTF8Encoding.UTF8);
					} catch (Exception ex) {
						Console.WriteLine("Error writing to the logfile " + Sync.Log);
						Console.WriteLine(ex.Message);
					}
				}
			}
		}
		
		public static void Text(string text) { lock(Lock) { Console.WriteLine(text); LogText(text, true); } }
		public static void RedText(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Red; Text(text); Console.ForegroundColor = oldc; } }
		public static void CyanText(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Cyan; Text(text); Console.ForegroundColor = oldc; } }
		public static void GreenText(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Green; Text(text); Console.ForegroundColor = oldc; } }
		public static void YellowText(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Yellow; Text(text); Console.ForegroundColor = oldc; } }
		public static void YellowLabel(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Yellow; Console.Write(text); LogText(text, false); Console.ForegroundColor = oldc; } }
		public static void RedLabel(string text) { lock (Lock) { var oldc = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Red; Console.Write(text); LogText(text, false); Console.ForegroundColor = oldc; } }
		public static void Label(string text) { lock (Lock) { Console.Write(text); LogText(text, false); } }

		public static void Exception(Exception e) { lock (Lock) { Errors++; RedText("Error"); RedText(e.Message); if (Sync.Verbose) { RedText(e.StackTrace); } /* System.Diagnostics.Debugger.Break(); */ } }
		public static void Exception(FtpClient ftp, Exception e) {
			if (ftp == null) Exception(e);
			else {
				lock (Lock) {
					var prefix = FtpConnections.FTPTag(ftp.Index) + "! ";
					Errors++; RedLabel(prefix);  RedText("Error");
					var lines = e.Message.Split('\n');
					foreach (var line in lines) { RedLabel(prefix); RedText(line); }
					if (Sync.Verbose) {
						lines = e.StackTrace.Split('\n');
						foreach (var line in lines) { RedLabel(prefix); RedText(line); }
					}
					// System.Diagnostics.Debugger.Break();
				}
			}
		}
		public static void Upload(string path, long size, TimeSpan time) { GreenText(string.Format("Uploaded {0}    =>    {1} at {2:F3}/s.", path, Size(size), Size((long)(size / time.TotalSeconds + 0.5)))); Uploads++; UploadSize += size; }
		public static void Download(string path, long size, TimeSpan time) { GreenText(string.Format("Downloaded {0}    =>    {1} at {2:F3}/s.", path, Size(size), Size((long)(size / time.TotalSeconds + 0.5)))); Downloads++; DownloadSize += size; }
		public static void Progress(string path, long size, long part, TimeSpan time) { GreenText(string.Format("Transfer of {0}    =>    {1:F1}% at {2:F3}/s.", path, (part*100.0 / size), Size((long)(part / time.TotalSeconds + 0.5)))); }


		public static void Summary(TimeSpan t) {
			Text("");
			GreenText(string.Format("####    =>    {0} Files and {1} transfered in {2:F3} seconds at {3}/s. {4} Errors.",
				Math.Max(Uploads, Downloads), Size(UploadSize + DownloadSize), t.TotalSeconds, Size((long)(Math.Max(UploadSize, DownloadSize) / t.TotalSeconds + 0.5)), Errors));
			Text("");
			Text("");
			Text("");

			if (Sync.Log != null) {
				var log = new FileInfo(Sync.Log);
				if (log.Length > MaxLogSize) {
					var loglines = File.ReadAllLines(Sync.Log).ToList();
					loglines.RemoveRange(0, loglines.Count / 2);
					File.WriteAllLines(Sync.Log, loglines.ToArray());
				}
			}
		}

	}
}
