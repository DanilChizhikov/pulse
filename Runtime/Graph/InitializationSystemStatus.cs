using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Status of a single system inside a recorded initialization run.
	/// </summary>
	[Preserve]
	public enum InitializationSystemStatus
	{
		/// <summary>
		/// The system never started, because the run ended before its dependencies were initialized.
		/// </summary>
		NotStarted = 0,

		/// <summary>
		/// The system was initialized successfully.
		/// </summary>
		Completed = 1,

		/// <summary>
		/// The system threw an exception while initializing.
		/// </summary>
		Failed = 2,

		/// <summary>
		/// The system was started but the run was cancelled before it finished.
		/// </summary>
		Cancelled = 3,
	}
}
