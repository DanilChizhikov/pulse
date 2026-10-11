using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
		private InitializationContext _parent;
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
		/// Sets the context whose systems are already initialized by the time this one runs, e.g. the context of a
		/// parent scope. A dependency that is not registered in this builder is looked up in the parent context and
		/// its own parents; when found there it is treated as satisfied. A system registered in this builder takes
		/// precedence over the parent ones. The parent context must be initialized before this context is run.
		/// </summary>
		/// <param name="parent">Parent context, or null to resolve dependencies only among the systems of this builder.</param>
		/// <returns>The same builder, allowing calls to be chained.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the context is already built.</exception>
		public InitializationContextBuilder SetParent(InitializationContext parent)
		{
			if (_isBuilt)
			{
				throw new InvalidOperationException("This builder has already built an initialization context.");
			}

			_parent = parent;
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
				_framePacer,
				_parent);
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
			var dependencyLists = new List<int>[count];
			for (int i = 0; i < count; i++)
			{
				InitializationNode node = nodes[i];
				List<Type> dependencies = node.GetDependencies();
				for (int j = 0; j < dependencies.Count; j++)
				{
					InitializationNode dependencyNode = ResolveDependencyNode(node, dependencies[j]);
					if (dependencyNode == null)
					{
						continue;
					}

					int dependencyIndex = indices[dependencyNode];
					(dependentLists[dependencyIndex] ??= new List<int>()).Add(i);
					(dependencyLists[i] ??= new List<int>()).Add(dependencyIndex);
					blockingDependencies[i]++;
				}
			}

			for (int i = 0; i < count; i++)
			{
				dependents[i] = dependentLists[i]?.ToArray() ?? Array.Empty<int>();
			}

			ValidateNoCycles(nodes, blockingDependencies, dependents);
			PromoteCriticalDependencies(nodes, dependencyLists);

			for (int i = 0; i < count; i++)
			{
				if (nodes[i].IsCritical)
				{
					criticalSystemsCount++;
				}
			}
		}

		private static void PromoteCriticalDependencies(InitializationNode[] nodes, List<int>[] dependencyLists)
		{
			var queue = new Queue<int>();
			for (int i = 0; i < nodes.Length; i++)
			{
				if (nodes[i].IsCritical)
				{
					queue.Enqueue(i);
				}
			}

			List<string> promotedNames = null;
			while (queue.Count > 0)
			{
				List<int> dependencies = dependencyLists[queue.Dequeue()];
				if (dependencies == null)
				{
					continue;
				}

				for (int i = 0; i < dependencies.Count; i++)
				{
					int dependencyIndex = dependencies[i];
					if (!nodes[dependencyIndex].PromoteToCritical())
					{
						continue;
					}

					(promotedNames ??= new List<string>()).Add(nodes[dependencyIndex].SystemType.FullName);
					queue.Enqueue(dependencyIndex);
				}
			}

			if (promotedNames == null)
			{
				return;
			}

			Debug.LogWarning(
				$"Pulse: {promotedNames.Count} system(s) were auto-promoted to critical because a critical system " +
				$"depends on them: {string.Join(", ", promotedNames)}. " +
				"Mark them with SetAsCritical() explicitly.");
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

		private static InvalidOperationException CreateAmbiguousDependencyException(
			InitializationNode node,
			Type dependencyType,
			IEnumerable<Type> candidateTypes)
		{
			string candidateNames = string.Join(", ", candidateTypes.Select(candidateType => candidateType.FullName));
			return new InvalidOperationException(
				$"System '{node.SystemType.FullName}' has dependency '{dependencyType.FullName}', " +
				$"but it matches multiple registered systems: {candidateNames}. " +
				"Register a concrete dependency type or remove the ambiguity.");
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
				throw CreateAmbiguousDependencyException(
					node,
					dependencyType,
					candidates.Select(candidate => candidate.SystemType));
			}

			if (IsProvidedByParent(node, dependencyType))
			{
				return null;
			}

			throw new InvalidOperationException($"System '{node.SystemType.FullName}' has dependency '{dependencyType.FullName}' " +
				$"which was not added to '{nameof(InitializationContextBuilder)}'. " +
				"All dependencies must be registered via AddSystem before Build is called " +
				"or provided by a parent context set via SetParent.");
		}

		private bool IsProvidedByParent(InitializationNode node, Type dependencyType)
		{
			for (InitializationContext parent = _parent; parent != null; parent = parent.Parent)
			{
				Type[] systemTypes = parent.SystemTypes;
				if (Array.IndexOf(systemTypes, dependencyType) >= 0)
				{
					return true;
				}

				Type[] candidates = systemTypes
					.Where(systemType => dependencyType.IsAssignableFrom(systemType))
					.ToArray();
				if (candidates.Length == 1)
				{
					return true;
				}

				if (candidates.Length > 1)
				{
					throw CreateAmbiguousDependencyException(node, dependencyType, candidates);
				}
			}

			return false;
		}
	}
}
