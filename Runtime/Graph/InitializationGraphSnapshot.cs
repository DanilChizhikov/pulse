using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Serializable]
	[Preserve]
	public sealed class InitializationGraphSnapshot
	{
		[SerializeField] private string _recordedAtUtc;
		[SerializeField] private InitializationGraphStatus _status;
		[SerializeField] private double _totalMilliseconds;
		[SerializeField] private List<InitializationBatchRecord> _batches;
		[SerializeField] private List<InitializationSystemRecord> _systems;
		
		public string RecordedAtUtc => _recordedAtUtc;
		public InitializationGraphStatus Status => _status;
		public double TotalMilliseconds => _totalMilliseconds;
		public IReadOnlyList<InitializationBatchRecord> Batches => _batches;
		public IReadOnlyList<InitializationSystemRecord> Systems => _systems;

		internal InitializationGraphSnapshot(
			string recordedAtUtc,
			InitializationGraphStatus status,
			double totalMilliseconds,
			List<InitializationBatchRecord> batches,
			List<InitializationSystemRecord> systems)
		{
			_recordedAtUtc = recordedAtUtc;
			_status = status;
			_totalMilliseconds = totalMilliseconds;
			_batches = batches ?? throw new ArgumentNullException(nameof(batches));
			_systems = systems ?? throw new ArgumentNullException(nameof(systems));
		}

		public string ToJson(bool prettyPrint = false)
		{
			return JsonUtility.ToJson(this, prettyPrint);
		}

		public static InitializationGraphSnapshot FromJson(string json)
		{
			if (string.IsNullOrEmpty(json))
			{
				throw new ArgumentException("Json cannot be null or empty.", nameof(json));
			}

			var snapshot = JsonUtility.FromJson<InitializationGraphSnapshot>(json);
			if (snapshot == null)
			{
				throw new ArgumentException("Json does not contain an initialization graph snapshot.", nameof(json));
			}

			snapshot._batches ??= new List<InitializationBatchRecord>();
			snapshot._systems ??= new List<InitializationSystemRecord>();
			return snapshot;
		}
	}
}
