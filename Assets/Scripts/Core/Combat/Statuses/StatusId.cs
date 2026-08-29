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
        DrumOfBattle,
        /// <summary>彼岸咆哮:回合開始時對所有敵人造成等同層數的攻擊傷害(等同於從消耗堆再打一次)。</summary>
        HowlFromBeyond,
        /// <summary>無情猛攻:接下來等同層數的攻擊牌費用為 0,每打出一張消耗一層。</summary>
        NextAttackFree,
        /// <summary>腐化:技能牌費用為 0,但打出後一律消耗。</summary>
        Corruption,
        /// <summary>連環拳:接下來等同層數的攻擊牌會額外生效一次,每打出一張消耗一層。</summary>
        NextAttackDoubled,
        /// <summary>殘酷:你對「有易傷的敵人」造成的傷害額外增加等同層數的百分比。</summary>
        Cruelty,
        /// <summary>巨像:本回合「有易傷的攻擊者」對你造成的傷害減半(回合結束移除)。</summary>
        Colossus,
        /// <summary>
        /// 覆甲:自己回合結束時獲得等同層數的格擋,然後層數 -1(打出的當回合就生效)。
        /// 刻意不用 DecrementAtOwnerTurnEnd:那條規則有「同回合剛施加就跳過首次」的語意,
        /// 覆甲不跳。
        /// </summary>
        Plating,
        /// <summary>岿然不動:本回合第一次獲得格擋時,該次格擋翻倍。</summary>
        Unmovable,
        /// <summary>擒拿:本回合每當你獲得格擋,對隨機敵人造成等同層數的傷害(回合結束移除)。</summary>
        Grapple,
        /// <summary>兇惡:每當你對敵人施加易傷,抽等同層數的牌。</summary>
        Vicious,
        /// <summary>躍躍欲試的副作用:本回合不再獲得任何額外能量(回合結束移除)。</summary>
        NoEnergyGain,
        /// <summary>好勇鬥狠:回合開始時從棄牌堆撈一張隨機攻擊牌到手上並升級它。</summary>
        Aggression,
        /// <summary>地獄狂徒:每當你抽到名字含「打擊」的牌,立刻對隨機敵人打出它。</summary>
        Hellraiser,
        /// <summary>驚逃:回合結束時隨機打出手上一張攻擊牌。</summary>
        Stampede,
        /// <summary>雜耍:每回合你打出的第三張攻擊牌,把一張複製品加入手牌。</summary>
        Juggling
    }
}
