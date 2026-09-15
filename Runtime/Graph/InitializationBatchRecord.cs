using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Timings of a single batch of systems initialized in parallel.
	/// </summary>
	[Serializable]
	[Preserve]
	public sealed class InitializationBatchRecord
	{
		[SerializeField] private int _index;
		[SerializeField] private double _startMilliseconds;
		[SerializeField] private double _durationMilliseconds;

		/// <summary>
		/// Zero-based position of the batch in the execution order.
		/// </summary>
		public int Index => _index;

		/// <summary>
		/// Offset from the start of the initialization run to the start of the batch, in milliseconds.
		/// </summary>
		public double StartMilliseconds => _startMilliseconds;

		/// <summary>
		/// Duration of the batch, in milliseconds.
		/// </summary>
		public double DurationMilliseconds => _durationMilliseconds;

		internal InitializationBatchRecord(int index, double startMilliseconds, double durationMilliseconds)
		{
			_index = index;
			_startMilliseconds = startMilliseconds;
			_durationMilliseconds = durationMilliseconds;
		}
	}
}
