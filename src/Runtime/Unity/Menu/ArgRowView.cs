using TMPro;
using UnityEngine;

namespace Lumera.ZRay
{
    public sealed class ArgRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label = null;
        [SerializeField] private ValueWidget _widget = null;

        public ValueWidget Widget => _widget;

        public void SetLabel(CheatParameter parameter)
        {
            string range = CheatFormatter.FormatRange(parameter);
            _label.text = range.Length == 0 ? parameter.DisplayName : $"{parameter.DisplayName}  {range}";
            _label.color = parameter.IsRequired ? ZRayTheme.TEXT : ZRayTheme.MUTED;
        }
    }
}
