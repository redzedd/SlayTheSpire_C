using System;
using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;
using STS.Core.Content;
using STS.Core.Relics;
using STS.Core.Rng;

namespace STS.Core.Combat
{
    /// <summary>
    /// 戰鬥引擎:指令進、事件序列出。每個指令在呼叫內同步結算完畢;
    /// UI 取走 Events 照序播放動畫,期間不回查引擎(GetIntentPreview 是唯一的純查詢例外)。
    /// </summary>
    public sealed class CombatEngine
    {
        /// <summary>SourceIndex/TargetIndex 中代表玩家的值。</summary>
        public const int PlayerIndex = -1;
        private const int CardsPerTurn = 5;

        public readonly CombatState State = new CombatState();
        /// <summary>累積事件;UI 播放完呼叫 ClearEvents。</summary>
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        /// <summary>玩家遺物,獲得順序即 hook 觸發順序。實體由呼叫端持有(Counter 跨戰鬥)。</summary>
        public readonly List<RelicInstance> Relics = new List<RelicInstance>();

        private readonly IContentDb _db;
        private readonly RunRng _rng;
        private readonly List<EnemyRuntime> _enemyRuntimes = new List<EnemyRuntime>();
        private int _nextInstanceId = 1;
        private bool _endedEmitted;
        private int _xEnergySpent;

        // AwaitingChoice 暫存:中斷當下的剩餘步驟與在途卡
        private EffectStep[] _pendingSteps;
        private int _pendingResumeIndex;
        private int _pendingSourceIndex;
        private int _pendingTargetIndex;
        private CardInstance _pendingPlayedCard;
        private CardDef _pendingPlayedDef;
        private bool _pendingIgnoreModifiers;

        /// <summary>延後到「整個效果來源結算完」才生效的格擋(捲曲用)。</summary>
        private readonly struct DeferredBlock
        {
            internal readonly int Index;
            internal readonly int Amount;

            internal DeferredBlock(int index, int amount)
            {
                Index = index;
                Amount = amount;
            }
        }

        private readonly List<DeferredBlock> _deferredBlocks = new List<DeferredBlock>();

        public CombatEngine(IContentDb db, RunRng rng, CombatSetup setup)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            State.MaxEnergy = setup.MaxEnergy;
            State.Player = new CombatantState { Name = "玩家", Hp = setup.PlayerHp, MaxHp = setup.PlayerMaxHp };

            foreach (var enemyId in setup.EnemyIds)
            {
                var def = _db.GetEnemy(enemyId);
                int hp = _rng.CombatMisc.Range(def.HpMin, def.HpMax);
                var enemy = new CombatantState { Name = def.Name, Hp = hp, MaxHp = hp };
                foreach (var initial in def.InitialStatuses)
                {
                    enemy.ModifyStatus(initial.Id, initial.Stacks);
                }
                State.Enemies.Add(enemy);
                var runtime = new EnemyRuntime { Def = def };
                if (def.Ai == AiKind.Custom)
                {
                    runtime.GuardianThreshold = GuardianAi.InitialThreshold;
                }
                _enemyRuntimes.Add(runtime);
            }

            State.DrawPile.AddRange(setup.Deck);
            foreach (var card in setup.Deck)
            {
                if (card.InstanceId >= _nextInstanceId) _nextInstanceId = card.InstanceId + 1;
            }
            Relics.AddRange(setup.Relics);
            State.PotionSlots.AddRange(setup.PotionIds);
        }

        public void ClearEvents()
        {
            Events.Clear();
        }

        public void StartCombat()
        {
            if (State.Phase != CombatPhase.NotStarted)
            {
                throw new InvalidOperationException("戰鬥已經開始過");
            }
            _rng.Shuffle.Shuffle(State.DrawPile);
            Emit(new CombatEvent(EventKind.PileShuffled, amount: State.DrawPile.Count));
            for (int i = 0; i < State.Enemies.Count; i++)
            {
                _enemyRuntimes[i].NextMoveId = EnemyAi.SelectNextMove(this, i, _enemyRuntimes[i], _rng.EnemyAi);
                EmitIntentShown(i);
            }
            BeginPlayerTurn(true);
        }

