using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace DTech.Pulse.Editor.Update
{
	internal static class NoteStyles
	{
		private const string MonospaceFontPath = "Fonts/RobotoMono/RobotoMono-Regular.ttf";
		private const int MonospaceFallbackSize = 12;
		private const float FrameRadius = 4f;

		private static readonly string[] _monospaceFallbacks = { "Menlo", "Consolas", "Courier New" };

		private static Font _monospace;

		public static string CodeColor => IsDark ? "#E6C07B" : "#A0522D";
		public static string LinkColor => IsDark ? "#6CB6FF" : "#0969DA";
		public static string MarkColor => IsDark ? "#FFFFFF14" : "#0000000F";

		public static Color Text => IsDark ? new Color32(0xDC, 0xDC, 0xDC, 0xFF) : new Color32(0x1F, 0x1F, 0x1F, 0xFF);
		public static Color Muted => IsDark ? new Color32(0x9B, 0x9B, 0x9B, 0xFF) : new Color32(0x66, 0x66, 0x66, 0xFF);
		public static Color Border => IsDark ? new Color32(0x47, 0x47, 0x47, 0xFF) : new Color32(0xC4, 0xC4, 0xC4, 0xFF);
		public static Color Surface => IsDark ? new Color32(0x2A, 0x2A, 0x2A, 0xFF) : new Color32(0xF4, 0xF4, 0xF4, 0xFF);
		public static Color SurfaceRaised => IsDark ? new Color32(0x31, 0x31, 0x31, 0xFF) : new Color32(0xFF, 0xFF, 0xFF, 0xFF);
		public static Color ButtonBackground => IsDark ? new Color32(0x3A, 0x3A, 0x3A, 0xFF) : new Color32(0xE2, 0xE2, 0xE2, 0xFF);
		public static Color CodeBackground => IsDark ? new Color32(0x1B, 0x1B, 0x1B, 0xFF) : new Color32(0xF6, 0xF8, 0xFA, 0xFF);
		public static Color Accent => IsDark ? new Color32(0x3D, 0xDC, 0x84, 0xFF) : new Color32(0x1A, 0x7F, 0x37, 0xFF);

		private static bool IsDark => EditorGUIUtility.isProSkin;

		private static Font Monospace
		{
			get
			{
				if (_monospace == null)
				{
					_monospace = EditorGUIUtility.Load(MonospaceFontPath) as Font;
					if (_monospace == null)
					{
						_monospace = Font.CreateDynamicFontFromOSFont(_monospaceFallbacks, MonospaceFallbackSize);
					}
				}

				return _monospace;
			}
		}

		public static void ApplyFrame(VisualElement element)
		{
			SetBorder(element.style, 1f, Border);
			SetRadius(element.style, FrameRadius);
			element.style.backgroundColor = Surface;
		}

		public static void SetBorder(IStyle style, float width, Color color)
		{
			style.borderTopWidth = width;
			style.borderRightWidth = width;
			style.borderBottomWidth = width;
			style.borderLeftWidth = width;
			style.borderTopColor = color;
			style.borderRightColor = color;
			style.borderBottomColor = color;
			style.borderLeftColor = color;
		}

		public static void SetRadius(IStyle style, float radius)
		{
			style.borderTopLeftRadius = radius;
			style.borderTopRightRadius = radius;
			style.borderBottomLeftRadius = radius;
			style.borderBottomRightRadius = radius;
		}

		public static Label RichLabel(string text)
		{
			var label = new Label(text) { enableRichText = true };
			label.selection.isSelectable = true;
			label.style.whiteSpace = WhiteSpace.Normal;
			label.style.color = Text;
			label.RegisterCallback<PointerUpLinkTagEvent>(LinkClickHandler);
			return label;
		}

		public static Label PlainLabel(string text, Color color)
		{
			var label = new Label(text) { enableRichText = false };
			label.style.color = color;
			return label;
		}

		public static void UseMonospace(VisualElement element)
		{
			element.style.unityFontDefinition = new StyleFontDefinition(Monospace);
		}

		private static void LinkClickHandler(PointerUpLinkTagEvent evt)
		{
			string link = evt.linkID;
			if (string.IsNullOrEmpty(link))
			{
				return;
			}

			if (link.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
				link.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
			{
				Application.OpenURL(link);
			}
		}
	}
}
