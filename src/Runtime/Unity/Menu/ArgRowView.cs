using TMPro;
using UnityEngine;

namespace Lumera.ZRay
{
    // 펼친 치트 행 안의 인자 한 줄: 이름(범위)과 입력 위젯.
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
