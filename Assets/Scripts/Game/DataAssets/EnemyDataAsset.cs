using UnityEngine;
using STS.Core.Combat.Enemies;

namespace STS.Game.DataAssets
{
    /// <summary>敵人資料資產。單一真相在 enemies.json,本資產由匯入器生成/就地更新。</summary>
    [CreateAssetMenu(menuName = "STS/敵人定義", fileName = "Enemy")]
    public sealed class EnemyDataAsset : ScriptableObject
    {
        [Tooltip("敵人 id(遭遇表以此參照)")] public string id;
        [Tooltip("顯示名稱")] public string enemyName;
        [Tooltip("血量下限(開戰時擲)")] public int hpMin;
        [Tooltip("血量上限")] public int hpMax;
        [Tooltip("AI 種類:Loop 循環/Weighted 加權/Custom 程式註冊(守護者)")] public AiKind ai = AiKind.Loop;
        [Tooltip("招式庫")] public MoveData[] moves = System.Array.Empty<MoveData>();
        [Tooltip("開場固定招(依序,以招式 id 參照)")] public string[] openingScript = System.Array.Empty<string>();
        [Tooltip("Loop AI 的循環序;空 = 循環整個招式庫")] public string[] loopScript = System.Array.Empty<string>();
        [Tooltip("開戰時自帶的狀態(蝨子的捲曲等)")] public StatusStackData[] initialStatuses = System.Array.Empty<StatusStackData>();

        public EnemyDef ToDef()
        {
            var moveDefs = new MoveDef[moves.Length];
            for (int i = 0; i < moves.Length; i++) moveDefs[i] = moves[i].ToDef();
            var initial = new StatusStack[initialStatuses.Length];
            for (int i = 0; i < initialStatuses.Length; i++)
            {
                initial[i] = new StatusStack(initialStatuses[i].status, initialStatuses[i].stacks);
            }
            return new EnemyDef
            {
                Id = id,
                Name = enemyName,
                HpMin = hpMin,
                HpMax = hpMax,
                Moves = moveDefs,
                OpeningScript = openingScript,
                LoopScript = loopScript,
                Ai = ai,
                InitialStatuses = initial
            };
        }
    }
}
