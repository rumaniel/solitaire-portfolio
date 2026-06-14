#if !UNITY_WEBGL
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;

namespace Gateway.Auth
{
    public class FirebaseAuthGateway : IAuthGateway
    {
        private FirebaseAuth auth;
        private FirebaseUser user;

        public void Initialize() { }

        public async UniTask<string> GetUuid()
        {
            if (user != null) return user.UserId;

            try
            {
                var status = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (status != DependencyStatus.Available)
                {
                    Debug.LogError($"[FirebaseAuth] Dependencies unavailable: {status}");
                    return string.Empty;
                }

                auth = FirebaseAuth.DefaultInstance;
                var result = await auth.SignInAnonymouslyAsync();
                user = result.User;
                Debug.Log($"[FirebaseAuth] Signed in: {user.UserId}");
                return user.UserId;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return string.Empty;
            }
        }
    }
}
#endif
