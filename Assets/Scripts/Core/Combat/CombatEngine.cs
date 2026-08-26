using System;
using System.Collections.Generic;
using STS.Core.Cards;
using STS.Core.Combat.Statuses;
using STS.Core.Content;
using STS.Core.Rng;

namespace STS.Core.Combat
{
    /// <summary>
    /// 戰鬥引擎:指令進、事件序列出。每個指令在呼叫內同步結算完畢;
    /// UI 取走 Events 照序播放動畫,期間不回查引擎。
    /// </summary>
    public sealed class CombatEngine
    {
        /// <summary>SourceIndex/TargetIndex 中代表玩家的值。</summary>
        public const int PlayerIndex = -1;
        private const int CardsPerTurn = 5;

        public readonly CombatState State = new CombatState();
        /// <summary>累積事件;UI 播放完呼叫 ClearEvents。</summary>
        public readonly List<CombatEvent> Events = new List<CombatEvent>();

        private readonly IContentDb _db;
        private readonly RunRng _rng;
        private readonly List<EnemySetup> _enemySetups = new List<EnemySetup>();
        private bool _endedEmitted;

        public CombatEngine(IContentDb db, RunRng rng, CombatSetup setup)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (setup == null) throw new ArgumentNullException(nameof(setup));

            State.MaxEnergy = setup.MaxEnergy;
            State.Player = new CombatantState { Name = "玩家", Hp = setup.PlayerHp, MaxHp = setup.PlayerMaxHp };
            foreach (var enemySetup in setup.Enemies)
            {
                _enemySetups.Add(enemySetup);
                State.Enemies.Add(new CombatantState { Name = enemySetup.Name, Hp = enemySetup.Hp, MaxHp = enemySetup.Hp });
            }
            State.DrawPile.AddRange(setup.Deck);
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
            BeginPlayerTurn();
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
            if (State.Energy < def.Cost)
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

            State.Energy -= def.Cost;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));

            State.Hand.RemoveAt(handIndex);
            Emit(new CombatEvent(EventKind.CardPlayed, sourceIndex: PlayerIndex, targetIndex: targetEnemyIndex,
                cardId: card.CardId, cardInstanceId: card.InstanceId));

            EffectResolver.Resolve(this, def.Steps, PlayerIndex, targetEnemyIndex);

            if (def.Exhausts)
            {
                State.ExhaustPile.Add(card);
                Emit(new CombatEvent(EventKind.CardExhausted, cardId: card.CardId, cardInstanceId: card.InstanceId));
            }
            else
            {
                State.DiscardPile.Add(card);
            }
            CheckOutcome();
        }

        public void EndPlayerTurn()
        {
            if (State.Phase != CombatPhase.PlayerTurn)
            {
                throw new InvalidOperationException("現在不是玩家回合,無法結束回合");
            }
            // 棄整手(M2 在此之前插入 PlayerTurnEnd hook、手牌狀態卡結算與 Ethereal 消耗)
            for (int i = State.Hand.Count - 1; i >= 0; i--)
            {
                var card = State.Hand[i];
                State.Hand.RemoveAt(i);
                State.DiscardPile.Add(card);
                Emit(new CombatEvent(EventKind.CardDiscarded, cardId: card.CardId, cardInstanceId: card.InstanceId));
            }
            RunEnemyTurns();
            if (State.Phase == CombatPhase.EnemyTurn)
            {
                BeginPlayerTurn();
            }
        }

        private void BeginPlayerTurn()
        {
            State.TurnNumber++;
            State.Phase = CombatPhase.PlayerTurn;
            // 格擋在「自己回合開始」清除,不是回合結束——回合末獲得的格擋要活過敵方回合(R5)
            if (State.Player.Block != 0)
            {
                State.Player.Block = 0;
                Emit(new CombatEvent(EventKind.BlockCleared, sourceIndex: PlayerIndex));
            }
            State.Energy = State.MaxEnergy;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));
            Emit(new CombatEvent(EventKind.TurnStarted, sourceIndex: PlayerIndex, amount: State.TurnNumber));
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
                if (enemy.Block != 0)
                {
                    enemy.Block = 0;
                    Emit(new CombatEvent(EventKind.BlockCleared, sourceIndex: i));
                }
                Emit(new CombatEvent(EventKind.EnemyMoveStarted, sourceIndex: i));
                EffectResolver.Resolve(this, _enemySetups[i].MoveSteps, i, PlayerIndex);

                if (!State.Player.IsAlive)
                {
                    CheckOutcome();
                    return;
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
            EmitEnded();
        }

        private void EmitEnded()
        {
            if (_endedEmitted) return;
            _endedEmitted = true;
            Emit(new CombatEvent(EventKind.CombatEnded, amount: State.Phase == CombatPhase.Victory ? 1 : 0));
        }

        // ---- 供 EffectResolver 呼叫的內部操作(單一結算路,禁止繞過) ----

        internal RngStream CombatMiscRng => _rng.CombatMisc;

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

            if (!target.IsAlive && targetIndex != PlayerIndex)
            {
                Emit(new CombatEvent(EventKind.EnemyDied, targetIndex: targetIndex));
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
        }

        internal void ApplyStatusTo(int index, StatusId status, int amount)
        {
            var combatant = GetCombatant(index);
            if (!combatant.IsAlive) return;
            int stacks = combatant.ModifyStatus(status, amount);
            Emit(new CombatEvent(EventKind.StatusChanged, targetIndex: index, amount: amount, amount2: stacks, status: status));
        }

        internal void DrawCards(int count)
        {
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
            State.Energy += amount;
            Emit(new CombatEvent(EventKind.EnergyChanged, amount: State.Energy, amount2: State.MaxEnergy));
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