        public bool CanPlayCard(int handIndex, int targetEnemyIndex, out string reason)
        {
            if (State.Phase != CombatPhase.PlayerTurn)
            {
                reason = "現在不是玩家回合";
                return false;
            }
            if (handIndex < 0 || handIndex >= State.Hand.Count)
            {
                reason = "手牌索引無效";
                return false;
            }
            var def = _db.GetCard(State.Hand[handIndex].ResolvedCardId);
            if (def.Unplayable)
            {
                reason = "此卡不可打出";
                return false;
            }
            if (!def.CostIsX && State.Energy < def.Cost)
            {
                reason = "能量不足";
                return false;
            }
            if (RequiresEnemyTarget(def))
            {
                if (targetEnemyIndex < 0 || targetEnemyIndex >= State.Enemies.Count)
                {
                    reason = "此卡需要指定敵人目標";
                    return false;
                }
                if (!State.Enemies[targetEnemyIndex].IsAlive)
                {
                    reason = "目標已死亡";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        public void PlayCard(int handIndex, int targetEnemyIndex = -1)
        {
            if (!CanPlayCard(handIndex, targetEnemyIndex, out string reason))
            {
                throw new InvalidOperationException("無法出牌:" + reason);
            }
            var card = State.Hand[handIndex];
            var def = _db.GetCard(card.ResolvedCardId);

            int cost = def.CostIsX ? State.Energy : def.Cost;
            _xEnergySpent = def.CostIsX ? State.Energy : 0;
            State.Energy -= cost;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));

            State.Hand.RemoveAt(handIndex);
            Emit(new CombatEvent(EventKind.CardPlayed, sourceIndex: PlayerIndex, targetIndex: targetEnemyIndex,
                cardId: card.CardId, cardInstanceId: card.InstanceId));

            // 出牌 hook(激怒/尖刺皮/雙節棍)在效果結算前觸發([近似] StS 時序,測試鎖定)
            FireHook(new HookContext(HookPoint.CardPlayed, sourceIndex: PlayerIndex, cardType: def.Type));
            if (!State.Player.IsAlive)
            {
                CheckOutcome();
                return;
            }

            _pendingPlayedCard = card;
            _pendingPlayedDef = def;
            EffectResolver.Resolve(this, def.Steps, PlayerIndex, targetEnemyIndex);
            if (State.Phase == CombatPhase.AwaitingChoice) return;   // 等 ResolveChoice 收尾

            FinishCardPlay();
        }

        /// <summary>AwaitingChoice 時由 UI 回填選擇的手牌索引(消耗它們),續跑剩餘效果。</summary>
        public void ResolveChoice(int[] handIndices)
        {
            if (State.Phase != CombatPhase.AwaitingChoice)
            {
                throw new InvalidOperationException("目前沒有待回填的選擇");
            }
            if (handIndices == null || handIndices.Length != State.PendingChoiceCount)
            {
                throw new InvalidOperationException($"必須恰好選擇 {State.PendingChoiceCount} 張手牌");
            }
            var sorted = new List<int>(handIndices);
            sorted.Sort();
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == sorted[i - 1]) throw new InvalidOperationException("選擇的手牌索引重複");
            }
            if (sorted.Count > 0 && (sorted[0] < 0 || sorted[sorted.Count - 1] >= State.Hand.Count))
            {
                throw new InvalidOperationException("選擇的手牌索引無效");
            }
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var card = State.Hand[sorted[i]];
                State.Hand.RemoveAt(sorted[i]);
                ExhaustCard(card);
            }

            State.PendingChoiceCount = 0;
            State.Phase = CombatPhase.PlayerTurn;
            var steps = _pendingSteps;
            int resumeIndex = _pendingResumeIndex;
            int source = _pendingSourceIndex;
            int target = _pendingTargetIndex;
            _pendingSteps = null;

            EffectResolver.ResolveFrom(this, steps, resumeIndex, source, target, _pendingIgnoreModifiers);
            if (State.Phase == CombatPhase.AwaitingChoice) return;   // 防衛:再次中斷(切片不會發生)

            FinishCardPlay();
        }

        public void UsePotion(int slot, int targetEnemyIndex = -1)
        {
            if (State.Phase != CombatPhase.PlayerTurn)
            {
                throw new InvalidOperationException("現在不是玩家回合,無法使用藥水");
            }
            if (slot < 0 || slot >= State.PotionSlots.Count || State.PotionSlots[slot] == null)
            {
                throw new InvalidOperationException("藥水欄位無效或已空");
            }
            var def = _db.GetPotion(State.PotionSlots[slot]);
            if (def.NeedsTarget)
            {
                if (targetEnemyIndex < 0 || targetEnemyIndex >= State.Enemies.Count
                    || !State.Enemies[targetEnemyIndex].IsAlive)
                {
                    throw new InvalidOperationException("此藥水需要指定活著的敵人目標");
                }
            }
            State.PotionSlots[slot] = null;
            // 藥水不吃力量/虛弱/易傷/敏捷/脆弱:瓶裝效果是固定的
            EffectResolver.Resolve(this, def.Steps, PlayerIndex, targetEnemyIndex, ignoreModifiers: true);
            if (State.Phase == CombatPhase.AwaitingChoice)
            {
                throw new NotSupportedException("切片藥水不支援中斷選擇型效果");
            }
            FlushDeferredBlocks();   // 整瓶藥水打完才結清延後格擋
            CheckOutcome();
        }

        public void EndPlayerTurn()
        {
            if (State.Phase != CombatPhase.PlayerTurn)
            {
                throw new InvalidOperationException("現在不是玩家回合,無法結束回合");
            }
            FireHook(new HookContext(HookPoint.PlayerTurnEnd, sourceIndex: PlayerIndex));

            // 手牌狀態卡(燒傷型):回合結束仍在手才觸發
            var handSnapshot = new List<CardInstance>(State.Hand);
            for (int i = 0; i < handSnapshot.Count; i++)
            {
                var def = _db.GetCard(handSnapshot[i].ResolvedCardId);
                if (def.TurnEndInHandSteps.Length > 0)
                {
                    EffectResolver.Resolve(this, def.TurnEndInHandSteps, PlayerIndex, -1);
                    FlushDeferredBlocks();
                }
            }
            if (!State.Player.IsAlive)
            {
                CheckOutcome();
                return;
            }

            // 虛無(Ethereal)卡:回合結束在手即消耗;其餘棄整手
            for (int i = State.Hand.Count - 1; i >= 0; i--)
            {
                var card = State.Hand[i];
                var def = _db.GetCard(card.ResolvedCardId);
                State.Hand.RemoveAt(i);
                if (def.Ethereal)
                {
                    ExhaustCard(card);
                }
                else
                {
                    State.DiscardPile.Add(card);
                    Emit(new CombatEvent(EventKind.CardDiscarded, cardId: card.CardId, cardInstanceId: card.InstanceId));
                }
            }

            DecayStatuses(PlayerIndex);
            RunEnemyTurns();
            if (State.Phase == CombatPhase.EnemyTurn)
            {
                BeginPlayerTurn(false);
            }
        }

        /// <summary>純查詢:取卡牌實體對應的定義(UI 顯示用)。</summary>
        public CardDef GetCardDef(CardInstance card)
        {
            return _db.GetCard(card.ResolvedCardId);
        }

        /// <summary>意圖預覽(純查詢):傷害含雙方力量/虛弱/易傷即時重算。</summary>
        public IntentInfo GetIntentPreview(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= State.Enemies.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyIndex));
            }
            var runtime = _enemyRuntimes[enemyIndex];
            var move = runtime.Def.GetMove(runtime.NextMoveId);
            var enemy = State.Enemies[enemyIndex];
            int damage = 0;
            int hits = 0;
            for (int i = 0; i < move.Steps.Length; i++)
            {
                var step = move.Steps[i];
                if (step.Op != EffectOp.Damage) continue;
                damage = CombatMath.CalculateAttackDamage(
                    step.Amount,
                    enemy.GetStatus(StatusId.Strength),
                    enemy.GetStatus(StatusId.Weak) > 0,
                    State.Player.GetStatus(StatusId.Vulnerable) > 0);
                hits = step.RepeatIsX ? 0 : (step.Repeat <= 1 ? 1 : step.Repeat);
                break;
            }
            return new IntentInfo(move.Intent, move.Name, damage, hits);
        }

        private void BeginPlayerTurn(bool isFirstTurn)
        {
            State.TurnNumber++;
            State.Phase = CombatPhase.PlayerTurn;
            State.AttacksPlayedThisTurn = 0;
            // 格擋在「自己回合開始」清除,不是回合結束——回合末獲得的格擋要活過敵方回合(R5)
            // 壁壘:整條清除規則失效,格擋累積不掉
            if (State.Player.Block != 0 && State.Player.GetStatus(StatusId.Barricade) <= 0)
            {
                State.Player.Block = 0;
                Emit(new CombatEvent(EventKind.BlockCleared, sourceIndex: PlayerIndex));
            }
            State.Energy = State.MaxEnergy;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));
            if (isFirstTurn)
            {
                // 開戰 hook 在清格擋之後、抽牌之前(R6:船錨的格擋不能被清掉)
                FireHook(new HookContext(HookPoint.CombatStart));
            }
            Emit(new CombatEvent(EventKind.TurnStarted, sourceIndex: PlayerIndex, amount: State.TurnNumber));
            FireHook(new HookContext(HookPoint.PlayerTurnStart, sourceIndex: PlayerIndex));
            DrawCards(CardsPerTurn);
        }

        private void RunEnemyTurns()
        {
            State.Phase = CombatPhase.EnemyTurn;
            for (int i = 0; i < State.Enemies.Count; i++)
            {
                var enemy = State.Enemies[i];
                if (!enemy.IsAlive) continue;

                // 敵人格擋在牠自己行動開始時清除,與玩家同一條規則
                if (enemy.Block != 0 && enemy.GetStatus(StatusId.Barricade) <= 0)
                {
                    enemy.Block = 0;
                    Emit(new CombatEvent(EventKind.BlockCleared, sourceIndex: i));
                }
                FireHook(new HookContext(HookPoint.EnemyTurnStart, sourceIndex: i));

                var runtime = _enemyRuntimes[i];
                string moveId = runtime.NextMoveId;
                Emit(new CombatEvent(EventKind.EnemyMoveStarted, sourceIndex: i, cardId: moveId));
                EffectResolver.Resolve(this, runtime.Def.GetMove(moveId).Steps, i, PlayerIndex);
                FlushDeferredBlocks();   // 整個敵招結算完才結清延後格擋

                if (!State.Player.IsAlive)
                {
                    CheckOutcome();
                    return;
                }

                FireHook(new HookContext(HookPoint.EnemyTurnEnd, sourceIndex: i));
                DecayStatuses(i);
                EnemyAi.RecordExecuted(runtime, moveId);
                if (enemy.IsAlive)
                {
                    runtime.NextMoveId = EnemyAi.SelectNextMove(this, i, runtime, _rng.EnemyAi);
                    EmitIntentShown(i);
                }
            }
            CheckOutcome();   // 敵人可能在自己回合被反傷打死(青銅鱗片反殺)
        }

        /// <summary>
        /// 回合結束衰減。計時型狀態只在擁有者回合末 -1;「擁有者自己回合中施加」的首次衰減跳過
        /// (JustApplied),對手回合施加的不跳——這是 StS 計時語意的重建([近似] R2,測試鎖定)。
        /// </summary>
        private void DecayStatuses(int ownerIndex)
        {
            var owner = GetCombatant(ownerIndex);
            var snapshot = new List<StatusInstance>(owner.Statuses);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var status = snapshot[i];
                if (status.Stacks <= 0) continue;
                switch (StatusRegistry.GetDecayRule(status.Id))
                {
                    case DecayRule.DecrementAtOwnerTurnEnd:
                        if (status.JustApplied)
                        {
                            status.JustApplied = false;
                        }
                        else
                        {
                            ApplyStatusTo(ownerIndex, status.Id, -1);
                        }
                        break;
                    case DecayRule.RemoveAtOwnerTurnEnd:
                        ApplyStatusTo(ownerIndex, status.Id, -status.Stacks);
                        break;
                }
            }
        }

        private void CheckOutcome()
        {
            if (State.Phase == CombatPhase.Victory || State.Phase == CombatPhase.Defeat) return;

            if (!State.Player.IsAlive)
            {
                State.Phase = CombatPhase.Defeat;
                EmitEnded();
                return;
            }
            for (int i = 0; i < State.Enemies.Count; i++)
            {
                if (State.Enemies[i].IsAlive) return;
            }
            State.Phase = CombatPhase.Victory;
            FireHook(new HookContext(HookPoint.CombatVictory));   // 燃燒之血在 CombatEnded 前結算
            EmitEnded();
        }

        private void EmitEnded()
        {
            if (_endedEmitted) return;
            _endedEmitted = true;
            Emit(new CombatEvent(EventKind.CombatEnded, amount: State.Phase == CombatPhase.Victory ? 1 : 0));
        }

        private void FinishCardPlay()
        {
            FlushDeferredBlocks();   // 整張牌打完才結清捲曲之類的延後格擋
            var card = _pendingPlayedCard;
            var def = _pendingPlayedDef;
            _pendingPlayedCard = null;
            _pendingPlayedDef = null;
            if (card == null) return;

            // 攻擊牌計數在「這張打完之後」才加:計數的語意固定是「本回合已打完的其他攻擊牌」,
            // 結算時與卡面預覽時看到的數字才會是同一個(焚燒型成長卡靠這個對齊)
            if (def.Type == CardType.Attack) State.AttacksPlayedThisTurn++;

            if (def.Type == CardType.Power)
            {
                State.PowersPlayed.Add(card);   // 能力卡不進任何牌堆
            }
            else if (def.Exhausts)
            {
                ExhaustCard(card);
            }
            else
            {
                State.DiscardPile.Add(card);
            }
            CheckOutcome();
        }

        // ---- 供 EffectResolver / Registry 呼叫的內部操作(單一結算路,禁止繞過) ----

        internal RngStream CombatMiscRng => _rng.CombatMisc;
        internal int XEnergySpent => _xEnergySpent;

        internal CombatantState GetCombatant(int index)
        {
            return index == PlayerIndex ? State.Player : State.Enemies[index];
        }

        internal void CollectLivingEnemies(List<int> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < State.Enemies.Count; i++)
            {
                if (State.Enemies[i].IsAlive) buffer.Add(i);
            }
        }

        internal void DealAttackDamage(int sourceIndex, int targetIndex, int baseAmount)
        {
            var source = GetCombatant(sourceIndex);
            var target = GetCombatant(targetIndex);
            if (!source.IsAlive || !target.IsAlive) return;

            int final = CombatMath.CalculateAttackDamage(
                baseAmount,
                source.GetStatus(StatusId.Strength),
                source.GetStatus(StatusId.Weak) > 0,
                target.GetStatus(StatusId.Vulnerable) > 0);
            var result = CombatMath.ResolveAttack(final, target.Block, target.Hp);
            target.Block = result.RemainingBlock;
            target.Hp = result.RemainingHp;

            Emit(new CombatEvent(EventKind.DamageDealt, sourceIndex: sourceIndex, targetIndex: targetIndex,
                amount: final, amount2: result.BlockConsumed, hpLost: result.HpLost,
                remainingBlock: result.RemainingBlock, remainingHp: result.RemainingHp));

            // 攻擊 hook(捲曲/尖刺皮/青銅鱗片);非攻擊傷害(反傷)不觸發,否則反傷互咬無限循環。
            // 順序要點:hook 先跑、AfterHpLoss(死亡判定與守護者換模式)後跑——
            // 否則「這一刀打出來的尖刺皮」會回頭反彈這一刀自己。
            FireHook(new HookContext(HookPoint.AttackDealt, sourceIndex: sourceIndex, targetIndex: targetIndex, amount: result.HpLost));
            FireHook(new HookContext(HookPoint.AttackReceived, sourceIndex: sourceIndex, targetIndex: targetIndex, amount: result.HpLost));
            AfterHpLoss(targetIndex, result.HpLost);
        }

        /// <summary>非攻擊傷害(反傷/尖刺皮):吃格擋,但不吃力量/虛弱/易傷修正,不觸發攻擊 hook。</summary>
        internal void DealNonAttackDamage(int sourceIndex, int targetIndex, int amount)
        {
            var target = GetCombatant(targetIndex);
            if (!target.IsAlive || amount <= 0) return;

            var result = CombatMath.ResolveAttack(amount, target.Block, target.Hp);
            target.Block = result.RemainingBlock;
            target.Hp = result.RemainingHp;
            Emit(new CombatEvent(EventKind.DamageDealt, sourceIndex: sourceIndex, targetIndex: targetIndex,
                amount: amount, amount2: result.BlockConsumed, hpLost: result.HpLost,
                remainingBlock: result.RemainingBlock, remainingHp: result.RemainingHp));
            AfterHpLoss(targetIndex, result.HpLost);
        }

        /// <summary>直接失血(燒傷型):無視格擋。</summary>
        internal void LoseHpDirect(int index, int amount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive || amount <= 0) return;
            int loss = amount > combatant.Hp ? combatant.Hp : amount;
            combatant.Hp -= loss;
            Emit(new CombatEvent(EventKind.HpLost, targetIndex: index, amount: loss, remainingHp: combatant.Hp));
            AfterHpLoss(index, loss);
        }

        internal void HealHp(int index, int amount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive || amount <= 0) return;
            int healed = combatant.Hp + amount > combatant.MaxHp ? combatant.MaxHp - combatant.Hp : amount;
            if (healed <= 0) return;
            combatant.Hp += healed;
            Emit(new CombatEvent(EventKind.HpHealed, targetIndex: index, amount: healed, remainingHp: combatant.Hp));
        }

        private void AfterHpLoss(int index, int hpLost)
        {
            if (hpLost <= 0) return;
            // 掉血 hook 對玩家與敵人都要觸發(撕裂/獄火靠它);敵人專屬的死亡與換模式邏輯在下面
            FireHook(new HookContext(HookPoint.HpLost, targetIndex: index, amount: hpLost));
            if (index == PlayerIndex) return;
            var target = GetCombatant(index);
            if (!target.IsAlive)
            {
                Emit(new CombatEvent(EventKind.EnemyDied, targetIndex: index));
                FireHook(new HookContext(HookPoint.EnemyDied, targetIndex: index));
            }
            var runtime = _enemyRuntimes[index];
            if (runtime.Def.Ai == AiKind.Custom && target.IsAlive)
            {
                GuardianAi.OnDamaged(this, index, runtime, hpLost);
            }
        }

        internal void GainBlock(int index, int baseAmount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive) return;
            int gain = BlockMath.CalculateBlockGain(
                baseAmount,
                combatant.GetStatus(StatusId.Dexterity),
                combatant.GetStatus(StatusId.Frail) > 0);
            combatant.Block += gain;
            Emit(new CombatEvent(EventKind.BlockGained, sourceIndex: index, amount: gain, remainingBlock: combatant.Block));
            // 勢不可當靠這個 hook;它打的是非攻擊傷害,所以不會和尖刺皮互相觸發成迴圈
            FireHook(new HookContext(HookPoint.BlockGained, sourceIndex: index, amount: gain));
        }

        /// <summary>是否輪到該單位的回合(狀態行為判定用)。</summary>
        internal bool IsOwnersTurn(int index)
        {
            return IsOwnTurnNow(index);
        }

        /// <summary>
        /// 數本場戰鬥中名字含指定字串的牌(完美打擊型)。
        /// 四個牌堆加已打出的能力卡都算——牌會在牌堆間流動,只數手牌會讓數字忽高忽低。
        /// </summary>
        internal int CountCardsNamedContaining(string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return 0;
            int count = 0;
            count += CountIn(State.DrawPile, fragment);
            count += CountIn(State.Hand, fragment);
            count += CountIn(State.DiscardPile, fragment);
            count += CountIn(State.ExhaustPile, fragment);
            count += CountIn(State.PowersPlayed, fragment);
            // 正在結算的這張牌已離開手牌、還沒進棄牌堆,不補上就會少數自己一張
            if (_pendingPlayedDef != null && _pendingPlayedDef.Name != null
                && _pendingPlayedDef.Name.Contains(fragment))
            {
                count++;
            }
            return count;
        }

        private int CountIn(List<CardInstance> pile, string fragment)
        {
            int count = 0;
            for (int i = 0; i < pile.Count; i++)
            {
                var def = _db.GetCard(pile[i].ResolvedCardId);
                if (def.Name != null && def.Name.Contains(fragment)) count++;
            }
            return count;
        }

        /// <summary>
        /// 成長型數值的「數量」來源。結算與卡面預覽都走這裡——分兩份實作,兩邊就會漂掉
        /// (卡面說 6 點、實際打 12 點這種 bug 已經付過一次代價)。
        /// targetVulnerable:目標身上的易傷層數,沒有目標時傳 0。
        /// </summary>
        internal int ScalingCount(AmountKind kind, int targetVulnerable)
        {
            switch (kind)
            {
                case AmountKind.PerExhaustedCard:
                    return State.ExhaustPile.Count;
                case AmountKind.PerTargetVulnerable:
                    return targetVulnerable;
                case AmountKind.PerAttackPlayedThisTurn:
                    return State.AttacksPlayedThisTurn;
                case AmountKind.PerStrikeCard:
                    return CountCardsNamedContaining("打擊");
                case AmountKind.PerLastExhausted:
                    return State.LastExhaustedCount;
                default:
                    return 0;
            }
        }

        /// <summary>該 AmountKind 是不是 Amount + Secondary × 數量 的成長型。</summary>
        internal static bool IsScalingKind(AmountKind kind)
        {
            return kind == AmountKind.PerExhaustedCard
                || kind == AmountKind.PerTargetVulnerable
                || kind == AmountKind.PerAttackPlayedThisTurn
                || kind == AmountKind.PerStrikeCard
                || kind == AmountKind.PerLastExhausted;
        }

        /// <summary>隨機挑一個活著的敵人;全滅回 -1。</summary>
        internal int PickRandomLivingEnemy()
        {
            var living = new List<int>(4);
            CollectLivingEnemies(living);
            if (living.Count == 0) return -1;
            return living[_rng.CombatMisc.NextInt(living.Count)];
        }

        /// <summary>對所有活著的敵人造成非攻擊傷害(獄火用)。</summary>
        internal void DamageAllEnemiesNonAttack(int sourceIndex, int amount)
        {
            if (amount <= 0) return;
            var living = new List<int>(4);
            CollectLivingEnemies(living);
            for (int i = 0; i < living.Count; i++)
            {
                DealNonAttackDamage(sourceIndex, living[i], amount);
            }
        }

        /// <summary>
        /// 登記一筆延後格擋:等當前這張牌/藥水/敵招整個結算完才實際獲得。
        /// 用意是讓多段攻擊的每一段都打在同樣的防禦狀態上,不會被自己觸發的盾吃掉後段傷害。
        /// </summary>
        internal void DeferBlock(int index, int amount)
        {
            if (amount <= 0) return;
            _deferredBlocks.Add(new DeferredBlock(index, amount));
        }

        /// <summary>結清延後格擋;期間死掉的單位不再上盾。</summary>
        private void FlushDeferredBlocks()
        {
            if (_deferredBlocks.Count == 0) return;
            var pending = new List<DeferredBlock>(_deferredBlocks);
            _deferredBlocks.Clear();
            foreach (var entry in pending)
            {
                if (GetCombatant(entry.Index).IsAlive)
                {
                    GainBlock(entry.Index, entry.Amount);
                }
            }
        }

        /// <summary>不吃敏捷/脆弱的原始格擋(藥水用)。</summary>
        internal void GainBlockRaw(int index, int amount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive || amount <= 0) return;
            combatant.Block += amount;
            Emit(new CombatEvent(EventKind.BlockGained, sourceIndex: index, amount: amount, remainingBlock: combatant.Block));
        }

        internal void ApplyStatusTo(int index, StatusId status, int amount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive || amount == 0) return;
            bool isNew = combatant.GetStatus(status) == 0 && amount > 0;
            int stacks = combatant.ModifyStatus(status, amount);
            if (isNew)
            {
                var instance = combatant.GetStatusInstance(status);
                if (instance != null)
                {
                    instance.JustApplied = IsOwnTurnNow(index);
                }
            }
            Emit(new CombatEvent(EventKind.StatusChanged, targetIndex: index, amount: amount, amount2: stacks, status: status));
        }

        /// <summary>是否在該單位「自己的回合」中(JustApplied 判定用)。</summary>
        private bool IsOwnTurnNow(int index)
        {
            if (index == PlayerIndex)
            {
                return State.Phase == CombatPhase.PlayerTurn || State.Phase == CombatPhase.AwaitingChoice;
            }
            return State.Phase == CombatPhase.EnemyTurn;   // [近似] 任一敵人的回合都算敵方自己的回合
        }

        internal void DrawCards(int count)
        {
            if (State.Player.GetStatus(StatusId.NoDraw) > 0) return;   // 戰吼型:本回合不能再抽
            for (int k = 0; k < count; k++)
            {
                // 手牌滿:取消剩餘抽牌,卡留在抽牌堆([近似] R11,以測試鎖定此語意,校正期對照 wiki)
                if (State.Hand.Count >= CombatState.HandLimit) break;

                if (State.DrawPile.Count == 0)
                {
                    if (State.DiscardPile.Count == 0) break;
                    State.DrawPile.AddRange(State.DiscardPile);
                    State.DiscardPile.Clear();
                    _rng.Shuffle.Shuffle(State.DrawPile);
                    Emit(new CombatEvent(EventKind.PileShuffled, amount: State.DrawPile.Count));
                    FireHook(new HookContext(HookPoint.Shuffled));
                }

                int top = State.DrawPile.Count - 1;   // 尾端當堆頂:移除 O(1)
                var card = State.DrawPile[top];
                State.DrawPile.RemoveAt(top);
                State.Hand.Add(card);
                Emit(new CombatEvent(EventKind.CardDrawn, cardId: card.CardId, cardInstanceId: card.InstanceId));
            }
        }

        internal void GainEnergy(int amount)
        {
            if (amount == 0) return;
            State.Energy += amount;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));
        }

        /// <summary>生成新卡加入牌堆。手牌滿時落入棄牌堆;加入抽牌堆時插入隨機位置([近似])。</summary>
        internal void AddCardToPile(string cardId, PileType pile)
        {
            var card = new CardInstance(_nextInstanceId++, cardId);
            switch (pile)
            {
                case PileType.Hand:
                    if (State.Hand.Count >= CombatState.HandLimit)
                    {
                        State.DiscardPile.Add(card);
                        pile = PileType.Discard;
                    }
                    else
                    {
                        State.Hand.Add(card);
                    }
                    break;
                case PileType.Draw:
                    State.DrawPile.Insert(_rng.CombatMisc.NextInt(State.DrawPile.Count + 1), card);
                    break;
                case PileType.Exhaust:
                    State.ExhaustPile.Add(card);
                    break;
                default:
                    State.DiscardPile.Add(card);
                    break;
            }
            Emit(new CombatEvent(EventKind.CardAddedToPile, amount: (int)pile, cardId: card.CardId, cardInstanceId: card.InstanceId));
        }

        internal void ExhaustRandomFromHand(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (State.Hand.Count == 0) return;
                int index = _rng.CombatMisc.NextInt(State.Hand.Count);
                var card = State.Hand[index];
                State.Hand.RemoveAt(index);
                ExhaustCard(card);
            }
        }

        /// <summary>
        /// 把一張已從原牌堆取出的卡放進消耗堆。所有消耗都要走這裡——
        /// 無懼疼痛/黑暗之擁靠 CardExhausted hook,少一條路徑就會少觸發。
        /// </summary>
        internal void ExhaustCard(CardInstance card)
        {
            State.ExhaustPile.Add(card);
            Emit(new CombatEvent(EventKind.CardExhausted, cardId: card.CardId, cardInstanceId: card.InstanceId));
            FireHook(new HookContext(HookPoint.CardExhausted, sourceIndex: PlayerIndex));
        }

        /// <summary>
        /// 消耗整手牌(nonAttacksOnly = true 時只消耗非攻擊牌),回傳消耗張數並記進 LastExhaustedCount。
        /// 先把要消耗的牌整批抽離手牌再逐張入消耗堆——消耗 hook(黑暗之擁)會抽新牌進手裡,
        /// 邊走邊改集合的話新抽的牌會被一起消耗掉。
        /// </summary>
        internal int ExhaustHand(bool nonAttacksOnly)
        {
            var taken = new List<CardInstance>();
            for (int i = State.Hand.Count - 1; i >= 0; i--)
            {
                var card = State.Hand[i];
                if (nonAttacksOnly && _db.GetCard(card.ResolvedCardId).Type == CardType.Attack) continue;
                State.Hand.RemoveAt(i);
                taken.Add(card);
            }
            for (int i = 0; i < taken.Count; i++)
            {
                ExhaustCard(taken[i]);
            }
            State.LastExhaustedCount = taken.Count;
            return taken.Count;
        }

        /// <summary>棄掉整手牌,再抽等量的牌(添柴)。</summary>
        internal void DiscardHandDrawSame()
        {
            int count = State.Hand.Count;
            if (count == 0) return;
            for (int i = State.Hand.Count - 1; i >= 0; i--)
            {
                var card = State.Hand[i];
                State.Hand.RemoveAt(i);
                State.DiscardPile.Add(card);
                Emit(new CombatEvent(EventKind.CardDiscarded, cardId: card.CardId, cardInstanceId: card.InstanceId));
            }
            DrawCards(count);
        }

        /// <summary>一直抽到抽出一張非攻擊牌為止(劫掠);牌堆抽乾或手牌滿就停。</summary>
        internal void DrawUntilNonAttack()
        {
            const int 保險上限 = 20;   // 全是攻擊牌的牌組不該把這裡變成無窮迴圈
            for (int i = 0; i < 保險上限; i++)
            {
                int before = State.Hand.Count;
                DrawCards(1);
                if (State.Hand.Count == before) return;   // 抽不動了(牌堆空/手牌滿/禁抽)
                var drawn = State.Hand[State.Hand.Count - 1];
                if (_db.GetCard(drawn.ResolvedCardId).Type != CardType.Attack) return;
            }
        }

        /// <summary>消耗抽牌堆最上面 N 張(餘燼/戰鼓);抽牌堆空了就停,不重洗。</summary>
        internal void ExhaustTopOfDraw(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (State.DrawPile.Count == 0) return;
                int top = State.DrawPile.Count - 1;
                var card = State.DrawPile[top];
                State.DrawPile.RemoveAt(top);
                ExhaustCard(card);
            }
        }

        internal void RequestChoice(EffectStep[] steps, int resumeIndex, int sourceIndex, int targetIndex, int count,
            bool ignoreModifiers = false)
        {
            _pendingSteps = steps;
            _pendingResumeIndex = resumeIndex;
            _pendingSourceIndex = sourceIndex;
            _pendingTargetIndex = targetIndex;
            _pendingIgnoreModifiers = ignoreModifiers;
            State.PendingChoiceCount = count;
            State.Phase = CombatPhase.AwaitingChoice;
            Emit(new CombatEvent(EventKind.ChoiceRequired, amount: count));
        }

        internal void EmitIntentShown(int enemyIndex)
        {
            Emit(new CombatEvent(EventKind.IntentShown, sourceIndex: enemyIndex, cardId: _enemyRuntimes[enemyIndex].NextMoveId));
        }

        internal void FireHook(in HookContext ctx)
        {
            HookBus.Fire(this, ctx);
        }

        private static bool RequiresEnemyTarget(CardDef def)
        {
            for (int i = 0; i < def.Steps.Length; i++)
            {
                if (def.Steps[i].Target == EffectTarget.TargetEnemy) return true;
            }
            return false;
        }

        private void Emit(in CombatEvent combatEvent)
        {
            Events.Add(combatEvent);
        }
    }
}
