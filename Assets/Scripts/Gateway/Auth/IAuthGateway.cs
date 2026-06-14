using Cysharp.Threading.Tasks;

namespace Gateway.Auth
{

    public interface IAuthGateway
    {
        void Initialize();
        UniTask<string> GetUuid();
    }
}
