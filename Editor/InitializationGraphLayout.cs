using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.Pulse.Editor
{
	internal sealed class InitializationGraphLayout
	{
		public static InitializationGraphLayout Empty { get; } = new(
			Array.Empty<InitializationSystemRecord>(),
			Array.Empty<int>(),
			Array.Empty<bool>(),
			Array.Empty<bool[]>(),
			Array.Empty<int>(),
			0d,
			Array.Empty<LevelEntry>());

		public IReadOnlyList<InitializationSystemRecord> Systems { get; }
		public int[] Levels { get; }
		public bool[] CriticalSystems { get; }
		public bool[][] RedundantDependencies { get; }
		public int[] Rows { get; }
		public double MaxDurationMilliseconds { get; }
		public IReadOnlyList<LevelEntry> LevelEntries { get; }
		public int LevelsCount => LevelEntries.Count;

		public int CriticalCount
		{
			get
			{
				int count = 0;
				for (int i = 0; i < CriticalSystems.Length; i++)
				{
					if (CriticalSystems[i])
					{
						count++;
					}
				}

				return count;
			}
		}

		private InitializationGraphLayout(
			IReadOnlyList<InitializationSystemRecord> systems,
			int[] levels,
			bool[] criticalSystems,
			bool[][] redundantDependencies,
			int[] rows,
			double maxDurationMilliseconds,
			IReadOnlyList<LevelEntry> levelEntries)
		{
			Systems = systems;
			Levels = levels;
			CriticalSystems = criticalSystems;
			RedundantDependencies = redundantDependencies;
			Rows = rows;
			MaxDurationMilliseconds = maxDurationMilliseconds;
			LevelEntries = levelEntries;
		}

		public static InitializationGraphLayout Build(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null || snapshot.Systems.Count == 0)
			{
				return Empty;
			}

			IReadOnlyList<InitializationSystemRecord> systems = snapshot.Systems;
			int[] levels = InitializationGraphLevels.Calculate(snapshot);
			bool[] criticalSystems = InitializationGraphLevels.ResolveCriticalSystems(snapshot);
			bool[][] redundantDependencies = InitializationGraphReduction.FindRedundantDependencies(systems, levels);
			double maxDurationMilliseconds = systems.Max(system => system.DurationMilliseconds);

			var rows = new int[systems.Count];
			var levelEntries = new List<LevelEntry>();
			IEnumerable<IGrouping<int, int>> groupedLevels = Enumerable.Range(0, systems.Count)
				.GroupBy(index => levels[index])
				.OrderBy(level => level.Key);

			foreach (IGrouping<int, int> level in groupedLevels)
			{
				int[] orderedIndices = level
					.OrderBy(index => GetStartRank(systems[index]))
					.ThenBy(index => systems[index].TypeName, StringComparer.Ordinal)
					.ToArray();

				bool isCritical = false;
				for (int row = 0; row < orderedIndices.Length; row++)
				{
					int index = orderedIndices[row];
					rows[index] = row;
					isCritical |= criticalSystems[index];
				}

				string title = GetLevelTitle(systems, level.Key, isCritical, orderedIndices);
				levelEntries.Add(new LevelEntry(level.Key, title, isCritical, orderedIndices));
			}

			return new InitializationGraphLayout(
				systems,
				levels,
				criticalSystems,
				redundantDependencies,
				rows,
				maxDurationMilliseconds,
				levelEntries);
		}

		public bool IsAutoCritical(int index)
		{
			InitializationSystemRecord system = Systems[index];
			return CriticalSystems[index] && (system.IsAutoCritical || !system.IsCritical);
		}

		public bool IsRedundantDependency(int index, int dependencyPosition)
		{
			bool[] redundant = RedundantDependencies[index];
			return redundant != null && dependencyPosition < redundant.Length && redundant[dependencyPosition];
		}

		private static int GetStartRank(InitializationSystemRecord system)
		{
			return system.StartOrder >= 0 ? system.StartOrder : int.MaxValue;
		}

		private static string GetLevelTitle(
			IReadOnlyList<InitializationSystemRecord> systems,
			int level,
			bool isCritical,
			IReadOnlyList<int> indices)
		{
			bool isStarted = false;
			double minStartMilliseconds = double.MaxValue;
			double maxEndMilliseconds = double.MinValue;

			for (int i = 0; i < indices.Count; i++)
			{
				InitializationSystemRecord system = systems[indices[i]];
				if (system.StartOrder < 0)
				{
					continue;
				}

				isStarted = true;
				minStartMilliseconds = Math.Min(minStartMilliseconds, system.StartMilliseconds);
				maxEndMilliseconds = Math.Max(
					maxEndMilliseconds,
					system.StartMilliseconds + system.DurationMilliseconds);
			}

			string prefix = isCritical ? "Critical · " : string.Empty;
			return isStarted
				? $"{prefix}Level {level} · {InitializationTimeFormat.Format(maxEndMilliseconds - minStartMilliseconds)}"
				: $"{prefix}Level {level} · not started";
		}

		public readonly struct LevelEntry
		{
			public readonly int Level;
			public readonly string Title;
			public readonly bool IsCritical;
			public readonly int[] Indices;

			public LevelEntry(int level, string title, bool isCritical, int[] indices)
			{
				Level = level;
				Title = title;
				IsCritical = isCritical;
				Indices = indices;
			}
		}
	}
}