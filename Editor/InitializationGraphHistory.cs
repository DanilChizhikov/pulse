using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;

namespace DTech.Pulse.Editor
{
	[InitializeOnLoad]
	internal static class InitializationGraphHistory
	{
		public static event Action<RecordedSnapshot> OnRecorded;

		private const int MaxSnapshots = 20;

		private static readonly List<RecordedSnapshot> _snapshots = new();
		private static readonly ConcurrentQueue<InitializationGraphSnapshot> _pendingSnapshots = new();
		private static readonly int _mainThreadId;
		
		public static IReadOnlyList<RecordedSnapshot> Snapshots => _snapshots;

		private static int _lastOrdinal;

		static InitializationGraphHistory()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			InitializationGraphRecording.OnSnapshotRecorded += SnapshotRecordedHandler;
			EditorApplication.update += EditorUpdateHandler;
		}
		
		public static void Clear()
		{
			_snapshots.Clear();
			_lastOrdinal = 0;
		}

		private static void Add(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}

			_lastOrdinal++;
			var recorded = new RecordedSnapshot(snapshot, _lastOrdinal, DateTime.Now);
			_snapshots.Insert(0, recorded);
			if (_snapshots.Count > MaxSnapshots)
			{
				_snapshots.RemoveRange(MaxSnapshots, _snapshots.Count - MaxSnapshots);
			}

			OnRecorded?.Invoke(recorded);
		}
		
		private static void SnapshotRecordedHandler(InitializationGraphSnapshot snapshot)
		{
			if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
			{
				Add(snapshot);
				return;
			}

			_pendingSnapshots.Enqueue(snapshot);
		}
		
		private static void EditorUpdateHandler()
		{
			while (_pendingSnapshots.TryDequeue(out InitializationGraphSnapshot snapshot))
			{
				Add(snapshot);
			}
		}
	}
}