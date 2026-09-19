using System.Collections.Generic;

namespace DTech.Pulse.Editor
{
	internal static class InitializationGraphLevels
	{
		private const int UnknownLevel = -1;
		private const int VisitingLevel = -2;

		public static int[] Calculate(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null || snapshot.Systems.Count == 0)
			{
				return System.Array.Empty<int>();
			}

			IReadOnlyList<InitializationSystemRecord> systems = snapshot.Systems;
			bool[] criticalSystems = ResolveCriticalSystems(snapshot);
			var levels = new int[systems.Count];
			for (int i = 0; i < levels.Length; i++)
			{
				levels[i] = UnknownLevel;
			}

			int criticalLevelsCount = 0;
			for (int i = 0; i < levels.Length; i++)
			{
				if (!criticalSystems[i])
				{
					continue;
				}

				int level = Resolve(systems, levels, criticalSystems, i, true, 0);
				if (level >= criticalLevelsCount)
				{
					criticalLevelsCount = level + 1;
				}
			}

			for (int i = 0; i < levels.Length; i++)
			{
				if (criticalSystems[i])
				{
					continue;
				}

				Resolve(systems, levels, criticalSystems, i, false, criticalLevelsCount);
			}

			return levels;
		}
		
		public static bool[] ResolveCriticalSystems(InitializationGraphSnapshot snapshot)
		{
			if (snapshot == null || snapshot.Systems.Count == 0)
			{
				return System.Array.Empty<bool>();
			}

			IReadOnlyList<InitializationSystemRecord> systems = snapshot.Systems;
			var criticalSystems = new bool[systems.Count];
			var queue = new Queue<int>();
			for (int i = 0; i < systems.Count; i++)
			{
				if (!systems[i].IsCritical)
				{
					continue;
				}

				criticalSystems[i] = true;
				queue.Enqueue(i);
			}

			while (queue.Count > 0)
			{
				IReadOnlyList<int> dependencyIndices = systems[queue.Dequeue()].DependencyIndices;
				for (int i = 0; i < dependencyIndices.Count; i++)
				{
					int dependencyIndex = dependencyIndices[i];
					if (dependencyIndex < 0 || dependencyIndex >= systems.Count || criticalSystems[dependencyIndex])
					{
						continue;
					}

					criticalSystems[dependencyIndex] = true;
					queue.Enqueue(dependencyIndex);
				}
			}

			return criticalSystems;
		}

		public static int GetCount(int[] levels)
		{
			int count = 0;
			for (int i = 0; i < levels.Length; i++)
			{
				if (levels[i] >= count)
				{
					count = levels[i] + 1;
				}
			}

			return count;
		}

		private static int Resolve(
			IReadOnlyList<InitializationSystemRecord> systems,
			int[] levels,
			bool[] criticalSystems,
			int index,
			bool isCriticalPass,
			int minimumLevel)
		{
			if (levels[index] >= 0)
			{
				return levels[index];
			}

			if (levels[index] == VisitingLevel)
			{
				return minimumLevel;
			}

			levels[index] = VisitingLevel;

			int level = minimumLevel;
			IReadOnlyList<int> dependencyIndices = systems[index].DependencyIndices;
			for (int i = 0; i < dependencyIndices.Count; i++)
			{
				int dependencyIndex = dependencyIndices[i];
				if (dependencyIndex < 0 || dependencyIndex >= systems.Count || dependencyIndex == index)
				{
					continue;
				}
				
				if (isCriticalPass && !criticalSystems[dependencyIndex])
				{
					continue;
				}

				int dependencyLevel = Resolve(
					systems,
					levels,
					criticalSystems,
					dependencyIndex,
					criticalSystems[dependencyIndex],
					criticalSystems[dependencyIndex] ? 0 : minimumLevel);
				if (dependencyLevel + 1 > level)
				{
					level = dependencyLevel + 1;
				}
			}

			levels[index] = level;
			return level;
		}
	}
}