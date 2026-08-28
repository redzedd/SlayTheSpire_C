using System;
using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat;
using STS.Core.Combat.Enemies;
using STS.Core.Content;
using STS.Core.Map;
using STS.Core.Relics;
using STS.Core.Rng;

namespace STS.Core.Run
{
    /// <summary>進入節點的結果(UI 據此切畫面)。</summary>
    public readonly struct NodeEntry
    {
        public readonly MapNodeType NodeType;
        /// <summary>戰鬥類節點要打的遭遇 id;其餘為 null。</summary>
        public readonly string EncounterId;
        /// <summary>寶箱節點開出的遺物 id(已自動入包);無可拿時為 null。</summary>
        public readonly string TreasureRelicId;

        public NodeEntry(MapNodeType nodeType, string encounterId, string treasureRelicId)
        {
            NodeType = nodeType;
            EncounterId = encounterId;
            TreasureRelicId = treasureRelicId;
        }
    }

    /// <summary>
    /// 戰後獎勵清單。每一項都要玩家自己點才入帳(對應「搜刮!」畫面的逐項領取),
    /// 領完或離開才回地圖。已領取的項目留在清單裡但標記為已領,UI 據此劃掉。
    /// </summary>
    public sealed class CombatRewards
    {
        public int Gold;
        public bool GoldClaimed;
        public readonly List<string> CardChoices = new List<string>();
        public bool CardResolved;
        public string PotionId;
        public bool PotionClaimed;
        public string RelicId;
        public bool RelicClaimed;

        public bool HasGold => Gold > 0 && !GoldClaimed;
        public bool HasPotion => PotionId != null && !PotionClaimed;
        public bool HasRelic => RelicId != null && !RelicClaimed;
        public bool HasCard => CardChoices.Count > 0 && !CardResolved;
        public bool AllClaimed => !HasGold && !HasPotion && !HasRelic && !HasCard;
    }

    /// <summary>
    /// 商店庫存與定價。CardIds 前 ClassCardCount 張是職業牌、其後是無色牌——
    /// 兩區共用同一份索引,買牌的介面才不用分兩套。
    /// </summary>
    public sealed class ShopInventory
    {
        public readonly List<string> CardIds = new List<string>();
        public readonly List<int> CardCosts = new List<int>();
        public readonly List<string> RelicIds = new List<string>();
        public readonly List<string> PotionIds = new List<string>();
        public int ClassCardCount;
        public int RelicCost;
        public int PotionCost;
        public int RemoveCost;
    }

    /// <summary>
    /// 爬塔流程引擎:地圖行進、遭遇抽池、獎勵、商店、燈火。戰鬥本身交給 CombatEngine,
    /// Run 層只負責「戰鬥前的組裝」與「戰鬥後的結果回寫」。
    /// 亂數分流([近似] 指派):地圖/遭遇=Map 流、卡獎=CardReward、藥水=PotionReward、
    /// 遺物=RelicReward、金幣=CombatMisc。
    /// </summary>
    public sealed class RunEngine
    {
        public readonly RunState State;
        /// <summary>戰後獎勵(ChoosingReward 階段有效)。</summary>
        public CombatRewards PendingRewards { get; private set; }
        /// <summary>商店庫存(InShop 階段有效)。</summary>
        public ShopInventory Shop { get; private set; }

        private readonly IContentCatalog _catalog;

        public RunEngine(IContentCatalog catalog, RunState state)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>開新一輪:生地圖、灌起始配置(全部來自 BalanceDef)。</summary>
        public static RunEngine NewRun(IContentCatalog catalog, ulong seed)
        {
            var balance = catalog.Balance;
            var state = new RunState
            {
                Seed = seed,
                Rng = RunRng.FromSeed(seed),
                Hp = balance.StartHp,
                MaxHp = balance.StartHp,
                Gold = balance.StartGold
            };
            state.Map = MapGenerator.Generate(balance, state.Rng.Map);
            foreach (var cardId in balance.StartingDeckCardIds)
            {
                state.Deck.Add(new CardInstance(state.NextCardInstanceId++, cardId));
            }
            foreach (var relicId in balance.StartingRelicIds)
            {
                state.Relics.Add(new RelicInstance(relicId));
            }
            for (int i = 0; i < balance.StartingPotionIds.Length && i < state.PotionSlots.Length; i++)
            {
                state.PotionSlots[i] = balance.StartingPotionIds[i];
            }
            return new RunEngine(catalog, state);
        }

