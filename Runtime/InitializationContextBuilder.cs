using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.Pulse
{
	public sealed class InitializationContextBuilder
	{
		private readonly List<InitializationNode> _nodes = new();
		private readonly Dictionary<Type, InitializationNode> _nodesByType = new();

		private bool _isBuilt;

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

		public InitializationContext Build()
		{
			if (_isBuilt)
			{
				throw new InvalidOperationException("This builder has already built an initialization context.");
			}

			List<ICollection<InitializationNode>> batches = BuildBatches(out HashSet<InitializationNode> criticalSystems);
			for (int i = 0; i < _nodes.Count; i++)
			{
				_nodes[i].SetProcessed();
			}

			_isBuilt = true;
			return new InitializationContext(batches, criticalSystems, _nodes);
		}

		private List<ICollection<InitializationNode>> BuildBatches(out HashSet<InitializationNode> criticalSystems)
		{
			var batches = new List<ICollection<InitializationNode>>();
			criticalSystems = new HashSet<InitializationNode>();

			var inDegree = new Dictionary<InitializationNode, int>();
			var adjacency = new Dictionary<InitializationNode, List<InitializationNode>>();

			foreach (var node in _nodes)
			{
				inDegree[node] = 0;
				adjacency[node] = new List<InitializationNode>();
			}

			foreach (var node in _nodes)
			{
				List<Type> dependencies = node.GetDependencies();
				foreach (var depType in dependencies)
				{
					var depNode = _nodes.FirstOrDefault(n => depType.IsAssignableFrom(n.SystemType));
					if (depNode == null)
					{
						throw new Exception($"System '{node.SystemType.FullName}' has dependency '{depType.FullName}' " +
							$"which was not added to '{nameof(InitializationContextBuilder)}'. " +
							"All dependencies must be registered via AddSystem before Build is called.");
					}
					else
					{
						adjacency[depNode].Add(node);
						inDegree[node]++;
					}
				}

				if (node.IsCritical)
				{
					criticalSystems.Add(node);
				}
			}

			var queue = new Queue<InitializationNode>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

			while (queue.Count > 0)
			{
				var batch = new List<InitializationNode>();
				int batchCount = queue.Count;

				for (int i = 0; i < batchCount; i++)
				{
					var node = queue.Dequeue();
					batch.Add(node);
					List<InitializationNode> dependents = adjacency[node];
					foreach (InitializationNode dependent in dependents)
					{
						inDegree[dependent]--;
						if (inDegree[dependent] == 0)
						{
							queue.Enqueue(dependent);
						}
					}
				}

				batches.Add(batch);
			}

			if (inDegree.Any(kv => kv.Value > 0))
			{
				string cycle = string.Join(", ", inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key.SystemType.Name));
				throw new Exception("Cyclic dependencies detected: " + cycle);
			}

			return batches;
		}
	}
}
