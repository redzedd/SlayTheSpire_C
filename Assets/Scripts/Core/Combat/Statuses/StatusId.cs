namespace STS.Core.Combat.Statuses
{
    /// <summary>
    /// 全部狀態效果的識別。None 必須是 0:EffectStep 未指定狀態時的預設值,不能意外落在真實狀態上。
    /// </summary>
    public enum StatusId
    {
        None = 0,
        Strength,
        Dexterity,
        Weak,
        Vulnerable,
        Frail,
        NoDraw,
        Ritual,
        Enrage,
        Curl,
        Metallicize,
        DemonForm,
        SharpHide,
        LoseStrengthAtTurnEnd
    }
}
