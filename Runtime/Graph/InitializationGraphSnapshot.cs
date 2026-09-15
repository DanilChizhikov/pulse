using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Serializable result of a single initialization run: its systems, their order and timings.
	/// </summary>
	[Serializable]
	[Preserve]
	public sealed class InitializationGraphSnapshot
	{
		[SerializeField] private string _recordedAtUtc;
		[SerializeField] private InitializationGraphStatus _status;
		[SerializeField] private double _totalMilliseconds;
		[SerializeField] private List<InitializationSystemRecord> _systems;
		
		/// <summary>
		/// UTC time the snapshot was recorded at, in round-trip ("O") format.
		/// </summary>
		public string RecordedAtUtc => _recordedAtUtc;

		/// <summary>
		/// Final status of the recorded initialization run.
		/// </summary>
		public InitializationGraphStatus Status => _status;

		/// <summary>
		/// Total duration of the initialization run, in milliseconds.
		/// </summary>
		public double TotalMilliseconds => _totalMilliseconds;

		/// <summary>
		/// Recorded systems, ordered the same way they were registered in the builder.
		/// </summary>
		public IReadOnlyList<InitializationSystemRecord> Systems => _systems;

		internal InitializationGraphSnapshot(
			string recordedAtUtc,
			InitializationGraphStatus status,
			double totalMilliseconds,
			List<InitializationSystemRecord> systems)
		{
			_recordedAtUtc = recordedAtUtc;
			_status = status;
			_totalMilliseconds = totalMilliseconds;
			_systems = systems ?? throw new ArgumentNullException(nameof(systems));
		}

		/// <summary>
		/// Serializes the snapshot to JSON.
		/// </summary>
		/// <param name="prettyPrint">Whether the JSON should be formatted for reading.</param>
		/// <returns>The JSON representation of the snapshot.</returns>
		public string ToJson(bool prettyPrint = false)
		{
			return JsonUtility.ToJson(this, prettyPrint);
		}

		/// <summary>
		/// Restores a snapshot from its JSON representation.
		/// </summary>
		/// <param name="json">JSON produced by <see cref="ToJson"/>.</param>
		/// <returns>The deserialized snapshot.</returns>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="json"/> is null, empty or does not contain a snapshot.
		/// </exception>
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

			snapshot._systems ??= new List<InitializationSystemRecord>();
			return snapshot;
		}
	}
}
