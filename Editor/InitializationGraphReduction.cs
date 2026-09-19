using System;
using System.Collections;
using System.Collections.Generic;

namespace DTech.Pulse.Editor
{
	internal static class InitializationGraphReduction
	{
		public static bool[][] FindRedundantDependencies(IReadOnlyList<InitializationSystemRecord> systems, int[] levels)
		{
			if (levels.Length != systems.Count)
			{
				throw new ArgumentException("Levels must be calculated for the same systems.", nameof(levels));
			}

			int count = systems.Count;
			var redundantDependencies = new bool[count][];
			var ancestors = new BitArray[count];

			foreach (int index in GetLevelOrder(levels))
			{
				IReadOnlyList<int> dependencyIndices = systems[index].DependencyIndices;

				var systemAncestors = new BitArray(count);
				for (int i = 0; i < dependencyIndices.Count; i++)
				{
					int dependencyIndex = dependencyIndices[i];
					if (!IsTracked(dependencyIndex, index, levels))
					{
						continue;
					}

					systemAncestors.Or(ancestors[dependencyIndex]);
					systemAncestors[dependencyIndex] = true;
				}

				ancestors[index] = systemAncestors;
				redundantDependencies[index] = FindRedundant(dependencyIndices, index, levels, ancestors);
			}

			return redundantDependencies;
		}

		private static bool[] FindRedundant(
			IReadOnlyList<int> dependencyIndices,
			int index,
			int[] levels,
			BitArray[] ancestors)
		{
			var isRedundant = new bool[dependencyIndices.Count];
			for (int i = 0; i < dependencyIndices.Count; i++)
			{
				int dependencyIndex = dependencyIndices[i];
				if (!IsTracked(dependencyIndex, index, levels))
				{
					continue;
				}

				for (int j = 0; j < dependencyIndices.Count; j++)
				{
					int otherIndex = dependencyIndices[j];
					if (otherIndex == dependencyIndex || !IsTracked(otherIndex, index, levels))
					{
						continue;
					}

					if (ancestors[otherIndex][dependencyIndex])
					{
						isRedundant[i] = true;
						break;
					}
				}
			}

			return isRedundant;
		}
		
		private static bool IsTracked(int dependencyIndex, int index, int[] levels)
		{
			return dependencyIndex >= 0 &&
				dependencyIndex < levels.Length &&
				dependencyIndex != index &&
				levels[dependencyIndex] < levels[index];
		}

		private static int[] GetLevelOrder(int[] levels)
		{
			var order = new int[levels.Length];
			for (int i = 0; i < order.Length; i++)
			{
				order[i] = i;
			}

			var keys = (int[])levels.Clone();
			Array.Sort(keys, order);
			return order;
		}
	}
}