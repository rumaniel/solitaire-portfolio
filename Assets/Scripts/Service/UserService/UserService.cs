using Cysharp.Threading.Tasks;
using Gateway.Auth;
using Model.User;
using R3;
using VContainer;

namespace Service.UserService
{
    public class UserService : IUserService
    {
        public ReactiveProperty<User> User { get; } = new(new User());

        [Inject] private IAuthGateway AuthGateway { get; set; }

        public void Initialize()
        {
            AuthGateway.Initialize();
        }

        public async UniTask Login()
        {
            // Initialize user with auth gateway data
            var userId = await AuthGateway.GetUuid();
            if (!string.IsNullOrEmpty(userId))
            {
                var newUser = new User { UserId = userId };
                User.Value = newUser;
            }
        }

        public UniTask Logout()
        {
            // Clear user data
            User.Value = new User();

            return UniTask.CompletedTask;
        }

        public void Update(User user)
        {
            User.Value = user;
        }
    }
}
