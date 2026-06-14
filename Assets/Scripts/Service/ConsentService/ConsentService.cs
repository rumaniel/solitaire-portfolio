using UnityEngine;

namespace Service.ConsentService
{
    /// <summary>PlayerPrefs 기반 정책 버전 동의 영속화. 정책 변경 시 CurrentPolicyVersion만 bump하면 재프롬프트.</summary>
    public class ConsentService : IConsentService
    {
        private const string AcceptedVersionKey = "consent.policy_version_accepted";
        public const int CurrentPolicyVersion = 1;

        public int PolicyVersion => CurrentPolicyVersion;
        public int AcceptedVersion => PlayerPrefs.GetInt(AcceptedVersionKey, 0);
        public bool NeedsConsent => AcceptedVersion < CurrentPolicyVersion;

        public void MarkAccepted()
        {
            PlayerPrefs.SetInt(AcceptedVersionKey, CurrentPolicyVersion);
            PlayerPrefs.Save();
        }
    }
}
