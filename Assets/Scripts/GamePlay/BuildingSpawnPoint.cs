using UnityEngine;

namespace ReverseDefense
{
    // Attach to an existing building, then register it in GamePlay's Buildings list.
    public sealed class BuildingSpawnPoint : MonoBehaviour
    {
        [Tooltip("刷到 Road Nodes 的第几项，从0开始。旧网格路线也使用行走顺序编号。")]
        [Min(0)] public int RoadIndex = 1;
        [Tooltip("相同名称的路段共用容量。第一版只有一条路时全部填 main。")]
        public string RoadSegment = "main";
        [Min(0.01f)] public float IntervalDays = 1;
        [Min(1)] public int MonsterHealth = 8;
        [Min(1)] public int MonsterAttack = 2;

        public MonsterSpawnClock CreateClock(float secondsPerDay)
        {
            return new MonsterSpawnClock(RoadIndex, RoadSegment, IntervalDays * secondsPerDay,
                MonsterHealth, MonsterAttack);
        }
    }
}
