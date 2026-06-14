namespace Service.ConsentService
{
    public interface IConsentService
    {
        /// <summary>현재 정책 버전. 버전 bump 시 NeedsConsent가 다시 true가 된다.</summary>
        int PolicyVersion { get; }

        /// <summary>저장된 동의가 PolicyVersion 미만이면 true. 첫 실행도 true.</summary>
        bool NeedsConsent { get; }

        /// <summary>현재 PolicyVersion으로 동의를 영속화한다.</summary>
        void MarkAccepted();

        /// <summary>저장된 동의 버전 (없으면 0).</summary>
        int AcceptedVersion { get; }
    }
}
