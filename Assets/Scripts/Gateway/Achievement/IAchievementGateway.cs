using Cysharp.Threading.Tasks;
using Model.Achievement;

namespace Gateway.Achievement
{
    public interface IAchievementGateway
    {
        UniTask<AchievementStore> LoadAsync();
        UniTask SaveAsync(AchievementStore store);
    }
}
