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
		private readonly SystemEntry[] _systems;

		private long _startTimestamp;
		private int _startedSystemsCount;

		internal InitializationGraphRecorder(InitializationNode[] nodes, int[][] dependents)
		{
			_systems = new SystemEntry[nodes.Length];
			for (int i = 0; i < nodes.Length; i++)
			{
				_systems[i] = new SystemEntry(nodes[i]);
			}

			for (int i = 0; i < dependents.Length; i++)
			{
				int[] nodeDependents = dependents[i];
				for (int j = 0; j < nodeDependents.Length; j++)
				{
					List<int> dependencyIndices = _systems[nodeDependents[j]].DependencyIndices;
					if (!dependencyIndices.Contains(i))
					{
						dependencyIndices.Add(i);
					}
				}
			}

			foreach (SystemEntry system in _systems)
			{
				system.DependencyIndices.Sort();
			}
		}

		public void Begin()
		{
			_startTimestamp = Stopwatch.GetTimestamp();
		}

		public void MarkSystemStarted(int index)
		{
			SystemEntry system = _systems[index];
			system.StartOrder = Interlocked.Increment(ref _startedSystemsCount) - 1;
			system.StartTimestamp = Stopwatch.GetTimestamp();
		}

		public void MarkSystemCompleted(int index)
		{
			SystemEntry system = _systems[index];
			system.EndTimestamp = Stopwatch.GetTimestamp();
			system.IsFinished = true;
			system.Status = InitializationSystemStatus.Completed;
		}

		public void MarkSystemFailed(int index, Exception exception)
		{
			SystemEntry system = _systems[index];
			system.EndTimestamp = Stopwatch.GetTimestamp();
			system.IsFinished = true;
			system.Status = exception is OperationCanceledException
				? InitializationSystemStatus.Cancelled
				: InitializationSystemStatus.Failed;
			system.Error = $"{exception.GetType().Name}: {exception.Message}";
		}

		public void Complete(InitializationGraphStatus status)
		{
			long endTimestamp = Stopwatch.GetTimestamp();

			var systems = new List<InitializationSystemRecord>(_systems.Length);
			foreach (SystemEntry system in _systems)
			{
				bool isStarted = system.StartOrder >= 0;
				long systemEndTimestamp = system.IsFinished ? system.EndTimestamp : endTimestamp;
				systems.Add(new InitializationSystemRecord(
					system.TypeName,
					system.FullTypeName,
					system.StartOrder,
					system.IsCritical,
					system.IsAutoCritical,
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
				systems);

			InitializationGraphRecording.Publish(snapshot);
		}

		private static double ToMilliseconds(long timestampDelta)
		{
			return timestampDelta * 1000d / Stopwatch.Frequency;
		}

		private sealed class SystemEntry
		{
			public string TypeName { get; }
			public string FullTypeName { get; }
			public bool IsCritical { get; }
			public bool IsAutoCritical { get; }
			public List<int> DependencyIndices { get; } = new();

			public int StartOrder { get; set; } = -1;
			public long StartTimestamp { get; set; }
			public long EndTimestamp { get; set; }
			public bool IsFinished { get; set; }
			public InitializationSystemStatus Status { get; set; } = InitializationSystemStatus.NotStarted;
			public string Error { get; set; } = string.Empty;

			public SystemEntry(InitializationNode node)
			{
				TypeName = node.SystemType.Name;
				FullTypeName = node.SystemType.FullName;
				IsCritical = node.IsCritical;
				IsAutoCritical = node.IsAutoCritical;
			}
		}
	}
}
