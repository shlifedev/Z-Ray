using UnityEngine;

namespace Lumera.ZRay
{
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