        // ---- 地圖行進 ----

        public List<int> GetReachableNodeIds()
        {
            if (State.Phase != RunPhase.ChoosingNode) return new List<int>();
            if (State.CurrentNodeId < 0) return State.Map.NodeIdsAtRow(0);
            return State.Map.NextNodeIds(State.CurrentNodeId);
        }

        public NodeEntry EnterNode(int nodeId)
        {
            if (State.Phase != RunPhase.ChoosingNode)
            {
                throw new InvalidOperationException("現在不是選節點的階段");
            }
            if (!GetReachableNodeIds().Contains(nodeId))
            {
                throw new InvalidOperationException($"節點 {nodeId} 不可從目前位置抵達");
            }
            var node = State.Map.NodeById(nodeId);
            State.CurrentNodeId = nodeId;
            State.Floor++;

            switch (node.Type)
            {
                case MapNodeType.Combat:
                case MapNodeType.Elite:
                case MapNodeType.Boss:
                {
                    State.Phase = RunPhase.InCombat;
                    return new NodeEntry(node.Type, PickEncounter(node.Type), null);
                }
                case MapNodeType.Shop:
                    State.Phase = RunPhase.InShop;
                    Shop = BuildShop();
                    return new NodeEntry(node.Type, null, null);
                case MapNodeType.Rest:
                    State.Phase = RunPhase.AtRest;
                    return new NodeEntry(node.Type, null, null);
                case MapNodeType.Treasure:
                {
                    // 寶箱:直接開,遺物入包,回到選節點
                    string relicId = PickUnownedRelic(State.Rng.RelicReward);
                    if (relicId != null)
                    {
                        State.Relics.Add(new RelicInstance(relicId));
                    }
                    State.Phase = RunPhase.ChoosingNode;
                    return new NodeEntry(node.Type, null, relicId);
                }
                default:
                    throw new InvalidOperationException($"未支援的節點型別 {node.Type}");
            }
        }

        /// <summary>依 Run 狀態組出開戰參數(牌組/遺物實體共用參照:計數器與升級跨戰鬥持久)。</summary>
        public CombatSetup BuildCombatSetup(string encounterId)
        {
            var encounter = _catalog.GetEncounter(encounterId);
            var setup = new CombatSetup
            {
                PlayerHp = State.Hp,
                PlayerMaxHp = State.MaxHp
            };
            setup.Deck.AddRange(State.Deck);
            setup.EnemyIds.AddRange(encounter.EnemyIds);
            setup.Relics.AddRange(State.Relics);
            foreach (var potionId in State.PotionSlots)
            {
                if (potionId != null) setup.PotionIds.Add(potionId);
            }
            return setup;
        }

        /// <summary>
        /// 戰鬥結束回寫。usedPotionIds:戰鬥中用掉的藥水(從欄位移除)。
        /// 勝利:Boss → 通關;其餘 → 產生獎勵進 ChoosingReward。敗北 → GameOver。
        /// </summary>
        public void ApplyCombatResult(bool victory, int remainingHp, IReadOnlyList<string> usedPotionIds = null)
        {
            if (State.Phase != RunPhase.InCombat)
            {
                throw new InvalidOperationException("現在不在戰鬥中,無法回寫戰果");
            }
            State.Hp = remainingHp < 0 ? 0 : remainingHp;
            if (usedPotionIds != null)
            {
                foreach (var used in usedPotionIds)
                {
                    for (int i = 0; i < State.PotionSlots.Length; i++)
                    {
                        if (State.PotionSlots[i] == used)
                        {
                            State.PotionSlots[i] = null;
                            break;
                        }
                    }
                }
            }

            if (!victory || State.Hp <= 0)
            {
                State.Phase = RunPhase.GameOver;
                return;
            }

            var node = State.Map.NodeById(State.CurrentNodeId);
            if (node.Type == MapNodeType.Boss)
            {
                State.Phase = RunPhase.RunClear;
                return;
            }
            if (node.Type == MapNodeType.Combat)
            {
                State.NormalCombatsFought++;
            }
            PendingRewards = GenerateRewards(node.Type);
            State.Phase = RunPhase.ChoosingReward;
        }

        /// <summary>領取金幣。已領過或沒有金幣時回 false(UI 不必自己防呆)。</summary>
        public bool ClaimRewardGold()
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            if (!PendingRewards.HasGold) return false;
            State.Gold += PendingRewards.Gold;
            PendingRewards.GoldClaimed = true;
            return true;
        }

