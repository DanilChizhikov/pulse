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
			if (_nodes.Any(cachedNode => cachedNode.SystemType == node.SystemType))
			{
				throw new Exception($"System {node.SystemType.FullName} has already been added");
			}
			
			_nodes.Add(node);
			return node;
		}
		
		public InitializationContext Build()
		{
			List<ICollection<InitializationNode>> batches = BuildBatches();
			return new InitializationContext(batches, _criticalSystems);
		}
		
		private List<ICollection<InitializationNode>> BuildBatches()
		{
			var batches = new List<ICollection<InitializationNode>>();
			_criticalSystems.Clear();

			var remaining = new HashSet<InitializationNode>(_nodes);
			var dependenciesCount = new Dictionary<InitializationNode, int>();
            
			foreach (InitializationNode node in _nodes)
			{
				dependenciesCount.Add(node, node.Dependencies.Count);
				if (node.IsCritical)
				{
					_criticalSystems.Add(node);
				}
			}

			while (remaining.Any())
			{
				var batch = new HashSet<InitializationNode>();
				foreach (InitializationNode node in remaining)
				{
					if (dependenciesCount[node] != 0)
					{
						continue;
					}
                    
					batch.Add(node);
				}

				if (!batch.Any())
				{
					string cycle = string.Join(", ", remaining.Select(node => node.SystemType.Name));
					throw new Exception("Cyclic or missing dependencies detected: " + cycle);
				}

				batches.Add(batch);
				foreach (InitializationNode node in batch)
				{
					remaining.Remove(node);
					foreach (InitializationNode otherNode in remaining)
					{
						foreach (Type dependency in otherNode.Dependencies)
						{
							if (dependency.IsAssignableFrom(node.SystemType))
							{
								dependenciesCount[otherNode]--;
								break;
							}
						}
					}
				}
			}
			
			return batches;
		}
	}
}