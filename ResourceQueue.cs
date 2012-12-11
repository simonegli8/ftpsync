using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;

namespace johnshope.Sync {

	public class ResourceQueue<T>: System.Collections.Generic.LinkedList<T> {

		AutoResetEvent signal = new AutoResetEvent(false);

		public void Enqueue(T entry) {
			lock (this) {
				var node = First;
				while (node != null && node.Value != null) node = node.Next;
				if (node == null) AddLast(entry);
				else AddBefore(node, entry);
			}
			signal.Set();
		}

		public T Dequeue() {
			lock (this) {
				if (base.Count > 0) {
					var entry = First;
					RemoveFirst();
					return entry.Value;
				} else return default(T);
			}
		}

	
		public event EventHandler Blocking;
		public event EventHandler Blocked;

		public T DequeueOrBlock() {
			do {
				lock (this) {
					if (base.Count > 0) return Dequeue();
				}
				if (Blocking != null) Blocking(this, EventArgs.Empty);
				signal.WaitOne();
				if (Blocked != null) Blocked(this, EventArgs.Empty);
			} while (true);
		}

		public bool IsEmpty { get { lock (this) return base.Count == 0; } }
		public new int Count { get { lock (this) return base.Count; } }
	}
}
