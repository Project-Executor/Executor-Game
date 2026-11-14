using UnityEngine;

namespace Executor.Network
{
    /// <summary>
    /// 토큰 보관. 게임을 다시 켜도 로그인이 유지되도록 PlayerPrefs에 저장한다
    /// (Windows에서는 레지스트리 HKCU\Software\{회사}\{제품}).
    /// </summary>
    public static class TokenStore
    {
        private const string AccessKey = "executor.accessToken";
        private const string RefreshKey = "executor.refreshToken";

        private static string _access;
        private static string _refresh;
        private static bool _loaded;

        public static string AccessToken
        {
            get { Load(); return _access; }
        }

        public static string RefreshToken
        {
            get { Load(); return _refresh; }
        }

        /// <summary>저장된 로그인이 있는지 (Refresh Token이 있으면 재발급으로 이어갈 수 있다)</summary>
        public static bool HasSession => !string.IsNullOrEmpty(RefreshToken);

        public static void Save(string accessToken, string refreshToken)
        {
            _access = accessToken;
            _refresh = refreshToken;
            _loaded = true;
            PlayerPrefs.SetString(AccessKey, accessToken);
            PlayerPrefs.SetString(RefreshKey, refreshToken);
            PlayerPrefs.Save();
        }

        public static void Clear()
        {
            _access = null;
            _refresh = null;
            _loaded = true;
            PlayerPrefs.DeleteKey(AccessKey);
            PlayerPrefs.DeleteKey(RefreshKey);
            PlayerPrefs.Save();
        }

        private static void Load()
        {
            if (_loaded) return;
            _access = PlayerPrefs.GetString(AccessKey, null);
            _refresh = PlayerPrefs.GetString(RefreshKey, null);
            _loaded = true;
        }
    }
}
