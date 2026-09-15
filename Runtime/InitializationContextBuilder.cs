using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	/// <summary>
	/// Collects systems and their dependencies and builds an <see cref="InitializationContext"/> out of them.
	/// </summary>
	/// <remarks>
	/// A builder produces a single context: after <see cref="Build"/> it can no longer be used.
	/// </remarks>
	[Preserve]
	public sealed class InitializationContextBuilder
	{
		private readonly List<InitializationNode> _nodes = new();
		private readonly Dictionary<Type, InitializationNode> _nodesByType = new();

		private IInitializationFramePacer _framePacer;
		private bool _isBuilt;

		/// <summary>
		/// Registers a system and resolves the dependencies declared with <see cref="InitDependencyAttribute"/>.
		/// </summary>
		/// <param name="system">System instance to initialize.</param>
		/// <returns>A handle used to tune the dependencies and callbacks of the system.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="system"/> is null.</exception>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the context is already built or the system type is already registered.
		/// </exception>
		public IInitializationNodeHandle AddSystem(IInitializable system)
		{
			if (_isBuilt)
			{
				throw new InvalidOperationException("This builder has already built an initialization context.");
			}

			if (system == null)
			{
				throw new ArgumentNullException(nameof(system));
			}

			Type systemType = system.GetType();
			if (_nodesByType.ContainsKey(systemType))
			{
				throw new InvalidOperationException($"System '{systemType.FullName}' is already registered.");
			}

			var node = new InitializationNode(system);
			Type[] dependencies = system.GetDependencies();
			node.AddDependencies(dependencies);

			_nodes.Add(node);
			_nodesByType.Add(systemType, node);

			return node;
		}

		/// <summary>
		/// Sets the frame pacer used to postpone systems while the current frame is overloaded.
		/// </summary>
		/// <param name="framePacer">Pacer implementation, or null to initialize without any frame gate.</param>
		/// <returns>The same builder, allowing calls to be chained.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the context is already built.</exception>
		public InitializationContextBuilder SetFramePacer(IInitializationFramePacer framePacer)
		{
			if (_isBuilt)
			{
				throw new InvalidOperationException("This builder has already built an initialization context.");
			}

			_framePacer = framePacer;
			return this;
		}

		/// <summary>
		/// Validates the dependency graph and creates the context.
		/// </summary>
		/// <returns>The initialization context ready to be run.</returns>
		/// <exception cref="InvalidOperationException">
		/// Thrown when the context is already built, a dependency is missing or ambiguous, or the graph contains a cycle.
		/// </exception>
		public InitializationContext Build()
		{
			if (_isBuilt)
			{
				throw new InvalidOperationException("This builder has already built an initialization context.");
			}

			BuildPlan(
				out InitializationNode[] nodes,
				out int[] blockingDependencies,
				out int[][] dependents,
				out int criticalSystemsCount);
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i].SetProcessed();
			}

			InitializationGraphRecorder graphRecorder = InitializationGraphRecording.IsEnabled
				? new InitializationGraphRecorder(nodes, dependents)
				: null;

			_isBuilt = true;
			return new InitializationContext(
				nodes,
				blockingDependencies,
				dependents,
				criticalSystemsCount,
				graphRecorder,
				_framePacer);
		}

		private void BuildPlan(
			out InitializationNode[] nodes,
			out int[] blockingDependencies,
			out int[][] dependents,
			out int criticalSystemsCount)
		{
			int count = _nodes.Count;
			nodes = _nodes.ToArray();
			blockingDependencies = new int[count];
			dependents = new int[count][];
			criticalSystemsCount = 0;

			var indices = new Dictionary<InitializationNode, int>(count);
			for (int i = 0; i < count; i++)
			{
				indices.Add(nodes[i], i);
			}

			var dependentLists = new List<int>[count];
			for (int i = 0; i < count; i++)
			{
				InitializationNode node = nodes[i];
				List<Type> dependencies = node.GetDependencies();
				for (int j = 0; j < dependencies.Count; j++)
				{
					InitializationNode dependencyNode = ResolveDependencyNode(node, dependencies[j]);
					int dependencyIndex = indices[dependencyNode];
					(dependentLists[dependencyIndex] ??= new List<int>()).Add(i);
					blockingDependencies[i]++;
				}

				if (node.IsCritical)
				{
					criticalSystemsCount++;
				}
			}

			for (int i = 0; i < count; i++)
			{
				dependents[i] = dependentLists[i]?.ToArray() ?? Array.Empty<int>();
			}

			ValidateNoCycles(nodes, blockingDependencies, dependents);
		}

		private static void ValidateNoCycles(
			InitializationNode[] nodes,
			int[] blockingDependencies,
			int[][] dependents)
		{
			int count = nodes.Length;
			var remainingDependencies = new int[count];
			Array.Copy(blockingDependencies, remainingDependencies, count);

			var queue = new Queue<int>();
			for (int i = 0; i < count; i++)
			{
				if (remainingDependencies[i] == 0)
				{
					queue.Enqueue(i);
				}
			}

			int processedCount = 0;
			while (queue.Count > 0)
			{
				int index = queue.Dequeue();
				processedCount++;

				int[] nodeDependents = dependents[index];
				for (int i = 0; i < nodeDependents.Length; i++)
				{
					int dependentIndex = nodeDependents[i];
					if (--remainingDependencies[dependentIndex] == 0)
					{
						queue.Enqueue(dependentIndex);
					}
				}
			}

			if (processedCount == count)
			{
				return;
			}

			IEnumerable<string> cycleNames = Enumerable.Range(0, count)
				.Where(index => remainingDependencies[index] > 0)
				.Select(index => nodes[index].SystemType.Name);
			throw new InvalidOperationException("Cyclic dependencies detected: " + string.Join(", ", cycleNames));
		}

		private InitializationNode ResolveDependencyNode(InitializationNode node, Type dependencyType)
		{
			if (_nodesByType.TryGetValue(dependencyType, out InitializationNode exactNode))
			{
				return exactNode;
			}

			List<InitializationNode> candidates = _nodes
				.Where(candidate => dependencyType.IsAssignableFrom(candidate.SystemType))
				.ToList();
			if (candidates.Count == 1)
			{
				return candidates[0];
			}

			if (candidates.Count > 1)
			{
				string candidateNames = string.Join(", ", candidates.Select(candidate => candidate.SystemType.FullName));
				throw new InvalidOperationException(
					$"System '{node.SystemType.FullName}' has dependency '{dependencyType.FullName}', " +
					$"but it matches multiple registered systems: {candidateNames}. " +
					"Register a concrete dependency type or remove the ambiguity.");
			}

			throw new InvalidOperationException($"System '{node.SystemType.FullName}' has dependency '{dependencyType.FullName}' " +
				$"which was not added to '{nameof(InitializationContextBuilder)}'. " +
				"All dependencies must be registered via AddSystem before Build is called.");
		}
	}
}
