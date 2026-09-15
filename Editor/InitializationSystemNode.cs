using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor
{
	internal sealed class InitializationSystemNode : Node
	{
		private const float MinWidth = 240f;
		private const float StatusBorderWidth = 2f;

		private static readonly Color _fastColor = new(0.18f, 0.49f, 0.2f);
		private static readonly Color _slowColor = new(0.72f, 0.18f, 0.16f);
		private static readonly Color _notStartedColor = new(0.3f, 0.3f, 0.3f);
		private static readonly Color _failedColor = new(0.95f, 0.2f, 0.2f);
		private static readonly Color _cancelledColor = new(0.95f, 0.65f, 0.1f);
		private static readonly Color _criticalColor = new(0.95f, 0.75f, 0.2f);

		public Port Input { get; }
		public Port Output { get; }

		public InitializationSystemNode(InitializationSystemRecord record, double maxDurationMilliseconds)
		{
			title = record.TypeName;
			tooltip = string.IsNullOrEmpty(record.Error) ? record.FullTypeName : $"{record.FullTypeName}\n{record.Error}";
			capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable | Capabilities.Renamable);
			style.minWidth = MinWidth;

			Input = CreatePort(Direction.Input, "Depends on");
			inputContainer.Add(Input);
			Output = CreatePort(Direction.Output, "Required by");
			outputContainer.Add(Output);

			if (record.IsCritical)
			{
				titleButtonContainer.Insert(0, CreateCriticalBadge());
			}

			extensionContainer.Add(CreateDetails(record));
			titleContainer.style.backgroundColor = GetTitleColor(record, maxDurationMilliseconds);
			ApplyStatusBorder(record.Status);

			RefreshExpandedState();
			RefreshPorts();
		}

		private Port CreatePort(Direction direction, string portName)
		{
			Port port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(IInitializable));
			port.portName = portName;
			if (port.edgeConnector != null)
			{
				port.RemoveManipulator(port.edgeConnector);
			}

			return port;
		}

		private void ApplyStatusBorder(InitializationSystemStatus status)
		{
			Color color;
			switch (status)
			{
				case InitializationSystemStatus.Failed:
				{
					color = _failedColor;
				} break;

				case InitializationSystemStatus.Cancelled:
				{
					color = _cancelledColor;
				} break;

				default:
					return;
			}

			IStyle borderStyle = mainContainer.style;
			borderStyle.borderLeftColor = color;
			borderStyle.borderRightColor = color;
			borderStyle.borderTopColor = color;
			borderStyle.borderBottomColor = color;
			borderStyle.borderLeftWidth = StatusBorderWidth;
			borderStyle.borderRightWidth = StatusBorderWidth;
			borderStyle.borderTopWidth = StatusBorderWidth;
			borderStyle.borderBottomWidth = StatusBorderWidth;
		}

		private static VisualElement CreateDetails(InitializationSystemRecord record)
		{
			var details = new VisualElement();
			details.style.paddingLeft = 8f;
			details.style.paddingRight = 8f;
			details.style.paddingTop = 4f;
			details.style.paddingBottom = 6f;

			string order = record.StartOrder >= 0 ? $"#{record.StartOrder + 1}" : "Not started";
			details.Add(new Label($"{order} · Batch {record.BatchIndex}"));
			details.Add(new Label($"Start: +{record.StartMilliseconds:0.##} ms"));
			details.Add(new Label($"Duration: {record.DurationMilliseconds:0.##} ms"));
			details.Add(new Label($"Status: {record.Status}"));
			return details;
		}

		private static Label CreateCriticalBadge()
		{
			var badge = new Label("CRITICAL");
			badge.style.color = _criticalColor;
			badge.style.unityFontStyleAndWeight = FontStyle.Bold;
			badge.style.unityTextAlign = TextAnchor.MiddleCenter;
			badge.style.marginRight = 4f;
			return badge;
		}

		private static Color GetTitleColor(InitializationSystemRecord record, double maxDurationMilliseconds)
		{
			if (record.Status == InitializationSystemStatus.NotStarted)
			{
				return _notStartedColor;
			}

			float ratio = maxDurationMilliseconds > 0d
				? (float)(record.DurationMilliseconds / maxDurationMilliseconds)
				: 0f;
			return Color.Lerp(_fastColor, _slowColor, ratio);
		}
	}
}
