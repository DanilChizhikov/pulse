using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor
{
	internal sealed class InitializationGraphView : GraphView
	{
		private const float ColumnWidth = 340f;
		private const float RowHeight = 180f;
		private const float DimmedOpacity = 0.35f;

		private const Capabilities EdgeLockedCapabilities =
			Capabilities.Selectable | Capabilities.Deletable | Capabilities.Copiable | Capabilities.Movable;

		private readonly List<EdgeEntry> _edges = new();
		private readonly List<InitializationSystemNode> _nodes = new();
		private readonly HashSet<InitializationSystemNode> _selectedNodes = new();
		private readonly HashSet<InitializationSystemNode> _relatedNodes = new();
		
		public bool IsShowingAllEdges
		{
			get => _isShowingAllEdges;
			set
			{
				_isShowingAllEdges = value;
				UpdateFocus();
			}
		}

		private bool _isShowingAllEdges;

		public InitializationGraphView()
		{
			SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
			this.AddManipulator(new ContentDragger());
			this.AddManipulator(new SelectionDragger());
			this.AddManipulator(new RectangleSelector());

			var gridBackground = new GridBackground();
			Insert(0, gridBackground);
			gridBackground.StretchToParentSize();

			graphViewChanged = GraphViewChangedHandler;
			deleteSelection = DeleteSelectionHandler;
		}
		
		private static void LockEdge(Edge edge)
		{
			edge.capabilities &= ~EdgeLockedCapabilities;
			edge.pickingMode = PickingMode.Ignore;
			edge.edgeControl.pickingMode = PickingMode.Ignore;
			edge.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
		}

		private static int GetStartRank(InitializationSystemRecord system)
		{
			return system.StartOrder >= 0 ? system.StartOrder : int.MaxValue;
		}

		private static string GetLevelTitle(
			IReadOnlyList<InitializationSystemRecord> systems,
			IGrouping<int, int> level)
		{
			bool isStarted = false;
			double minStartMilliseconds = double.MaxValue;
			double maxEndMilliseconds = double.MinValue;

			foreach (int index in level)
			{
				InitializationSystemRecord system = systems[index];
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

			return isStarted
				? $"Level {level.Key} · {InitializationTimeFormat.Format(maxEndMilliseconds - minStartMilliseconds)}"
				: $"Level {level.Key} · not started";
		}

		private static GraphViewChange GraphViewChangedHandler(GraphViewChange change)
		{
			change.elementsToRemove?.Clear();
			change.edgesToCreate?.Clear();
			return change;
		}

		private static void DeleteSelectionHandler(string operationName, AskUser askUser)
		{
		}
		
		public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
		{
			return new List<Port>();
		}

		public override void AddToSelection(ISelectable selectable)
		{
			base.AddToSelection(selectable);
			UpdateFocus();
		}

		public override void RemoveFromSelection(ISelectable selectable)
		{
			base.RemoveFromSelection(selectable);
			UpdateFocus();
		}

		public override void ClearSelection()
		{
			base.ClearSelection();
			UpdateFocus();
		}

		public void Show(InitializationGraphSnapshot snapshot)
		{
			_edges.Clear();
			_nodes.Clear();
			ClearSelection();

			foreach (GraphElement element in graphElements.ToList())
			{
				RemoveElement(element);
			}

			if (snapshot == null || snapshot.Systems.Count == 0)
			{
				return;
			}

			IReadOnlyList<InitializationSystemRecord> systems = snapshot.Systems;
			double maxDurationMilliseconds = systems.Max(system => system.DurationMilliseconds);
			var nodes = new InitializationSystemNode[systems.Count];
			int[] levels = InitializationGraphLevels.Calculate(snapshot);
			bool[][] redundantDependencies = InitializationGraphReduction.FindRedundantDependencies(systems, levels);

			IEnumerable<IGrouping<int, int>> groupedLevels = Enumerable.Range(0, systems.Count)
				.GroupBy(index => levels[index])
				.OrderBy(level => level.Key);

			foreach (IGrouping<int, int> level in groupedLevels)
			{
				var group = new Group { title = GetLevelTitle(systems, level) };
				group.capabilities &= ~Capabilities.Deletable;
				AddElement(group);

				IEnumerable<int> orderedIndices = level
					.OrderBy(index => GetStartRank(systems[index]))
					.ThenBy(index => systems[index].TypeName, StringComparer.Ordinal);

				int row = 0;
				foreach (int index in orderedIndices)
				{
					var node = new InitializationSystemNode(systems[index], levels[index], maxDurationMilliseconds);
					node.SetPosition(new Rect(level.Key * ColumnWidth, row * RowHeight, 0f, 0f));
					AddElement(node);
					group.AddElement(node);
					nodes[index] = node;
					_nodes.Add(node);
					row++;
				}
			}

			for (int i = 0; i < systems.Count; i++)
			{
				IReadOnlyList<int> dependencyIndices = systems[i].DependencyIndices;
				for (int j = 0; j < dependencyIndices.Count; j++)
				{
					int dependencyIndex = dependencyIndices[j];
					if (dependencyIndex < 0 || dependencyIndex >= nodes.Length)
					{
						continue;
					}

					Edge edge = nodes[dependencyIndex].Output.ConnectTo(nodes[i].Input);
					LockEdge(edge);
					AddElement(edge);
					_edges.Add(new EdgeEntry(edge, nodes[dependencyIndex], nodes[i], redundantDependencies[i][j]));
				}
			}

			UpdateFocus();
			schedule.Execute(() => FrameAll());
		}
		
		private void UpdateFocus()
		{
			_selectedNodes.Clear();
			foreach (ISelectable selectable in selection)
			{
				if (selectable is InitializationSystemNode node)
				{
					_selectedNodes.Add(node);
				}
			}

			if (_selectedNodes.Count == 0)
			{
				foreach (EdgeEntry entry in _edges)
				{
					entry.Edge.visible = _isShowingAllEdges || !entry.IsRedundant;
				}

				foreach (InitializationSystemNode node in _nodes)
				{
					node.style.opacity = StyleKeyword.Null;
				}

				return;
			}

			_relatedNodes.Clear();
			_relatedNodes.UnionWith(_selectedNodes);
			foreach (EdgeEntry entry in _edges)
			{
				bool isFocused = _selectedNodes.Contains(entry.From) || _selectedNodes.Contains(entry.To);
				entry.Edge.visible = isFocused;
				if (isFocused)
				{
					_relatedNodes.Add(entry.From);
					_relatedNodes.Add(entry.To);
				}
			}

			foreach (InitializationSystemNode node in _nodes)
			{
				node.style.opacity = _relatedNodes.Contains(node)
					? new StyleFloat(StyleKeyword.Null)
					: new StyleFloat(DimmedOpacity);
			}
		}

		private readonly struct EdgeEntry
		{
			public readonly Edge Edge;
			public readonly InitializationSystemNode From;
			public readonly InitializationSystemNode To;
			public readonly bool IsRedundant;

			public EdgeEntry(Edge edge, InitializationSystemNode from, InitializationSystemNode to, bool isRedundant)
			{
				Edge = edge;
				From = from;
				To = to;
				IsRedundant = isRedundant;
			}
		}
	}
}
