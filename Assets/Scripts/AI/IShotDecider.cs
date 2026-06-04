using System.Threading;
using System.Threading.Tasks;
using Curling.Core;

namespace Curling.AI
{
    public interface IShotDecider
    {
        Task<ShotInput> DecideAsync(MatchState state, Team self, CancellationToken ct);
    }
}
