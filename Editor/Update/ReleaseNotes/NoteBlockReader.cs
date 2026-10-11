using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DTech.Pulse.Editor.Update
{
	internal sealed class NoteBlockReader
	{
		private static readonly Regex _heading = new("^(#{1,6})\\s+(.*?)(?:\\s+#+)?\\s*$");
		private static readonly Regex _fence = new("^\\s{0,3}(```+|~~~+)\\s*([^`\\s]*)");
		private static readonly Regex _rule = new("^\\s{0,3}((\\*\\s*){3,}|(-\\s*){3,}|(_\\s*){3,})$");
		private static readonly Regex _listEntry = new("^(\\s*)([-*+]|\\d{1,9}[.)])\\s+(.*)$");
		private static readonly Regex _taskBox = new("^\\[([ xX])\\]\\s+(.*)$");
		private static readonly Regex _tableSeparator = new("^\\s*\\|?\\s*:?-{2,}:?\\s*(\\|\\s*:?-{2,}:?\\s*)*\\|?\\s*$");

		private readonly string[] _lines;
		private readonly List<NoteBlock> _blocks = new();

		private int _index;

		private NoteBlockReader(string[] lines)
		{
			_lines = lines;
		}

		public static List<NoteBlock> Read(string markdown)
		{
			if (string.IsNullOrEmpty(markdown))
			{
				return new List<NoteBlock>();
			}

			var reader = new NoteBlockReader(markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
			return reader.ReadAll();
		}

		private static string[] SplitRow(string line)
		{
			string trimmed = line.Trim();
			if (trimmed.StartsWith("|", StringComparison.Ordinal))
			{
				trimmed = trimmed.Substring(1);
			}

			if (trimmed.EndsWith("|", StringComparison.Ordinal) && !trimmed.EndsWith("\\|", StringComparison.Ordinal))
			{
				trimmed = trimmed.Substring(0, trimmed.Length - 1);
			}

			var cells = new List<string>();
			var cell = new StringBuilder();
			for (int i = 0; i < trimmed.Length; i++)
			{
				char character = trimmed[i];
				if (character == '\\' && i + 1 < trimmed.Length && trimmed[i + 1] == '|')
				{
					cell.Append('|');
					i++;
					continue;
				}

				if (character == '|')
				{
					cells.Add(cell.ToString().Trim());
					cell.Clear();
					continue;
				}

				cell.Append(character);
			}

			cells.Add(cell.ToString().Trim());
			return cells.ToArray();
		}

		private static bool IsQuote(string line)
		{
			return line.TrimStart().StartsWith(">", StringComparison.Ordinal);
		}

		private static bool StartsBlock(string line)
		{
			return _fence.IsMatch(line) || _heading.IsMatch(line) || _listEntry.IsMatch(line) || IsQuote(line) || _rule.IsMatch(line);
		}

		private static NoteListEntry CreateEntry(Match match)
		{
			var entry = new NoteListEntry
			{
				Depth = match.Groups[1].Value.Replace("\t", "    ").Length / 2,
				Marker = match.Groups[2].Value,
				Text = match.Groups[3].Value,
			};

			Match task = _taskBox.Match(entry.Text);
			if (task.Success)
			{
				entry.Checked = task.Groups[1].Value != " ";
				entry.Text = task.Groups[2].Value;
			}

			return entry;
		}

		private List<NoteBlock> ReadAll()
		{
			while (_index < _lines.Length)
			{
				string line = _lines[_index];
				if (line.Trim().Length == 0)
				{
					_index++;
					continue;
				}

				Match fence = _fence.Match(line);
				if (fence.Success)
				{
					ReadCode(fence);
					continue;
				}

				Match heading = _heading.Match(line);
				if (heading.Success)
				{
					_blocks.Add(new NoteBlock
					{
						Kind = NoteBlockKind.Heading,
						Level = heading.Groups[1].Value.Length,
						Text = heading.Groups[2].Value,
					});
					_index++;
					continue;
				}

				if (_rule.IsMatch(line))
				{
					_blocks.Add(new NoteBlock { Kind = NoteBlockKind.Rule });
					_index++;
					continue;
				}

				if (IsQuote(line))
				{
					ReadQuote();
					continue;
				}

				if (_listEntry.IsMatch(line))
				{
					ReadList();
					continue;
				}

				if (IsTableStart(_index))
				{
					ReadTable();
					continue;
				}

				ReadParagraph();
			}

			return _blocks;
		}

		private bool IsTableStart(int index)
		{
			return _lines[index].Contains("|") && index + 1 < _lines.Length && _tableSeparator.IsMatch(_lines[index + 1]);
		}

		private void ReadCode(Match fence)
		{
			string marker = fence.Groups[1].Value;
			var code = new StringBuilder();
			_index++;
			while (_index < _lines.Length)
			{
				string line = _lines[_index];
				_index++;
				string trimmed = line.Trim();
				if (trimmed.StartsWith(marker, StringComparison.Ordinal) && trimmed.Trim(marker[0]).Length == 0)
				{
					break;
				}

				if (code.Length > 0)
				{
					code.Append('\n');
				}

				code.Append(line);
			}

			_blocks.Add(new NoteBlock
			{
				Kind = NoteBlockKind.Code,
				Language = fence.Groups[2].Value,
				Text = code.ToString(),
			});
		}

		private void ReadQuote()
		{
			var text = new StringBuilder();
			while (_index < _lines.Length && IsQuote(_lines[_index]))
			{
				if (text.Length > 0)
				{
					text.Append('\n');
				}

				text.Append(_lines[_index].TrimStart().Substring(1).TrimStart());
				_index++;
			}

			_blocks.Add(new NoteBlock { Kind = NoteBlockKind.Quote, Text = text.ToString() });
		}

		private void ReadList()
		{
			var block = new NoteBlock { Kind = NoteBlockKind.List };
			while (_index < _lines.Length)
			{
				string line = _lines[_index];
				Match match = _listEntry.Match(line);
				if (match.Success)
				{
					block.Entries.Add(CreateEntry(match));
					_index++;
					continue;
				}

				if (line.Trim().Length == 0)
				{
					if (_index + 1 < _lines.Length && _listEntry.IsMatch(_lines[_index + 1]))
					{
						_index++;
						continue;
					}

					break;
				}

				bool isContinuation = line.StartsWith("  ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal);
				if (isContinuation && block.Entries.Count > 0)
				{
					NoteListEntry last = block.Entries[block.Entries.Count - 1];
					last.Text += "\n" + line.Trim();
					_index++;
					continue;
				}

				break;
			}

			_blocks.Add(block);
		}

		private void ReadTable()
		{
			var block = new NoteBlock { Kind = NoteBlockKind.Table };
			block.Rows.Add(SplitRow(_lines[_index]));
			_index += 2;
			while (_index < _lines.Length && _lines[_index].Contains("|") && _lines[_index].Trim().Length > 0)
			{
				block.Rows.Add(SplitRow(_lines[_index]));
				_index++;
			}

			_blocks.Add(block);
		}

		private void ReadParagraph()
		{
			int start = _index;
			var text = new StringBuilder();
			while (_index < _lines.Length)
			{
				string line = _lines[_index];
				if (line.Trim().Length == 0)
				{
					break;
				}

				if (_index > start && (StartsBlock(line) || IsTableStart(_index)))
				{
					break;
				}

				if (text.Length > 0)
				{
					text.Append('\n');
				}

				text.Append(line.TrimEnd());
				_index++;
			}

			_blocks.Add(new NoteBlock { Kind = NoteBlockKind.Paragraph, Text = text.ToString() });
		}
	}
}
