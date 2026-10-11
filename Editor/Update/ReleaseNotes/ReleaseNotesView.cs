using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DTech.Pulse.Editor.Update
{
	internal sealed class ReleaseNotesView : VisualElement
	{
		private const long CopiedResetMs = 1200;
		private const float ListIndentWidth = 14f;
		private const float CodeRadius = 6f;
		private const float CodeMaxHeight = 320f;
		private const float CopyRadius = 4f;
		private const float QuoteBorderWidth = 3f;
		private const float BulletWidth = 16f;
		private const float CellMinWidth = 80f;

		public ReleaseNotesView()
		{
			style.marginTop = 8;
			style.marginBottom = 8;
			style.marginLeft = 10;
			style.marginRight = 10;
		}

		public void SetText(string markdown)
		{
			Clear();
			List<NoteBlock> blocks = NoteBlockReader.Read(markdown);
			foreach (NoteBlock block in blocks)
			{
				Add(Build(block));
			}
		}

		private static VisualElement Build(NoteBlock block)
		{
			switch (block.Kind)
			{
				case NoteBlockKind.Heading:
					return BuildHeading(block);
				case NoteBlockKind.Code:
					return BuildCode(block.Text, block.Language);
				case NoteBlockKind.Quote:
					return BuildQuote(block);
				case NoteBlockKind.List:
					return BuildList(block);
				case NoteBlockKind.Table:
					return BuildTable(block);
				case NoteBlockKind.Rule:
					return BuildRule();
				default:
					return BuildParagraph(block.Text);
			}
		}

		private static Label BuildParagraph(string text)
		{
			Label label = NoteStyles.RichLabel(NoteRichText.Convert(text));
			label.style.marginTop = 2;
			label.style.marginBottom = 4;
			label.style.marginLeft = 0;
			label.style.marginRight = 0;
			return label;
		}

		private static Label BuildHeading(NoteBlock block)
		{
			Label label = NoteStyles.RichLabel(NoteRichText.Convert(block.Text));
			label.style.unityFontStyleAndWeight = FontStyle.Bold;
			label.style.marginLeft = 0;
			label.style.marginRight = 0;
			switch (block.Level)
			{
				case 1:
					label.style.fontSize = 17;
					label.style.marginTop = 10;
					label.style.marginBottom = 4;
					break;
				case 2:
					label.style.fontSize = 15;
					label.style.marginTop = 10;
					label.style.marginBottom = 4;
					label.style.paddingBottom = 3;
					label.style.borderBottomWidth = 1;
					label.style.borderBottomColor = NoteStyles.Border;
					break;
				default:
					label.style.fontSize = 13;
					label.style.marginTop = 8;
					label.style.marginBottom = 2;
					break;
			}

			return label;
		}

		private static VisualElement BuildCode(string code, string language)
		{
			var container = new VisualElement
			{
				style =
				{
					marginTop = 4,
					marginBottom = 4,
					backgroundColor = NoteStyles.CodeBackground,
				}
			};

			NoteStyles.SetBorder(container.style, 1f, NoteStyles.Border);
			NoteStyles.SetRadius(container.style, CodeRadius);

			var header = new VisualElement
			{
				style =
				{
					flexDirection = FlexDirection.Row,
					alignItems = Align.Center,
					paddingTop = 2,
					paddingBottom = 2,
					paddingLeft = 8,
					paddingRight = 4,
					borderBottomWidth = 1,
					borderBottomColor = NoteStyles.Border,
				}
			};

			Label languageLabel = NoteStyles.PlainLabel(string.IsNullOrEmpty(language) ? "code" : language, NoteStyles.Muted);
			languageLabel.style.fontSize = 10;
			header.Add(languageLabel);
			header.Add(new VisualElement { style = { flexGrow = 1f } });
			header.Add(BuildCopyButton(code));
			container.Add(header);

			var scroll = new ScrollView(ScrollViewMode.Horizontal)
			{
				style = { maxHeight = CodeMaxHeight }
			};

			var label = new Label(NoteRichText.Escape(code))
			{
				enableRichText = true,
				style =
				{
					whiteSpace = WhiteSpace.NoWrap,
					fontSize = 11,
					color = NoteStyles.Text,
					paddingTop = 6,
					paddingBottom = 6,
					paddingLeft = 8,
					paddingRight = 8,
				}
			};

			label.selection.isSelectable = true;
			NoteStyles.UseMonospace(label);
			scroll.Add(label);
			container.Add(scroll);
			return container;
		}

		private static Button BuildCopyButton(string code)
		{
			var copy = new Button
			{
				text = "Copy",
				style =
				{
					fontSize = 10,
					marginTop = 0,
					marginBottom = 0,
					marginLeft = 0,
					marginRight = 0,
					paddingTop = 1,
					paddingBottom = 1,
					paddingLeft = 8,
					paddingRight = 8,
					backgroundColor = NoteStyles.ButtonBackground,
					color = NoteStyles.Text,
				}
			};

			NoteStyles.SetBorder(copy.style, 1f, NoteStyles.Border);
			NoteStyles.SetRadius(copy.style, CopyRadius);
			copy.clicked += () =>
			{
				EditorGUIUtility.systemCopyBuffer = code;
				copy.text = "Copied";
				copy.schedule.Execute(() => copy.text = "Copy").ExecuteLater(CopiedResetMs);
			};

			return copy;
		}

		private static VisualElement BuildQuote(NoteBlock block)
		{
			var quote = new VisualElement
			{
				style =
				{
					marginTop = 2,
					marginBottom = 2,
					paddingLeft = 8,
					borderLeftWidth = QuoteBorderWidth,
					borderLeftColor = NoteStyles.Accent,
				}
			};

			Label label = BuildParagraph(block.Text);
			label.style.color = NoteStyles.Muted;
			quote.Add(label);
			return quote;
		}

		private static VisualElement BuildList(NoteBlock block)
		{
			var list = new VisualElement
			{
				style =
				{
					marginTop = 2,
					marginBottom = 4,
				}
			};

			foreach (NoteListEntry entry in block.Entries)
			{
				var row = new VisualElement
				{
					style =
					{
						flexDirection = FlexDirection.Row,
						alignItems = Align.FlexStart,
						marginBottom = 2,
						marginLeft = entry.Depth * ListIndentWidth,
					}
				};

				Label bullet = NoteStyles.PlainLabel(Bullet(entry), NoteStyles.Accent);
				bullet.style.minWidth = BulletWidth;
				row.Add(bullet);

				Label text = NoteStyles.RichLabel(NoteRichText.Convert(entry.Text));
				text.style.flexGrow = 1;
				text.style.flexShrink = 1;
				row.Add(text);

				list.Add(row);
			}

			return list;
		}

		private static string Bullet(NoteListEntry entry)
		{
			if (entry.Checked.HasValue)
			{
				return entry.Checked.Value ? "☑" : "☐";
			}

			if (char.IsDigit(entry.Marker[0]))
			{
				return entry.Marker;
			}

			return entry.Depth % 2 == 0 ? "•" : "◦";
		}

		private static VisualElement BuildTable(NoteBlock block)
		{
			var scroll = new ScrollView(ScrollViewMode.Horizontal)
			{
				style =
				{
					marginTop = 4,
					marginBottom = 4,
				}
			};

			int columns = 0;
			foreach (string[] row in block.Rows)
			{
				columns = Mathf.Max(columns, row.Length);
			}

			for (int rowIndex = 0; rowIndex < block.Rows.Count; rowIndex++)
			{
				string[] cells = block.Rows[rowIndex];
				var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
				if (rowIndex == 0)
				{
					row.style.unityFontStyleAndWeight = FontStyle.Bold;
					row.style.backgroundColor = NoteStyles.SurfaceRaised;
				}

				for (int column = 0; column < columns; column++)
				{
					string cell = column < cells.Length ? cells[column] : string.Empty;
					row.Add(BuildCell(cell));
				}

				scroll.Add(row);
			}

			return scroll;
		}

		private static Label BuildCell(string text)
		{
			Label label = NoteStyles.RichLabel(NoteRichText.Convert(text));
			label.style.minWidth = CellMinWidth;
			label.style.flexGrow = 1;
			label.style.flexBasis = 0;
			label.style.paddingTop = 2;
			label.style.paddingBottom = 2;
			label.style.paddingLeft = 6;
			label.style.paddingRight = 6;
			label.style.borderRightWidth = 1;
			label.style.borderBottomWidth = 1;
			label.style.borderRightColor = NoteStyles.Border;
			label.style.borderBottomColor = NoteStyles.Border;
			return label;
		}

		private static VisualElement BuildRule()
		{
			return new VisualElement
			{
				style =
				{
					height = 1,
					marginTop = 6,
					marginBottom = 6,
					backgroundColor = NoteStyles.Border,
				}
			};
		}
	}
}
