﻿using System.Collections.Generic;

using pdxpartyparrot.Core.Actors;
using pdxpartyparrot.Core.DebugMenu;
using pdxpartyparrot.Core.Time;
using pdxpartyparrot.Core.Util;
using pdxpartyparrot.Core.World;
using pdxpartyparrot.Game.Characters.NPCs;
using pdxpartyparrot.Game.State;

using UnityEngine;

namespace pdxpartyparrot.Game.NPCs
{
    public interface INPCManager
    {
        bool NPCsImmune { get; }

        bool DebugBehavior { get; }

        float StuckCheckSeconds { get; }

        int StuckCheckMaxPasses { get; }

        void RegisterNPC(INPC npc);

        void UnregisterNPC(INPC npc);

        void DespawnAllNPCs();
    }

    public abstract class NPCManager<T> : SingletonBehavior<T>, INPCManager where T : NPCManager<T>
    {
        #region Debug

        [SerializeField]
        private bool _npcsImmune;

        public bool NPCsImmune => _npcsImmune;

        [SerializeField]
        private bool _debugNPCBehavior;

        public bool DebugBehavior => _debugNPCBehavior;

        #endregion

        [Space(10)]

        #region Stuck Check

        [Header("Stuck Check")]

        [SerializeField]
        private int _stuckCheckMs = 500;

        public float StuckCheckSeconds => _stuckCheckMs * TimeManager.MilliSecondsToSeconds;

        [SerializeField]
        [Tooltip("How many passes through the stuck check should we fail before being considered stuck")]
        private int _stuckCheckMaxPasses = 2;

        public int StuckCheckMaxPasses => _stuckCheckMaxPasses;

        #endregion

        private readonly HashSet<INPC> _npcs = new HashSet<INPC>();

        public IReadOnlyCollection<INPC> NPCs => _npcs;

        private DebugMenuNode _debugMenuNode;

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            InitDebugMenu();

            GameStateManager.Instance.RegisterNPCManager(this);
        }

        protected override void OnDestroy()
        {
            if(GameStateManager.HasInstance) {
                GameStateManager.Instance.UnregisterNPCManager();
            }

            DestroyDebugMenu();

            base.OnDestroy();
        }

        #endregion

        public virtual void RegisterNPC(INPC npc)
        {
            if(_npcs.Add(npc)) {
                Debug.Log($"Registered NPC {npc.Id}");
            }
        }

        public virtual void UnregisterNPC(INPC npc)
        {
            if(_npcs.Remove(npc)) {
                Debug.Log($"Unregistered NPC {npc.Id}");
            }
        }

        public bool RespawnNPC(INPC npc, string tag)
        {
            Debug.Log($"Respawning NPC {npc.Id}");

            SpawnPoint spawnPoint = SpawnManager.Instance.GetSpawnPoint(tag);
            if(null == spawnPoint) {
                Debug.LogError("Failed to get NPC spawnpoint!");
                return false;
            }

            return spawnPoint.ReSpawn((Actor)npc);
        }

        public void DespawnAllNPCs()
        {
            Debug.Log($"Despawning {_npcs.Count} NPCs...");

            INPC[] npcs = new INPC[_npcs.Count];
            _npcs.CopyTo(npcs);

            foreach(INPC npc in npcs) {
                npc.DeSpawn(true);
            }
        }

        #region Debug Menu

        private void InitDebugMenu()
        {
            _debugMenuNode = DebugMenuManager.Instance.AddNode(() => "Game.NPCManager");
            _debugMenuNode.RenderContentsAction = () => {
                GUILayout.BeginVertical("NPCs", GUI.skin.box);
                foreach(INPC npc in _npcs) {
                    GUILayout.Label($"{npc.Id} {npc.Movement.Position}");
                }
                GUILayout.EndVertical();

                _npcsImmune = GUILayout.Toggle(_npcsImmune, "NPCs Immune");
                _debugNPCBehavior = GUILayout.Toggle(_debugNPCBehavior, "Debug Behavior");
            };
        }

        private void DestroyDebugMenu()
        {
            if(DebugMenuManager.HasInstance) {
                DebugMenuManager.Instance.RemoveNode(_debugMenuNode);
            }
            _debugMenuNode = null;
        }

        #endregion
    }
}
