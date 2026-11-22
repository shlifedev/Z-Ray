using System;
using System.Collections.Generic;

namespace Lumera.DebuggerKit
{
    // 치트마다 마지막으로 입력한 인자 글자. 메뉴의 입력칸은 이 값으로 시작한다.
    // 호스트가 하나 갖고 런타임 메뉴, 에디터 창, 콘솔 실행이 함께 쓴다. 어디서 입력했든 다음에 같은 값이 보인다.
    // 저장하지 않는다. 호스트와 함께 사라지므로 플레이가 끝나면 지워진다.
    public sealed class CheatDraftStore
    {
        private readonly Dictionary<string, string[]> _drafts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // 치트의 인자마다 입력 중인 글자. 처음에는 인자의 기본값으로 채운다(그대로 실행하면 기본값이 쓰인다).
        // 돌려받은 배열을 고치면 그대로 남는다. 판이 바뀌어 치트가 다시 등록돼도 id가 같으면 같은 배열이다.
        public string[] Get(CheatDefinition definition)
        {
            if (_drafts.TryGetValue(definition.Id, out string[] drafts) && drafts.Length == definition.Parameters.Count)
            {
                return drafts;
            }

            drafts = new string[definition.Parameters.Count];
            for (int index = 0; index < drafts.Length; index++)
            {
                drafts[index] = definition.Parameters[index].FormatDefault();
            }

            _drafts[definition.Id] = drafts;
            return drafts;
        }

        // 실행한 인자 글자를 기억한다. 생략한 뒤쪽 인자는 입력하던 값을 그대로 둔다.
        public void Remember(CheatDefinition definition, IReadOnlyList<string> argumentTexts)
        {
            string[] drafts = Get(definition);
            for (int index = 0; index < argumentTexts.Count && index < drafts.Length; index++)
            {
                drafts[index] = argumentTexts[index];
            }
        }
    }
}
