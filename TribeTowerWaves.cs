using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TribeTowerWaves
{
    [BepInPlugin("com.btw.tribetower.waves", "TribeTower Waves", "0.4.1")]
    public class TribeTowerWavesPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private static TribeTowerWavesPlugin Instance;

        // ---- config ----
        private static ConfigEntry<bool> cfgEnabled;
        private static ConfigEntry<bool> cfgLogDiagnostics;
        private static ConfigEntry<string> cfgWave1, cfgWave2, cfgWave3, cfgWave4,
            cfgWave5, cfgWave6, cfgWave7, cfgWave8, cfgWave9, cfgWave10;

        // ---- reflection ----
        private static Type _battleSceneType;
        private static Type _battleWaveType;
        private static Type _persistentBoolItemType;
        private static Type _enemyDeathEffectsType;

        private static FieldInfo _wavesField;

        private static FieldInfo _bwBattleSceneField;
        private static FieldInfo _bwStartDelayField;
        private static FieldInfo _bwActivateEnemiesOnStartField;
        private static FieldInfo _bwClearDeathDropsField;
        private static FieldInfo _bwStartWaveEventRegisterField;

        private static FieldInfo _setPlayerDataBoolField;

        // ---- data maps ----
        private static readonly Dictionary<string, string> EnemyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hunter", "Bone Hunter" },
            { "Child",  "Bone Hunter Child" },
            { "Fly",    "Bone Hunter Fly" },
            { "Chief",  "Bone Hunter Fly Chief" },
        };

        private static readonly Dictionary<string, Vector3> Positions =
            new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
        {
            { "FL", new Vector3(135f, 20.5f, 0f) },
            { "L",  new Vector3(141f, 20.5f, 0f) },
            { "C",  new Vector3(148f, 20.5f, 0f) },
            { "R",  new Vector3(155f, 20.5f, 0f) },
            { "FR", new Vector3(161f, 20.5f, 0f) },
            { "UL", new Vector3(141f, 25.5f, 0f) },
            { "UC", new Vector3(148f, 25.5f, 0f) },
            { "UR", new Vector3(155f, 25.5f, 0f) },
        };

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            cfgEnabled = Config.Bind("General", "Enabled", true, "Master toggle.");
            cfgLogDiagnostics = Config.Bind("General", "LogDiagnostics", true,
                "Log details about wave construction.");

            const string waveDesc =
                "Comma-separated Enemy@Pos entries. Empty = leave vanilla untouched (waves 1-3 always protected). " +
                "Enemies: Hunter, Child, Fly, Chief. " +
                "Positions: FL, L, C, R, FR (ground); UL, UC, UR (air).";

            cfgWave1 = Config.Bind("Waves", "Wave1", "", waveDesc);
            cfgWave2 = Config.Bind("Waves", "Wave2", "", waveDesc);
            cfgWave3 = Config.Bind("Waves", "Wave3", "", waveDesc);
            cfgWave4 = Config.Bind("Waves", "Wave4", "Chief@C, Fly@L, Fly@R", waveDesc);
            cfgWave5 = Config.Bind("Waves", "Wave5", "Hunter@L, Child@R, Fly@FL, Fly@FR", waveDesc);
            cfgWave6 = Config.Bind("Waves", "Wave6", "Chief@FL, Chief@FR, Fly@L, Fly@R", waveDesc);
            cfgWave7 = Config.Bind("Waves", "Wave7", "Chief@UL, Chief@UR, Fly@UC, Child@L, Child@R", waveDesc);
            cfgWave8 = Config.Bind("Waves", "Wave8", "Hunter@L, Hunter@R, Child@FL, Child@FR, Fly@UL, Fly@UR, Chief@C, Chief@UC", waveDesc);
            cfgWave9 = Config.Bind("Waves", "Wave9", "", waveDesc);
            cfgWave10 = Config.Bind("Waves", "Wave10", "", waveDesc);

            CacheReflectionHandles();

            try
            {
                if (_battleSceneType == null) { Log.LogError("BattleScene type not found."); return; }
                var awakeMethod = AccessTools.Method(_battleSceneType, "Awake");
                if (awakeMethod == null) { Log.LogError("BattleScene.Awake not found."); return; }

                // This mobile bridge's Harmony type only exposes a parameterless ctor,
                // and its Patch overload accepts MethodInfo directly.
                var harmony = new Harmony();
                var postfix = AccessTools.Method(typeof(TribeTowerWavesPlugin), nameof(BattleSceneAwakePostfix));
                harmony.Patch(awakeMethod, postfix: postfix);
                Log.LogInfo("Patched BattleScene.Awake");
            }
            catch (Exception ex)
            {
                Log.LogError("Patching failed: " + ex);
            }
        }

        private static void CacheReflectionHandles()
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            _battleSceneType = AccessTools.TypeByName("BattleScene");
            _battleWaveType = AccessTools.TypeByName("BattleWave");
            _persistentBoolItemType = AccessTools.TypeByName("PersistentBoolItem");
            _enemyDeathEffectsType = AccessTools.TypeByName("EnemyDeathEffectsRegular");

            if (_battleSceneType != null)
                _wavesField = _battleSceneType.GetField("waves", F);

            if (_battleWaveType != null)
            {
                _bwBattleSceneField = _battleWaveType.GetField("battleScene", F);
                _bwStartDelayField = _battleWaveType.GetField("startDelay", F);
                _bwActivateEnemiesOnStartField = _battleWaveType.GetField("activateEnemiesOnStart", F);
                _bwClearDeathDropsField = _battleWaveType.GetField("clearDeathDrops", F);
                _bwStartWaveEventRegisterField = _battleWaveType.GetField("startWaveEventRegister", F);
            }

            if (_enemyDeathEffectsType != null)
                _setPlayerDataBoolField = _enemyDeathEffectsType.GetField("setPlayerDataBool", F);

            Log.LogInfo("Handles: BattleScene=" + (_battleSceneType != null)
                + " BattleWave=" + (_battleWaveType != null)
                + " waves=" + (_wavesField != null)
                + " bw.battleScene=" + (_bwBattleSceneField != null)
                + " bw.activate=" + (_bwActivateEnemiesOnStartField != null));
        }

        private static void BattleSceneAwakePostfix(object __instance)
        {
            try
            {
                if (!cfgEnabled.Value) return;
                if (__instance == null || _wavesField == null) return;

                var wavesList = _wavesField.GetValue(__instance) as IList;
                if (wavesList == null || wavesList.Count == 0)
                {
                    Log.LogError("[Waves] waves is null or empty.");
                    return;
                }

                // Snapshot original waves by index (never modified).
                var original = new GameObject[wavesList.Count];
                for (int i = 0; i < wavesList.Count; i++)
                {
                    var c = wavesList[i] as Component;
                    original[i] = c != null ? c.gameObject : null;
                }

                Log.LogInfo("[Waves] BattleScene.Awake. original count = " + original.Length);

                // Source enemies: Hunter + Child from Wave 1, Fly from Wave 2, Chief from Wave 4.
                var sources = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
                sources["Hunter"] = original.Length >= 1 ? FindEnemyInWave(original[0], "Bone Hunter") : null;
                sources["Child"]  = original.Length >= 1 ? FindEnemyInWave(original[0], "Bone Hunter Child") : null;
                sources["Fly"]    = original.Length >= 2 ? FindEnemyInWave(original[1], "Bone Hunter Fly") : null;
                sources["Chief"]  = original.Length >= 4 ? FindEnemyInWave(original[3], "Bone Hunter Fly Chief") : null;

                foreach (var kv in sources)
                    Log.LogInfo("[Waves] Source " + kv.Key + " = " + (kv.Value != null ? kv.Value.name : "NULL"));

                if (sources.Values.Any(v => v == null))
                {
                    Log.LogError("[Waves] Missing source enemy; aborting.");
                    return;
                }

                // Use the last original wave as template for BattleWave tunables and hierarchy parent.
                GameObject templateWave = original[original.Length - 1];
                Component templateBw = templateWave.GetComponent(_battleWaveType);

                var specs = new (int num, string spec)[]
                {
                    (1, cfgWave1.Value), (2, cfgWave2.Value), (3, cfgWave3.Value), (4, cfgWave4.Value),
                    (5, cfgWave5.Value), (6, cfgWave6.Value), (7, cfgWave7.Value), (8, cfgWave8.Value),
                    (9, cfgWave9.Value), (10, cfgWave10.Value),
                };

                foreach (var (num, spec) in specs)
                {
                    if (string.IsNullOrWhiteSpace(spec)) continue;
                    if (num <= 3)
                    {
                        Log.LogWarning("[Waves] Wave " + num + " is protected (1-3 are always vanilla). Skipping.");
                        continue;
                    }

                    GameObject newWaveGo = BuildWaveObject(
                        templateWave.transform.parent,
                        templateWave,
                        __instance,
                        "Wave " + num,
                        spec,
                        sources,
                        templateBw);

                    if (newWaveGo == null) continue;

                    var newBw = newWaveGo.GetComponent(_battleWaveType);
                    if (newBw == null)
                    {
                        Log.LogError("[Waves] Wave " + num + " missing BattleWave; discarding.");
                        UnityEngine.Object.Destroy(newWaveGo);
                        continue;
                    }

                    if (num <= wavesList.Count)
                    {
                        // Replace: deactivate the old GameObject but keep it in the scene (safer).
                        var oldComp = wavesList[num - 1] as Component;
                        wavesList[num - 1] = newBw;
                        if (oldComp != null && oldComp.gameObject != newWaveGo)
                            oldComp.gameObject.SetActive(false);

                        Log.LogInfo("[Waves] Replaced Wave " + num);
                    }
                    else
                    {
                        wavesList.Add(newBw);
                        Log.LogInfo("[Waves] Appended Wave " + num + " (list size " + wavesList.Count + ")");
                    }
                }

                Log.LogInfo("[Waves] Done. Final count = " + wavesList.Count);
            }
            catch (Exception ex)
            {
                Log.LogError("[Waves] Postfix failed: " + ex);
            }
        }

        private static GameObject BuildWaveObject(
            Transform parent,
            GameObject waveTemplate,
            object battleSceneInstance,
            string name,
            string spec,
            Dictionary<string, GameObject> sources,
            Component templateBw)
        {
            // Inactive container so AddComponent doesn't fire Awake yet.
            GameObject waveGo = new GameObject(name);
            waveGo.transform.SetParent(parent, false);
            waveGo.transform.position = waveTemplate.transform.position;
            waveGo.transform.rotation = waveTemplate.transform.rotation;
            waveGo.transform.localScale = waveTemplate.transform.localScale;
            waveGo.layer = waveTemplate.layer;
            waveGo.SetActive(false);

            // Instantiate enemies as inactive children first.
            var entries = spec.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);
            int added = 0;
            foreach (var entry in entries)
            {
                int at = entry.IndexOf('@');
                if (at < 0) { Log.LogWarning("[Waves] Bad entry (no @): " + entry); continue; }
                string alias = entry.Substring(0, at).Trim();
                string posName = entry.Substring(at + 1).Trim();

                if (!sources.TryGetValue(alias, out GameObject src) || src == null)
                {
                    Log.LogWarning("[Waves] Unknown enemy alias: " + alias);
                    continue;
                }
                if (!Positions.TryGetValue(posName, out Vector3 pos))
                {
                    Log.LogWarning("[Waves] Unknown position: " + posName);
                    continue;
                }

                GameObject enemy = UnityEngine.Object.Instantiate(src, waveGo.transform);
                enemy.name = src.name;
                enemy.transform.position = pos;                     // world coords
                enemy.transform.localScale = src.transform.localScale;
                enemy.SetActive(false);
                added++;
            }

            // Add BattleWave while GameObject is inactive (Awake deferred).
            Component bw = waveGo.AddComponent(_battleWaveType);

            // Copy tunables from the template wave's BattleWave.
            CopyField(templateBw, bw, _bwStartDelayField);
            CopyField(templateBw, bw, _bwClearDeathDropsField);
            CopyField(templateBw, bw, _bwStartWaveEventRegisterField);

            // Force activateEnemiesOnStart = true so freshly added children get activated.
            if (_bwActivateEnemiesOnStartField != null)
                _bwActivateEnemiesOnStartField.SetValue(bw, true);

            // Point back-reference at our BattleScene instance.
            if (_bwBattleSceneField != null)
                _bwBattleSceneField.SetValue(bw, battleSceneInstance);

            // Activate → Awake runs with children already present.
            waveGo.SetActive(true);

            if (cfgLogDiagnostics.Value)
                Log.LogInfo("[Waves]   " + name + " built with " + added
                    + " enemies, transformChildren=" + waveGo.transform.childCount);

            return waveGo;
        }

        private static void CopyField(object from, object to, FieldInfo f)
        {
            if (f == null || from == null || to == null) return;
            try { f.SetValue(to, f.GetValue(from)); } catch { }
        }

        private static GameObject FindEnemyInWave(GameObject waveGo, string exactName)
        {
            if (waveGo == null) return null;
            for (int i = 0; i < waveGo.transform.childCount; i++)
            {
                var c = waveGo.transform.GetChild(i).gameObject;
                if (c.name == exactName) return c;
                // Unity's Instantiate appends "(Clone)" with no space.
                if (c.name.StartsWith(exactName + "(Clone)", StringComparison.Ordinal)) return c;
                // Some ports use a " (n)" numeric suffix. Accept that too.
                if (c.name.StartsWith(exactName + " (", StringComparison.Ordinal)) return c;
            }
            return null;
        }
    }
}
