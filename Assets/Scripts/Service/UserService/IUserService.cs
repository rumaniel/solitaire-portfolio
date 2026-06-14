using Cysharp.Threading.Tasks;
using Model.User;
using R3;

namespace Service.UserService
{
    public interface IUserService
    {
        ReactiveProperty<User> User { get; }
        void Initialize();
        UniTask Login();
        UniTask Logout();
        void Update(User user);
    }
}
