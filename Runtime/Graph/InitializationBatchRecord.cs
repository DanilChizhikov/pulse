using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Serializable]
	[Preserve]
	public sealed class InitializationBatchRecord
	{
		[SerializeField] private int _index;
		[SerializeField] private double _startMilliseconds;
		[SerializeField] private double _durationMilliseconds;

		public int Index => _index;
		public double StartMilliseconds => _startMilliseconds;
		public double DurationMilliseconds => _durationMilliseconds;

		internal InitializationBatchRecord(int index, double startMilliseconds, double durationMilliseconds)
		{
			_index = index;
			_startMilliseconds = startMilliseconds;
			_durationMilliseconds = durationMilliseconds;
		}
	}
}
