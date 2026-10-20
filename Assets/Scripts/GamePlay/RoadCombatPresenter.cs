using System;
using System.Collections.Generic;
using UnityEngine;

namespace ReverseDefense
{
    // Owns monster instances and damage feedback, without creating any UI.
    public sealed class RoadCombatPresenter : IDisposable
    {
        private readonly PatrolSession session;
        private readonly GameObject prefab;
        private readonly RoadActorView hero;
        private readonly RectTransform root;
        private readonly GridMapView map;
        private readonly List<Cell> path;
        private readonly Dictionary<RoadMonster, RoadActorView> views = new Dictionary<RoadMonster, RoadActorView>();

        public RoadCombatPresenter(PatrolSession session, GameObject prefab,
            RectTransform root, GridMapView map, List<Cell> path)
        {
            this.session = session;
            this.prefab = prefab;
            this.root = root;
            this.map = map;
            this.path = path;
            hero = map.Player.GetComponent<RoadActorView>();
            session.Spawned += Spawn;
            session.Defeated += Remove;
            session.Battle.Damaged += Damage;
        }

        private void Spawn(RoadMonster monster)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, root, false);
            var view = instance.GetComponent<RoadActorView>();
            if (view == null) view = instance.AddComponent<RoadActorView>();
            instance.SetActive(true);
            view.Rect.position = map.RoadPosition(monster.RoadIndex, path);
            views.Add(monster, view);
        }

        private void Remove(RoadMonster monster)
        {
            if (!views.TryGetValue(monster, out var view)) return;
            views.Remove(monster);
            if (view != null) UnityEngine.Object.Destroy(view.gameObject);
        }

        private void Damage(CombatUnit unit)
        {
            if (unit == session.Hero) { if (hero != null) hero.ShowDamage(); return; }
            foreach (var pair in views)
                if (pair.Key.Unit == unit && pair.Value != null) pair.Value.ShowDamage();
        }

        public void Refresh(float seconds)
        {
            bool frozen = session.Paused || !session.Rounds.Running;
            if (hero != null) { hero.Render(session.Hero, seconds); hero.SetPaused(frozen); }
            foreach (var pair in views)
            {
                if (pair.Value == null) continue;
                pair.Value.Rect.position = map.RoadPosition(pair.Key.RoadIndex, path);
                pair.Value.Render(pair.Key.Unit, seconds);
                pair.Value.SetPaused(frozen);
            }
        }

        public void Dispose()
        {
            session.Spawned -= Spawn;
            session.Defeated -= Remove;
            session.Battle.Damaged -= Damage;
            foreach (var view in views.Values)
                if (view != null) UnityEngine.Object.Destroy(view.gameObject);
            views.Clear();
            if (hero != null) hero.SetPaused(false);
        }
    }
}
