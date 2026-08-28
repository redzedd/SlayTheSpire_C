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
        LoseStrengthAtTurnEnd,
        /// <summary>壁壘:格擋不在回合開始被清除。</summary>
        Barricade,
        /// <summary>無懼疼痛:每有一張牌被消耗,獲得等同層數的格擋。</summary>
        FeelNoPain,
        /// <summary>黑暗之擁:每有一張牌被消耗,抽等同層數的牌。</summary>
        DarkEmbrace,
        /// <summary>勢不可當:每次獲得格擋,對隨機敵人造成等同層數的傷害。</summary>
        Juggernaut,
        /// <summary>撕裂:自己回合內失去生命時,獲得等同層數的力量。</summary>
        Rupture,
        /// <summary>獄火:回合開始失去 1 點生命;自己回合內失去生命時對所有敵人造成等同層數的傷害。</summary>
        Inferno,
        /// <summary>薪火之源:回合開始獲得等同層數的能量。</summary>
        Pyre,
        /// <summary>緋紅披風:回合開始失去 1 點生命並獲得等同層數的格擋。</summary>
        CrimsonMantle,
        /// <summary>狂怒:本回合每打出一張攻擊牌就獲得等同層數的格擋(回合結束移除)。</summary>
        Rage,
        /// <summary>戰鼓:回合開始時消耗抽牌堆頂部等同層數的牌。</summary>
        DrumOfBattle
    }
}
