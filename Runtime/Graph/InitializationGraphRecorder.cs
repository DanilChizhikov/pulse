using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	internal sealed class InitializationGraphRecorder
	{
		private readonly Dictionary<InitializationNode, int> _indices;
		private readonly SystemEntry[] _systems;
		private readonly BatchEntry[] _batches;

		private long _startTimestamp;
		private int _startedSystemsCount;

		internal InitializationGraphRecorder(
			IReadOnlyList<ICollection<InitializationNode>> batches,
			IReadOnlyDictionary<InitializationNode, List<InitializationNode>> dependents)
		{
			_indices = new Dictionary<InitializationNode, int>();
			var systems = new List<SystemEntry>();
			for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
			{
				foreach (InitializationNode node in batches[batchIndex])
				{
					_indices.Add(node, systems.Count);
					systems.Add(new SystemEntry(node, batchIndex));
				}
			}

			_systems = systems.ToArray();
			_batches = new BatchEntry[batches.Count];

			foreach (KeyValuePair<InitializationNode, List<InitializationNode>> pair in dependents)
			{
				int dependencyIndex = _indices[pair.Key];
				foreach (InitializationNode dependent in pair.Value)
				{
					List<int> dependencyIndices = _systems[_indices[dependent]].DependencyIndices;
					if (!dependencyIndices.Contains(dependencyIndex))
					{
						dependencyIndices.Add(dependencyIndex);
					}
				}
			}

			foreach (SystemEntry system in _systems)
			{
				system.DependencyIndices.Sort();
			}
		}

		internal void Begin()
		{
			_startTimestamp = Stopwatch.GetTimestamp();
		}

		internal void MarkBatchStarted(int batchIndex)
		{
			_batches[batchIndex].StartTimestamp = Stopwatch.GetTimestamp();
			_batches[batchIndex].IsStarted = true;
		}

		internal void MarkBatchCompleted(int batchIndex)
		{
			_batches[batchIndex].EndTimestamp = Stopwatch.GetTimestamp();
			_batches[batchIndex].IsCompleted = true;
		}

		internal void MarkSystemStarted(InitializationNode node)
		{
			SystemEntry system = _systems[_indices[node]];
			system.StartOrder = Interlocked.Increment(ref _startedSystemsCount) - 1;
			system.StartTimestamp = Stopwatch.GetTimestamp();
		}

		internal void MarkSystemCompleted(InitializationNode node)
		{
			SystemEntry system = _systems[_indices[node]];
			system.EndTimestamp = Stopwatch.GetTimestamp();
			system.IsFinished = true;
			system.Status = InitializationSystemStatus.Completed;
		}

		internal void MarkSystemFailed(InitializationNode node, Exception exception)
		{
			SystemEntry system = _systems[_indices[node]];
			system.EndTimestamp = Stopwatch.GetTimestamp();
			system.IsFinished = true;
			system.Status = exception is OperationCanceledException
				? InitializationSystemStatus.Cancelled
				: InitializationSystemStatus.Failed;
			system.Error = $"{exception.GetType().Name}: {exception.Message}";
		}

		internal void Complete(InitializationGraphStatus status)
		{
			long endTimestamp = Stopwatch.GetTimestamp();

			var batches = new List<InitializationBatchRecord>(_batches.Length);
			for (int i = 0; i < _batches.Length; i++)
			{
				BatchEntry batch = _batches[i];
				if (!batch.IsStarted)
				{
					continue;
				}

				long batchEndTimestamp = batch.IsCompleted ? batch.EndTimestamp : endTimestamp;
				batches.Add(new InitializationBatchRecord(
					i,
					ToMilliseconds(batch.StartTimestamp - _startTimestamp),
					ToMilliseconds(batchEndTimestamp - batch.StartTimestamp)));
			}

			var systems = new List<InitializationSystemRecord>(_systems.Length);
			foreach (SystemEntry system in _systems)
			{
				bool isStarted = system.StartOrder >= 0;
				long systemEndTimestamp = system.IsFinished ? system.EndTimestamp : endTimestamp;
				systems.Add(new InitializationSystemRecord(
					system.TypeName,
					system.FullTypeName,
					system.BatchIndex,
					system.StartOrder,
					system.IsCritical,
					system.Status,
					isStarted ? ToMilliseconds(system.StartTimestamp - _startTimestamp) : 0d,
					isStarted ? ToMilliseconds(systemEndTimestamp - system.StartTimestamp) : 0d,
					system.DependencyIndices.ToArray(),
					system.Error));
			}

			var snapshot = new InitializationGraphSnapshot(
				DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
				status,
				ToMilliseconds(endTimestamp - _startTimestamp),
				batches,
				systems);

			InitializationGraphRecording.Publish(snapshot);
		}

		private static double ToMilliseconds(long timestampDelta)
		{
			return timestampDelta * 1000d / Stopwatch.Frequency;
		}

		private struct BatchEntry
		{
			public long StartTimestamp;
			public long EndTimestamp;
			public bool IsStarted;
			public bool IsCompleted;
		}

		private sealed class SystemEntry
		{
			public readonly string TypeName;
			public readonly string FullTypeName;
			public readonly int BatchIndex;
			public readonly bool IsCritical;
			public readonly List<int> DependencyIndices = new();

			public int StartOrder = -1;
			public long StartTimestamp;
			public long EndTimestamp;
			public bool IsFinished;
			public InitializationSystemStatus Status = InitializationSystemStatus.NotStarted;
			public string Error = string.Empty;

			public SystemEntry(InitializationNode node, int batchIndex)
			{
				TypeName = node.SystemType.Name;
				FullTypeName = node.SystemType.FullName;
				BatchIndex = batchIndex;
				IsCritical = node.IsCritical;
			}
		}
	}
}
