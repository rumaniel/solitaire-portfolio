using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Gateway.Auth
{
    /// <summary>
    /// Fallback auth gateway that provides a locally persisted user ID.
    /// </summary>
    public class LocalAuthGateway : IAuthGateway
    {
        private const string UserIdKey = "LocalAuthGateway.UserId";

        private string userId;

        public void Initialize() { }

        public UniTask<string> GetUuid()
        {
            if (!string.IsNullOrEmpty(userId)) return UniTask.FromResult(userId);

            userId = PlayerPrefs.GetString(UserIdKey, string.Empty);
            if (string.IsNullOrEmpty(userId))
            {
                userId = GenerateInitialId();
                PlayerPrefs.SetString(UserIdKey, userId);
                PlayerPrefs.Save();
            }
            return UniTask.FromResult(userId);
        }

        private static string GenerateInitialId()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            if (!string.IsNullOrEmpty(deviceId) && deviceId != SystemInfo.unsupportedIdentifier)
                return deviceId;
            return Guid.NewGuid().ToString("N");
        }
    }
}
