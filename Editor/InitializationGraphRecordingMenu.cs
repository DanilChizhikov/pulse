using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;

namespace DTech.Pulse.Editor
{
	[InitializeOnLoad]
	internal static class InitializationGraphRecordingMenu
	{
		private const string RecordMenuPath = "Tools/DTech/Pulse/Record Initialization Graph";
		private const string ClearRecordMenuPath = "Tools/DTech/Pulse/Clear Records";
		private const string RecordPrefsKey = "DTech.Pulse.RecordInitializationGraph";

		private static readonly ConcurrentQueue<InitializationGraphSnapshot> PendingSnapshots = new();
		private static readonly int MainThreadId;

		public static bool IsRecordingEnabled
		{
			get => EditorPrefs.GetBool(RecordPrefsKey, false);
			set
			{
				EditorPrefs.SetBool(RecordPrefsKey, value);
				InitializationGraphRecording.IsEnabled = value;
			}
		}

		static InitializationGraphRecordingMenu()
		{
			MainThreadId = Thread.CurrentThread.ManagedThreadId;
			InitializationGraphRecording.IsEnabled = IsRecordingEnabled;
			InitializationGraphRecording.OnSnapshotRecorded += SnapshotRecordedHandler;
			EditorApplication.update += EditorUpdateHandler;
		}

		[MenuItem(RecordMenuPath)]
		private static void ToggleRecording()
		{
			IsRecordingEnabled = !IsRecordingEnabled;
		}

		[MenuItem(RecordMenuPath, true)]
		private static bool ValidateToggleRecording()
		{
			Menu.SetChecked(RecordMenuPath, IsRecordingEnabled);
			return true;
		}
		
		[MenuItem(ClearRecordMenuPath)]
		private static void ClearRecords()
		{
			IReadOnlyList<string> paths = InitializationGraphStorage.GetSnapshotPaths();
			if (paths != null)
			{
				foreach (string path in paths)
				{
					if (File.Exists(path))
					{
						File.Delete(path);
					}
				}
			}
		}

		private static void SnapshotRecordedHandler(InitializationGraphSnapshot snapshot)
		{
			if (Thread.CurrentThread.ManagedThreadId == MainThreadId)
			{
				InitializationGraphStorage.Save(snapshot);
				return;
			}

			PendingSnapshots.Enqueue(snapshot);
		}

		private static void EditorUpdateHandler()
		{
			while (PendingSnapshots.TryDequeue(out InitializationGraphSnapshot snapshot))
			{
				InitializationGraphStorage.Save(snapshot);
			}
		}
	}
}
