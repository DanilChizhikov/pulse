using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Result and timings of a single system inside a recorded initialization run.
	/// </summary>
	[Serializable]
	[Preserve]
	public sealed class InitializationSystemRecord
	{
		[SerializeField] private string _typeName;
		[SerializeField] private string _fullTypeName;
		[SerializeField] private int _startOrder;
		[SerializeField] private bool _isCritical;
		[SerializeField] private InitializationSystemStatus _status;
		[SerializeField] private double _startMilliseconds;
		[SerializeField] private double _durationMilliseconds;
		[SerializeField] private int[] _dependencyIndices;
		[SerializeField] private string _error;

		/// <summary>
		/// Short name of the system type.
		/// </summary>
		public string TypeName => _typeName;

		/// <summary>
		/// Namespace-qualified name of the system type.
		/// </summary>
		public string FullTypeName => _fullTypeName;

		/// <summary>
		/// Order in which the system started, counted across the whole run.
		/// </summary>
		public int StartOrder => _startOrder;

		/// <summary>
		/// Whether the system was marked as critical.
		/// </summary>
		public bool IsCritical => _isCritical;

		/// <summary>
		/// Final status of the system.
		/// </summary>
		public InitializationSystemStatus Status => _status;

		/// <summary>
		/// Offset from the start of the initialization run to the start of the system, in milliseconds.
		/// </summary>
		public double StartMilliseconds => _startMilliseconds;

		/// <summary>
		/// Duration of the system initialization, in milliseconds.
		/// </summary>
		public double DurationMilliseconds => _durationMilliseconds;

		/// <summary>
		/// Indices, inside <see cref="InitializationGraphSnapshot.Systems"/>, of the systems this one depends on.
		/// </summary>
		public IReadOnlyList<int> DependencyIndices => _dependencyIndices ?? Array.Empty<int>();

		/// <summary>
		/// Text of the exception that failed the system, or an empty string when there was none.
		/// </summary>
		public string Error => _error ?? string.Empty;

		internal InitializationSystemRecord(
			string typeName,
			string fullTypeName,
			int startOrder,
			bool isCritical,
			InitializationSystemStatus status,
			double startMilliseconds,
			double durationMilliseconds,
			int[] dependencyIndices,
			string error)
		{
			_typeName = typeName;
			_fullTypeName = fullTypeName;
			_startOrder = startOrder;
			_isCritical = isCritical;
			_status = status;
			_startMilliseconds = startMilliseconds;
			_durationMilliseconds = durationMilliseconds;
			_dependencyIndices = dependencyIndices;
			_error = error;
		}
	}
}
