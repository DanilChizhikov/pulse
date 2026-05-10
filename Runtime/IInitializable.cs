using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace DTech.Pulse
{
	[Preserve]
	public interface IInitializable
	{
		Task InitializeAsync(CancellationToken token);
	}
}
