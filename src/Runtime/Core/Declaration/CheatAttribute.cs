using System;

namespace Lumera.ZRay
{
    // 메서드를 치트로 선언한다. 콘솔 명령, 자동완성, 도움말, 치트 메뉴 행이 모두 이 선언 하나에서 만들어진다.
    // 정적 메서드는 호스트가 시작할 때 저절로 등록되고, 인스턴스 메서드는 대상 객체를 Register로 넘길 때 등록된다.
    // 반환형은 void, string(성공 메시지), CheatResult(성공·실패와 메시지)만 받는다.
    // 매개변수의 C# 기본값이 그대로 콘솔·메뉴의 기본값이 된다.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CheatAttribute : Attribute
    {
        // 콘솔에 치는 이름. 점으로 나눠 묶는다(예: player.gold.add). 대소문자는 구분하지 않는다.
        public string Id { get; }
        // 메뉴에 보이는 이름. 비우면 Id를 쓴다.
        public string Name { get; set; } = "";
        // 메뉴의 분류. '/'로 하위 분류를 나눈다(예: "플레이어/재화"). 비우면 "기타"다.
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        // 같은 분류 안에서 작은 값이 먼저 보인다.
        public int Order { get; set; }
        // false면 콘솔에서만 쓴다(help, clear 같은 콘솔 명령).
        public bool IsShownInMenu { get; set; } = true;

        public CheatAttribute(string id)
        {
            Id = id;
        }
    }
}
