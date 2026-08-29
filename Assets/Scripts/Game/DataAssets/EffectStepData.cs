using System;
using UnityEngine;
using STS.Core.Cards;
using STS.Core.Combat.Enemies;
using STS.Core.Combat.Statuses;

namespace STS.Game.DataAssets
{
    /// <summary>EffectStep 的可序列化鏡像——SO 上給設計師看與微調,轉出純 def 給引擎。</summary>
    [Serializable]
    public sealed class EffectStepData
    {
        [Tooltip("效果操作種類")] public EffectOp op;
        [Tooltip("目標(相對於施放者)")] public EffectTarget target = EffectTarget.Self;
        [Tooltip("主數值(傷害/格擋/層數/張數)")] public int amount;
        [Tooltip("主數值的解讀方式(固定/X能量/當前格擋/力量倍計)")] public AmountKind amountKind = AmountKind.Fixed;
        [Tooltip("副數值(力量倍率/複製張數)")] public int secondaryAmount;
        [Tooltip("段數;0 或 1 = 單次")] public int repeat;
        [Tooltip("段數的算法:X 費卡吃消耗的能量,失血成長型加上本場失血次數")] public RepeatKind repeatKind = RepeatKind.Fixed;
        [Tooltip("這一步的執行條件;不成立就整步跳過,後面的步驟照跑")] public StepCondition condition = StepCondition.None;
        [Tooltip("ApplyStatus 的狀態種類")] public StatusId status = StatusId.None;
        [Tooltip("AddCardToPile 的卡牌 id")] public string cardId;
        [Tooltip("AddCardToPile 的目標牌堆")] public PileType pile = PileType.Discard;
        [Tooltip("Custom 逃生門的識別 id")] public string customId;

        public EffectStep ToStep()
        {
            return new EffectStep(op, target, amount, amountKind, secondaryAmount, repeat, repeatKind, status,
                string.IsNullOrEmpty(cardId) ? null : cardId, pile,
                string.IsNullOrEmpty(customId) ? null : customId, condition);
        }

        public static EffectStep[] ToSteps(EffectStepData[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<EffectStep>();
            var steps = new EffectStep[data.Length];
            for (int i = 0; i < data.Length; i++) steps[i] = data[i].ToStep();
            return steps;
        }
    }

    /// <summary>初始狀態(層數)的可序列化鏡像。</summary>
    [Serializable]
    public sealed class StatusStackData
    {
        [Tooltip("狀態種類")] public StatusId status;
        [Tooltip("層數")] public int stacks;
    }

    /// <summary>敵人招式的可序列化鏡像。</summary>
    [Serializable]
    public sealed class MoveData
    {
        [Tooltip("招式 id(開場/循環腳本以此參照)")] public string id;
        [Tooltip("招式名稱(意圖顯示用)")] public string moveName;
        [Tooltip("意圖圖示類型")] public IntentType intent = IntentType.Special;
        [Tooltip("加權 AI 的權重;0 = 不參與選招(僅開場腳本用)")] public int weight = 1;
        [Tooltip("連續使用上限;0 = 無限制")] public int maxConsecutive;
        [Tooltip("效果步驟(與卡牌共用同一套結算)")] public EffectStepData[] steps = Array.Empty<EffectStepData>();

        public MoveDef ToDef()
        {
            return new MoveDef
            {
                Id = id,
                Name = moveName,
                Intent = intent,
                Weight = weight,
                MaxConsecutive = maxConsecutive,
                Steps = EffectStepData.ToSteps(steps)
            };
        }
    }
}
