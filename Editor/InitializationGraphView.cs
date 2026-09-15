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
		}

		public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
		{
			return new List<Port>();
		}

		public void Show(InitializationGraphSnapshot snapshot)
		{
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
					row++;
				}
			}

			for (int i = 0; i < systems.Count; i++)
			{
				foreach (int dependencyIndex in systems[i].DependencyIndices)
				{
					if (dependencyIndex < 0 || dependencyIndex >= nodes.Length)
					{
						continue;
					}

					Edge edge = nodes[dependencyIndex].Output.ConnectTo(nodes[i].Input);
					edge.capabilities &= ~Capabilities.Deletable;
					AddElement(edge);
				}
			}

			schedule.Execute(() => FrameAll());
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
				? $"Level {level.Key} · {maxEndMilliseconds - minStartMilliseconds:0.##} ms"
				: $"Level {level.Key} · not started";
		}

		private static GraphViewChange GraphViewChangedHandler(GraphViewChange change)
		{
			change.elementsToRemove?.Clear();
			change.edgesToCreate?.Clear();
			return change;
		}
	}
}
