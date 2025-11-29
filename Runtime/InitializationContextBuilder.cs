using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
				Type[] dependencies = GetDependencies(system);
				node.AddDependencies(dependencies);
			}
			
			return node;
		}
		
		public InitializationContext Build()
		{
			List<ICollection<InitializationNode>> batches = BuildBatches();
			return new InitializationContext(batches, _criticalSystems);
		}

		private static Type[] GetDependencies(object system)
		{
			var result = new HashSet<Type>();
			MemberInfo[] members = system.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
			foreach (MemberInfo member in members)
			{
				var attribute = member.GetCustomAttribute<InitDependencyAttribute>();
				if (attribute != null)
				{
					switch (member)
					{
						case FieldInfo fieldInfo:
						{
							result.Add(fieldInfo.FieldType);
						} break;

						case MethodInfo methodInfo:
						{
							ParameterInfo[] parameters = methodInfo.GetParameters();
							foreach (ParameterInfo parameter in parameters)
							{
								result.Add(parameter.ParameterType);
							}
						} break;

						case PropertyInfo propertyInfo:
						{
							result.Add(propertyInfo.PropertyType);
						} break;
					}
				}
			}

			ConstructorInfo[] constructors = system.GetType().GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			ConstructorInfo constructorInfo = null;
			if (constructors.Length > 1)
			{
				constructorInfo = GetConstructor(constructors);
			}
			else if (constructors.Length == 1)
			{
				constructorInfo = constructors[0];
			}

			if (constructorInfo != null)
			{
				result.UnionWith(GetDependencies(constructorInfo));
			}
			
			result.RemoveWhere(type => !typeof(IInitializable).IsAssignableFrom(type));
			return result.ToArray();
		}

		private static Type[] GetDependencies(ConstructorInfo constructor)
		{
			ParameterInfo[] parameterInfos = constructor.GetParameters();
			return parameterInfos.Select(parameterInfo => parameterInfo.ParameterType).ToArray();
		}

		private static ConstructorInfo GetConstructor(IReadOnlyList<ConstructorInfo> constructors)
		{
			ConstructorInfo publicConstructor = null;
			bool hasPublicConstructor = false;
			for (int i = 0; i < constructors.Count; i++)
			{
				ConstructorInfo constructorInfo = constructors[i];
				var attribute = constructorInfo.GetCustomAttribute<InitDependencyAttribute>();
				if (attribute != null)
				{
					return constructorInfo;
				}

				if (constructorInfo.IsPublic && !hasPublicConstructor)
				{
					publicConstructor = constructorInfo;
					hasPublicConstructor = true;
				}
			}

			return publicConstructor;
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
				foreach (var depType in node.GetDependencies())
				{
					var depNode = _nodes.FirstOrDefault(n => depType.IsAssignableFrom(n.SystemType));
					if (depNode != null)
					{
						adjacency[depNode].Add(node);
						inDegree[node]++;
					}
				}

				if (node.IsCritical)
					_criticalSystems.Add(node);
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