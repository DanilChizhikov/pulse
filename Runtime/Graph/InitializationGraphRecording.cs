using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	public static class InitializationGraphRecording
	{
		public static event Action<InitializationGraphSnapshot> OnSnapshotRecorded;
		
		public static bool IsEnabled { get; set; }

		internal static void Publish(InitializationGraphSnapshot snapshot)
		{
			Action<InitializationGraphSnapshot> handler = OnSnapshotRecorded;
			if (handler == null)
			{
				return;
			}

			foreach (Delegate subscriber in handler.GetInvocationList())
			{
				try
				{
					((Action<InitializationGraphSnapshot>)subscriber).Invoke(snapshot);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}
	}
}
