using System.Collections.Generic;
using STS.Core.Cards;

namespace STS.Core.Combat
{
    /// <summary>
    /// 開戰參數。M1 過渡型:敵人直接內嵌招式;M2 導入 EnemyDef/EncounterDef 後由其取代。
    /// </summary>
    public sealed class CombatSetup
    {
        public int PlayerHp = 80;
        public int PlayerMaxHp = 80;
        public int MaxEnergy = 3;
        public List<CardInstance> Deck = new List<CardInstance>();
        public List<EnemySetup> Enemies = new List<EnemySetup>();
    }

    /// <summary>M1 簡化敵人:每回合固定執行同一組步驟。</summary>
    public sealed class EnemySetup
    {
        public string Name;
        public int Hp;
        public EffectStep[] MoveSteps = System.Array.Empty<EffectStep>();
    }
}
