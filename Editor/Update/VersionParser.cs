using System;

namespace DTech.Pulse.Editor.Update
{
	internal abstract class VersionParser
	{
		public abstract bool TryGetLastVersion(out Version version, out string error);
	}
}