using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Global switch and publishing point of the initialization graph snapshots.
	/// </summary>
	/// <remarks>
	/// Recording must be enabled before <see cref="InitializationContextBuilder.Build"/> is called,
	/// otherwise the context is created without a recorder.
	/// </remarks>
	[Preserve]
	public static class InitializationGraphRecording
	{
		/// <summary>
		/// Raised when an initialization run is finished and its snapshot is ready.
		/// </summary>
		/// <remarks>
		/// Every subscriber is invoked independently: an exception thrown by one of them is logged and does not
		/// prevent the others from being called.
		/// </remarks>
		public static event Action<InitializationGraphSnapshot> OnSnapshotRecorded;
		
		/// <summary>
		/// Enables the recording of initialization graphs. Disabled by default.
		/// </summary>
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
