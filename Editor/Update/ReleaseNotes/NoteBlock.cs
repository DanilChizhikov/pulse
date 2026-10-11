using System.Collections.Generic;

namespace DTech.Pulse.Editor.Update
{
	internal sealed class NoteBlock
	{
		public List<NoteListEntry> Entries { get; } = new();
		public List<string[]> Rows { get; } = new();
		public NoteBlockKind Kind { get; set; }
		public int Level { get; set; }
		public string Text { get; set; } = string.Empty;
		public string Language { get; set; }
	}
}
