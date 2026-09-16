namespace ProjectT.Thread
{
    // 실 버프의 종류. 스폰 시스템, 홀더, UI 등에서 공통으로 참조하는 식별자.
    // 값을 추가/재정렬하면 저장된 씬/에셋의 직렬화 값이 어긋날 수 있으니 주의.
    public enum ThreadType
    {
        Red,
        Blue,
        Gold
    }
}
