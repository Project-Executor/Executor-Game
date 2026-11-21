using UnityEngine;

namespace Executor.Play
{
    /// <summary>
    /// 이 PC에 저장하는 설정 (PlayerPrefs). 소리는 아직 없어서 볼륨은 값만 저장해 두고, 소리가 생기면 여기 값을 쓴다.
    /// 라이트 테마·글리치 효과는 업데이트 예정이라 값만 있다
    /// </summary>
    public static class GameSettings
    {
        private const string MasterKey = "executor.settings.master", BgmKey = "executor.settings.bgm", SfxKey = "executor.settings.sfx";
        private const string TypingKey = "executor.settings.typingSound", GaugeKey = "executor.settings.turnGauge";
        private const string FullKey = "executor.settings.fullscreen";

        /// <summary>0~1</summary>
        public static float Master { get => PlayerPrefs.GetFloat(MasterKey, 0.8f); set => PlayerPrefs.SetFloat(MasterKey, value); }
        public static float Bgm { get => PlayerPrefs.GetFloat(BgmKey, 0.6f); set => PlayerPrefs.SetFloat(BgmKey, value); }
        public static float Sfx { get => PlayerPrefs.GetFloat(SfxKey, 0.9f); set => PlayerPrefs.SetFloat(SfxKey, value); }
        /// <summary>해킹할 때 키보드 소리</summary>
        public static bool TypingSound { get => PlayerPrefs.GetInt(TypingKey, 1) == 1; set => PlayerPrefs.SetInt(TypingKey, value ? 1 : 0); }
        /// <summary>HUD turns 칸의 남은 턴 칸 막대</summary>
        public static bool TurnGauge { get => PlayerPrefs.GetInt(GaugeKey, 1) == 1; set => PlayerPrefs.SetInt(GaugeKey, value ? 1 : 0); }
        /// <summary>전체 화면 (false면 창)</summary>
        public static bool Fullscreen { get => PlayerPrefs.GetInt(FullKey, Screen.fullScreen ? 1 : 0) == 1; set => PlayerPrefs.SetInt(FullKey, value ? 1 : 0); }

        public static void Save() => PlayerPrefs.Save();

        /// <summary>저장한 화면 모드를 적용한다 (에디터에서는 바뀌지 않는다). onlyIfSaved면 설정에서 저장한 적이 있을 때만</summary>
        public static void ApplyScreen(bool onlyIfSaved = false)
        {
            if (Application.isEditor || (onlyIfSaved && !PlayerPrefs.HasKey(FullKey))) return;
            if (Fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }

        /// <summary>바뀌면 화면들이 다시 그린다 (남은 턴 게이지 등)</summary>
        public static event System.Action Changed;

        public static void NotifyChanged() => Changed?.Invoke();
    }
}
