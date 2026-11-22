using UnityEngine;

namespace Lumera.DebuggerKit
{
    // 입력 기록과 즐겨찾기를 PlayerPrefs에 남긴다. 개발 도구의 편의 상태라 기기마다 따로 가져도 된다.
    internal sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string GetStringOrNull(string key)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }
    }
}
