using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor.Update
{
	internal sealed class PulseIcon : VisualElement
	{
		private const float LineWidth = 4f;
		private const float CapTop = 0.2f;
		private const float CapBottom = 0.8f;

		private static readonly Color _strokeColor = new Color32(0x3D, 0xDC, 0x84, 0xFF);

		private static readonly Vector2[] _points =
		{
			new(0f, 0.5f),
			new(0.22f, 0.5f),
			new(0.30f, 0.35f),
			new(0.38f, 0.5f),
			new(0.46f, 0.5f),
			new(0.54f, 0f),
			new(0.64f, 1f),
			new(0.72f, 0.5f),
			new(1f, 0.5f),
		};

		public PulseIcon()
		{
			generateVisualContent += OnGenerateVisualContent;
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			Rect rect = contentRect;
			float inset = LineWidth * 0.5f;
			float width = rect.width - LineWidth;
			float top = rect.y + Mathf.Max(rect.height * CapTop, inset);
			float bottom = rect.y + Mathf.Min(rect.height * CapBottom, rect.height - inset);
			float height = bottom - top;
			if (width <= 0f || height <= 0f)
			{
				return;
			}

			Painter2D painter = context.painter2D;
			painter.strokeColor = _strokeColor;
			painter.lineWidth = LineWidth;
			painter.lineJoin = LineJoin.Round;
			painter.lineCap = LineCap.Round;

			painter.BeginPath();
			for (int i = 0; i < _points.Length; i++)
			{
				var point = new Vector2(
					rect.x + inset + _points[i].x * width,
					top + _points[i].y * height);

				if (i == 0)
				{
					painter.MoveTo(point);
				}
				else
				{
					painter.LineTo(point);
				}
			}

			painter.Stroke();
		}
	}
}
