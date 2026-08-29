using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using STS.Core.Content;
using STS.Core.Potions;
using STS.Core.Relics;
using STS.Core.Rng;

namespace STS.Core.Tests
{
    /// <summary>測試用內容庫:字典實作 IContentDb。查無 id 由字典自然拋 KeyNotFoundException(fail fast)。</summary>
    internal sealed class TestDb : IContentDb
    {
        public readonly Dictionary<string, CardDef> Cards = new Dictionary<string, CardDef>();
        public readonly Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>();
        public readonly Dictionary<string, PotionDef> Potions = new Dictionary<string, PotionDef>();

        public CardDef GetCard(string cardId) => Cards[cardId];
        public bool TryGetCard(string cardId, out CardDef def) => Cards.TryGetValue(cardId, out def);
        public EnemyDef GetEnemy(string enemyId) => Enemies[enemyId];
        public PotionDef GetPotion(string potionId) => Potions[potionId];
    }

    /// <summary>全測試共用的內容工廠與引擎組裝。卡牌數值為測試自訂,只驗邏輯不對照原作。</summary>
    internal static class TestContent
    {
        internal static CardDef 打擊()
        {
            return new CardDef
            {
                Id = "strike", Name = "打擊", Type = CardType.Attack, Rarity = CardRarity.Starter, Cost = 1,
                DescriptionTemplate = "造成 {dmg} 點傷害。",
                Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 6) }
            };
        }

        internal static CardDef 防禦()
        {
            return new CardDef
            {
                Id = "defend", Name = "防禦", Type = CardType.Skill, Rarity = CardRarity.Starter, Cost = 1,
                DescriptionTemplate = "獲得 {blk} 點格擋。",
                Steps = new[] { new EffectStep(EffectOp.Block, EffectTarget.Self, 5) }
            };
        }

        internal static CardDef 痛擊()
        {
            return new CardDef
            {
                Id = "bash", Name = "痛擊", Type = CardType.Attack, Rarity = CardRarity.Starter, Cost = 2,
                DescriptionTemplate = "造成 {dmg} 點傷害,施加 2 層易傷。",
                Steps = new[]
                {
                    new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, 8),
                    new EffectStep(EffectOp.ApplyStatus, EffectTarget.TargetEnemy, 2, status: StatusId.Vulnerable)
                }
            };
        }

        /// <summary>單招木樁敵人:attackDamage &gt; 0 每回合攻擊,否則待機。</summary>
        internal static EnemyDef 木樁(string id = "dummy", int hp = 50, int attackDamage = 0)
        {
            MoveDef move = attackDamage > 0
                ? new MoveDef { Id = "hit", Name = "揮擊", Intent = IntentType.Attack, Steps = new[] { new EffectStep(EffectOp.Damage, EffectTarget.TargetEnemy, attackDamage) } }
                : new MoveDef { Id = "idle", Name = "待機", Intent = IntentType.Special, Steps = System.Array.Empty<EffectStep>() };
            return new EnemyDef
            {
                Id = id, Name = "測試敵", HpMin = hp, HpMax = hp,
                Moves = new[] { move },
                LoopScript = new[] { move.Id },
                Ai = AiKind.Loop
            };
        }

        internal static TestDb 基礎DB(params CardDef[] extraDefs)
        {
            var db = new TestDb();
            db.Cards["strike"] = 打擊();
            db.Cards["defend"] = 防禦();
            db.Cards["bash"] = 痛擊();
            foreach (var def in extraDefs) db.Cards[def.Id] = def;
            return db;
        }

        internal static CombatSetup 基礎Setup(string[] deckCardIds, int playerHp = 80, int playerMaxHp = 0,
            string[] relicIds = null, string[] potionIds = null)
        {
            var setup = new CombatSetup
            {
                PlayerHp = playerHp,
                PlayerMaxHp = playerMaxHp <= 0 ? playerHp : playerMaxHp
            };
            for (int i = 0; i < deckCardIds.Length; i++)
            {
                setup.Deck.Add(new CardInstance(i + 1, deckCardIds[i]));
            }
            if (relicIds != null)
            {
                foreach (var id in relicIds) setup.Relics.Add(new RelicInstance(id));
            }
            if (potionIds != null)
            {
                setup.PotionIds.AddRange(potionIds);
            }
            return setup;
        }

        internal static CombatEngine 引擎(TestDb db, CombatSetup setup, ulong seed = 1234UL)
        {
            return new CombatEngine(db, RunRng.FromSeed(seed), setup);
        }

        /// <summary>常用捷徑:strike/defend/bash 卡庫 + 單一木樁敵。</summary>
        internal static CombatEngine 標準引擎(
            string[] deckCardIds,
            int enemyHp = 50,
            int enemyAttack = 0,
            ulong seed = 1234UL,
            int playerHp = 80,
            string[] relicIds = null,
            string[] potionIds = null,
            params CardDef[] extraDefs)
        {
            var db = 基礎DB(extraDefs);
            db.Enemies["dummy"] = 木樁(hp: enemyHp, attackDamage: enemyAttack);
            var setup = 基礎Setup(deckCardIds, playerHp, relicIds: relicIds, potionIds: potionIds);
            setup.EnemyIds.Add("dummy");
            return 引擎(db, setup, seed);
        }

        internal static string[] 重複(string cardId, int count)
        {
            var ids = new string[count];
            for (int i = 0; i < count; i++) ids[i] = cardId;
            return ids;
        }

        internal static int 手牌位置(CombatEngine engine, string cardId)
        {
            for (int i = 0; i < engine.State.Hand.Count; i++)
            {
                if (engine.State.Hand[i].CardId == cardId) return i;
            }
            return -1;
        }

        internal static int 事件數(CombatEngine engine, EventKind kind)
        {
            int count = 0;
            for (int i = 0; i < engine.Events.Count; i++)
            {
                if (engine.Events[i].Kind == kind) count++;
            }
            return count;
        }
    }
}
