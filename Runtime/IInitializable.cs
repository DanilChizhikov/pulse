using System.Threading;
using System.Threading.Tasks;

namespace DTech.Pulse
{
	public interface IInitializable
	{
		Task InitializeAsync(CancellationToken token);
	}
}