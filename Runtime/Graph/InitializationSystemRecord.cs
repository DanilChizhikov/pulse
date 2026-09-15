using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Serializable]
	[Preserve]
	public sealed class InitializationSystemRecord
	{
		[SerializeField] private string _typeName;
		[SerializeField] private string _fullTypeName;
		[SerializeField] private int _batchIndex;
		[SerializeField] private int _startOrder;
		[SerializeField] private bool _isCritical;
		[SerializeField] private InitializationSystemStatus _status;
		[SerializeField] private double _startMilliseconds;
		[SerializeField] private double _durationMilliseconds;
		[SerializeField] private int[] _dependencyIndices;
		[SerializeField] private string _error;

		public string TypeName => _typeName;
		public string FullTypeName => _fullTypeName;
		public int BatchIndex => _batchIndex;
		public int StartOrder => _startOrder;
		public bool IsCritical => _isCritical;
		public InitializationSystemStatus Status => _status;
		public double StartMilliseconds => _startMilliseconds;
		public double DurationMilliseconds => _durationMilliseconds;
		public IReadOnlyList<int> DependencyIndices => _dependencyIndices ?? Array.Empty<int>();
		public string Error => _error ?? string.Empty;

		internal InitializationSystemRecord(
			string typeName,
			string fullTypeName,
			int batchIndex,
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
			_batchIndex = batchIndex;
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
