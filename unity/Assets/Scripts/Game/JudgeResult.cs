namespace Hero.Game
{
    public enum JudgeResult
    {
        Good,            // 先頭文字一致 & 未使用
        Bad,             // 文字不一致
        AlreadyUsed,     // 既出
    }
}
