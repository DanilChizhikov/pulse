using System.Collections.Generic;

namespace DTech.Pulse.Editor
{
	/// <summary>
	/// Derives the dependency levels of a recorded graph: a system sits one level below its deepest dependency.
	/// </summary>
	/// <remarks>
	/// Levels are a presentation detail. Pulse does not initialize level by level: a system starts as soon as
	/// its own dependencies are done, so systems of the same level may run at completely different times.
	/// </remarks>
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
			var levels = new int[systems.Count];
			for (int i = 0; i < levels.Length; i++)
			{
				levels[i] = UnknownLevel;
			}

			for (int i = 0; i < levels.Length; i++)
			{
				Resolve(systems, levels, i);
			}

			return levels;
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

		private static int Resolve(IReadOnlyList<InitializationSystemRecord> systems, int[] levels, int index)
		{
			if (levels[index] >= 0)
			{
				return levels[index];
			}

			if (levels[index] == VisitingLevel)
			{
				return 0;
			}

			levels[index] = VisitingLevel;

			int level = 0;
			IReadOnlyList<int> dependencyIndices = systems[index].DependencyIndices;
			for (int i = 0; i < dependencyIndices.Count; i++)
			{
				int dependencyIndex = dependencyIndices[i];
				if (dependencyIndex < 0 || dependencyIndex >= systems.Count || dependencyIndex == index)
				{
					continue;
				}

				int dependencyLevel = Resolve(systems, levels, dependencyIndex);
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
