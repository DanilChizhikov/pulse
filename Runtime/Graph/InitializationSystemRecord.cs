using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Result and timings of a single system inside a recorded initialization run.
	/// </summary>
	[Preserve]
	public sealed class InitializationSystemRecord
	{
		/// <summary>
		/// Short name of the system type.
		/// </summary>
		public string TypeName { get; }

		/// <summary>
		/// Namespace-qualified name of the system type.
		/// </summary>
		public string FullTypeName { get; }

		/// <summary>
		/// Order in which the system started, counted across the whole run.
		/// </summary>
		public int StartOrder { get; }

		/// <summary>
		/// Whether the system was marked as critical.
		/// </summary>
		public bool IsCritical { get; }

		/// <summary>
		/// Whether the system became critical because a critical system depends on it, and not because it was
		/// explicitly marked as critical.
		/// </summary>
		public bool IsAutoCritical { get; }

		/// <summary>
		/// Final status of the system.
		/// </summary>
		public InitializationSystemStatus Status { get; }

		/// <summary>
		/// Offset from the start of the initialization run to the start of the system, in milliseconds.
		/// </summary>
		public double StartMilliseconds { get; }

		/// <summary>
		/// Duration of the system initialization, in milliseconds.
		/// </summary>
		public double DurationMilliseconds { get; }

		/// <summary>
		/// Indices, inside <see cref="InitializationGraphSnapshot.Systems"/>, of the systems this one depends on.
		/// </summary>
		public IReadOnlyList<int> DependencyIndices { get; }

		/// <summary>
		/// Text of the exception that failed the system, or an empty string when there was none.
		/// </summary>
		public string Error { get; }

		internal InitializationSystemRecord(
			string typeName,
			string fullTypeName,
			int startOrder,
			bool isCritical,
			bool isAutoCritical,
			InitializationSystemStatus status,
			double startMilliseconds,
			double durationMilliseconds,
			int[] dependencyIndices,
			string error)
		{
			TypeName = typeName;
			FullTypeName = fullTypeName;
			StartOrder = startOrder;
			IsCritical = isCritical;
			IsAutoCritical = isAutoCritical;
			Status = status;
			StartMilliseconds = startMilliseconds;
			DurationMilliseconds = durationMilliseconds;
			DependencyIndices = dependencyIndices ?? Array.Empty<int>();
			Error = error ?? string.Empty;
		}
	}
}