        /// <summary>領取藥水。藥水欄滿了就拿不走,回 false 讓 UI 提示。</summary>
        public bool ClaimRewardPotion()
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            if (!PendingRewards.HasPotion) return false;
            int slot = FreePotionSlot();
            if (slot < 0) return false;
            State.PotionSlots[slot] = PendingRewards.PotionId;
            PendingRewards.PotionClaimed = true;
            return true;
        }

        public bool ClaimRewardRelic()
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            if (!PendingRewards.HasRelic) return false;
            State.Relics.Add(new RelicInstance(PendingRewards.RelicId));
            PendingRewards.RelicClaimed = true;
            return true;
        }

        /// <summary>選走一張卡牌獎勵;卡牌那一項就此結案(不會再出現)。</summary>
        public void TakeCardReward(int choiceIndex)
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            if (!PendingRewards.HasCard)
            {
                throw new InvalidOperationException("卡牌獎勵已處理過");
            }
            if (choiceIndex < 0 || choiceIndex >= PendingRewards.CardChoices.Count)
            {
                throw new InvalidOperationException("卡牌獎勵索引無效");
            }
            State.Deck.Add(new CardInstance(State.NextCardInstanceId++, PendingRewards.CardChoices[choiceIndex]));
            PendingRewards.CardResolved = true;
        }

        /// <summary>不選卡:卡牌那一項結案,其餘獎勵仍可領。</summary>
        public void SkipCardReward()
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            PendingRewards.CardResolved = true;
        }

        /// <summary>離開獎勵畫面回地圖——沒領的就是不要了。</summary>
        public void LeaveRewards()
        {
            RequirePhase(RunPhase.ChoosingReward, "獎勵");
            PendingRewards = null;
            State.Phase = RunPhase.ChoosingNode;
        }

        // ---- 商店 ----

        public bool BuyCard(int index)
        {
            RequirePhase(RunPhase.InShop, "商店");
            if (index < 0 || index >= Shop.CardIds.Count || Shop.CardIds[index] == null) return false;
            int cost = Shop.CardCosts[index];
            if (State.Gold < cost) return false;
            State.Gold -= cost;
            State.Deck.Add(new CardInstance(State.NextCardInstanceId++, Shop.CardIds[index]));
            Shop.CardIds[index] = null;   // 售出即下架
            return true;
        }

        public bool BuyRelic(int index)
        {
            RequirePhase(RunPhase.InShop, "商店");
            if (index < 0 || index >= Shop.RelicIds.Count || Shop.RelicIds[index] == null) return false;
            if (State.Gold < Shop.RelicCost) return false;
            State.Gold -= Shop.RelicCost;
            State.Relics.Add(new RelicInstance(Shop.RelicIds[index]));
            Shop.RelicIds[index] = null;
            return true;
        }

        public bool BuyPotion(int index)
        {
            RequirePhase(RunPhase.InShop, "商店");
            if (index < 0 || index >= Shop.PotionIds.Count || Shop.PotionIds[index] == null) return false;
            int freeSlot = FreePotionSlot();
            if (freeSlot < 0) return false;
            if (State.Gold < Shop.PotionCost) return false;
            State.Gold -= Shop.PotionCost;
            State.PotionSlots[freeSlot] = Shop.PotionIds[index];
            Shop.PotionIds[index] = null;
            return true;
        }

        public bool BuyRemoveCard(int deckIndex)
        {
            RequirePhase(RunPhase.InShop, "商店");
            if (deckIndex < 0 || deckIndex >= State.Deck.Count) return false;
            if (State.Gold < Shop.RemoveCost) return false;
            State.Gold -= Shop.RemoveCost;
            State.Deck.RemoveAt(deckIndex);
            State.ShopRemovesPurchased++;
            Shop.RemoveCost = _catalog.Balance.ShopRemoveBaseCost
                + _catalog.Balance.ShopRemoveCostIncrement * State.ShopRemovesPurchased;
            return true;
        }

        public void LeaveShop()
        {
            RequirePhase(RunPhase.InShop, "商店");
            Shop = null;
            State.Phase = RunPhase.ChoosingNode;
        }

        // ---- 燈火 ----

        public void RestHeal()
        {
            RequirePhase(RunPhase.AtRest, "燈火");
            int heal = State.MaxHp * _catalog.Balance.RestHealPercent / 100;
            State.Hp = State.Hp + heal > State.MaxHp ? State.MaxHp : State.Hp + heal;
            State.Phase = RunPhase.ChoosingNode;
        }

        public void RestUpgrade(int deckIndex)
        {
            RequirePhase(RunPhase.AtRest, "燈火");
            if (deckIndex < 0 || deckIndex >= State.Deck.Count)
            {
                throw new InvalidOperationException("卡組索引無效");
            }
            var card = State.Deck[deckIndex];
            if (card.Upgraded)
            {
                throw new InvalidOperationException("這張卡已升級過");
            }
            card.Upgraded = true;
            State.Phase = RunPhase.ChoosingNode;
        }

        // ---- 內部:抽池與獎勵 ----

        private string PickEncounter(MapNodeType nodeType)
        {
            EncounterPool pool;
            switch (nodeType)
            {
                case MapNodeType.Boss:
                    pool = EncounterPool.Boss;
                    break;
                case MapNodeType.Elite:
                    pool = EncounterPool.Elite;
                    break;
                default:
                    pool = State.NormalCombatsFought < _catalog.Balance.WeakPoolFightCount
                        ? EncounterPool.Weak
                        : EncounterPool.Normal;
                    break;
            }
            var candidates = new List<EncounterDef>();
            int totalWeight = 0;
            foreach (var encounter in _catalog.AllEncounterDefs)
            {
                if (encounter.Pool != pool) continue;
                candidates.Add(encounter);
                totalWeight += encounter.Weight <= 0 ? 1 : encounter.Weight;
            }
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException($"遭遇池 {pool} 是空的——資料缺內容");
            }
            int roll = State.Rng.Map.NextInt(totalWeight);
            foreach (var encounter in candidates)
            {
                roll -= encounter.Weight <= 0 ? 1 : encounter.Weight;
                if (roll < 0) return encounter.Id;
            }
            return candidates[candidates.Count - 1].Id;
        }

        private CombatRewards GenerateRewards(MapNodeType nodeType)
        {
            var balance = _catalog.Balance;
            var rewards = new CombatRewards();

            // 金幣:只擲出數量,實際入帳等玩家在「搜刮!」清單上點下去
            rewards.Gold = nodeType == MapNodeType.Elite
                ? State.Rng.CombatMisc.Range(balance.EliteGoldMin, balance.EliteGoldMax)
                : State.Rng.CombatMisc.Range(balance.NormalGoldMin, balance.NormalGoldMax);

            // 卡牌三選一(不重複)
            for (int i = 0; i < 3; i++)
            {
                string pick = PickRewardCard(rewards.CardChoices);
                if (pick != null) rewards.CardChoices.Add(pick);
            }

            // 藥水掉落(未掉+、掉了-);掉出來只是上架,入包要玩家自己領
            int chance = balance.PotionDropBasePercent + State.PotionChanceOffset;
            if (State.Rng.PotionReward.NextInt(100) < chance)
            {
                State.PotionChanceOffset -= balance.PotionDropDeltaPercent;
                rewards.PotionId = PickRandomPotion();
            }
            else
            {
                State.PotionChanceOffset += balance.PotionDropDeltaPercent;
            }

            // 精英掉遺物
            if (nodeType == MapNodeType.Elite)
            {
                rewards.RelicId = PickUnownedRelic(State.Rng.RelicReward);
            }
            return rewards;
        }

        private string PickRewardCard(List<string> exclude)
        {
            var balance = _catalog.Balance;
            int roll = State.Rng.CardReward.NextInt(
                balance.CardRewardCommonWeight + balance.CardRewardUncommonWeight + balance.CardRewardRareWeight);
            CardRarity rarity;
            if ((roll -= balance.CardRewardCommonWeight) < 0) rarity = CardRarity.Common;
            else if ((roll -= balance.CardRewardUncommonWeight) < 0) rarity = CardRarity.Uncommon;
            else rarity = CardRarity.Rare;

            var pool = new List<string>();
            foreach (var card in _catalog.AllCardDefs)
            {
                if (card.Rarity != rarity) continue;
                if (card.Colorless) continue;   // 無色牌只在商店無色區出現
                if (card.Type == CardType.Status || card.Type == CardType.Curse) continue;
                if (card.Id.EndsWith("+")) continue;
                if (exclude.Contains(card.Id)) continue;
                pool.Add(card.Id);
            }
            if (pool.Count == 0) return null;
            return pool[State.Rng.CardReward.NextInt(pool.Count)];
        }

        /// <summary>無色牌池:不分稀有度,商店無色區專用。</summary>
        private string PickColorlessCard(List<string> exclude)
        {
            var pool = new List<string>();
            foreach (var card in _catalog.AllCardDefs)
            {
                if (!card.Colorless) continue;
                if (card.Id.EndsWith("+")) continue;
                if (exclude.Contains(card.Id)) continue;
                pool.Add(card.Id);
            }
            if (pool.Count == 0) return null;
            return pool[State.Rng.CardReward.NextInt(pool.Count)];
        }

        /// <param name="exclude">額外排除的 id(例如同一次商店已上架的),null = 不排除。</param>
        private string PickUnownedRelic(RngStream rng, List<string> exclude = null)
        {
            var pool = new List<string>();
            foreach (var relic in _catalog.AllRelicDefs)
            {
                if (exclude != null && exclude.Contains(relic.Id)) continue;
                bool owned = false;
                foreach (var mine in State.Relics)
                {
                    if (mine.Id == relic.Id)
                    {
                        owned = true;
                        break;
                    }
                }
                if (!owned) pool.Add(relic.Id);
            }
            if (pool.Count == 0) return null;
            return pool[rng.NextInt(pool.Count)];
        }

        /// <param name="exclude">額外排除的 id(同一次商店已上架的),null = 不排除。</param>
        private string PickRandomPotion(List<string> exclude = null)
        {
            var pool = new List<string>();
            foreach (var potion in _catalog.AllPotionDefs)
            {
                if (exclude != null && exclude.Contains(potion.Id)) continue;
                pool.Add(potion.Id);
            }
            if (pool.Count == 0) return null;
            return pool[State.Rng.PotionReward.NextInt(pool.Count)];
        }

        private ShopInventory BuildShop()
        {
            var balance = _catalog.Balance;
            var shop = new ShopInventory
            {
                RelicCost = balance.ShopRelicCost,
                PotionCost = balance.ShopPotionCost,
                RemoveCost = balance.ShopRemoveBaseCost
                    + balance.ShopRemoveCostIncrement * State.ShopRemovesPurchased
            };
            // 上排職業牌(不重複),定價按稀有度
            var exclude = new List<string>();
            for (int i = 0; i < balance.ShopClassCardCount; i++)
            {
                string cardId = PickRewardCard(exclude);
                if (cardId == null) break;
                exclude.Add(cardId);
                AddShopCard(shop, cardId, balance);
            }
            shop.ClassCardCount = shop.CardIds.Count;
            // 下排無色牌(另一個池,不重複)
            for (int i = 0; i < balance.ShopColorlessCardCount; i++)
            {
                string cardId = PickColorlessCard(exclude);
                if (cardId == null) break;
                exclude.Add(cardId);
                AddShopCard(shop, cardId, balance);
            }

            // 抽到已上架的就換一個再抽:去重不能靠丟掉這一格,否則貨架會少格
            for (int i = 0; i < balance.ShopRelicCount; i++)
            {
                string relicId = PickUnownedRelic(State.Rng.RelicReward, shop.RelicIds);
                if (relicId == null) break;   // 池抽乾了才收手
                shop.RelicIds.Add(relicId);
            }
            for (int i = 0; i < balance.ShopPotionCount; i++)
            {
                string potionId = PickRandomPotion(shop.PotionIds);
                if (potionId == null) break;   // 藥水種類不夠就少擺幾格,不重複上架
                shop.PotionIds.Add(potionId);
            }
            return shop;
        }

        private void AddShopCard(ShopInventory shop, string cardId, BalanceDef balance)
        {
            shop.CardIds.Add(cardId);
            var def = _catalog.GetCard(cardId);
            int cost = def.Rarity == CardRarity.Rare
                ? balance.ShopCardRareCost
                : (def.Rarity == CardRarity.Uncommon ? balance.ShopCardUncommonCost : balance.ShopCardCommonCost);
            shop.CardCosts.Add(cost);
        }

        private int FreePotionSlot()
        {
            for (int i = 0; i < State.PotionSlots.Length; i++)
            {
                if (State.PotionSlots[i] == null) return i;
            }
            return -1;
        }

        private void RequirePhase(RunPhase phase, string what)
        {
            if (State.Phase != phase)
            {
                throw new InvalidOperationException($"現在不在{what}階段");
            }
        }
    }
}
