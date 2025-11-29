using System;
using System.Collections.Generic;
using System.Linq;

namespace DTech.Pulse
{
	public sealed class InitializationContextBuilder
	{
		private readonly HashSet<InitializationNode> _nodes = new();
		private readonly HashSet<InitializationNode> _criticalSystems = new();
		
		public IInitializationNodeHandle AddSystem(IInitializable system)
		{
			var node = new InitializationNode(system);
			if (_nodes.Add(node))
			{
				Type[] dependencies = system.GetDependencies();
				node.AddDependencies(dependencies);
			}
			
			return node;
		}
		
		public InitializationContext Build()
		{
			List<ICollection<InitializationNode>> batches = BuildBatches();
			return new InitializationContext(batches, _criticalSystems, _nodes);
		}
		
		private List<ICollection<InitializationNode>> BuildBatches()
		{
			var batches = new List<ICollection<InitializationNode>>();
			_criticalSystems.Clear();
			
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
					_criticalSystems.Add(node);
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