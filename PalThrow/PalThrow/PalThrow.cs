using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PalThrow
{
    [BepInPlugin("com.lamia.palthrow", "PalThrow", "1.4.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> ThrowStrength;
        internal static ManualLogSource Log;

        private void Awake()
        {
            ThrowStrength = Config.Bind("General", "ThrowStrength", 5.5f, "How strong the throw is when throwing a character.");
            Log = Logger;
            Log.LogInfo("PalThrow loaded.");
            Harmony harmony = new Harmony("com.lamia.palthrow");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    public static class PalThrow
    {
        [HarmonyPostfix]
        public static void AwakePatch(Character __instance)
        {
            if (__instance.GetComponent<PalThrowManager>() == null)
            {
                __instance.gameObject.AddComponent<PalThrowManager>();
                Plugin.Log.LogInfo("PalThrowManager added to: " + __instance.name);
            }
        }
    }

    public class PalThrowManager : MonoBehaviour
    {
        private Character character;

        private void Start()
        {
            character = GetComponent<Character>();
        }

        private void Update()
        {
            if (!character.IsLocal || !Input.GetKeyDown(KeyCode.X)) return;

            // Search all characters to see who's standing on you
            foreach (var other in FindObjectsOfType<Character>())
            {
                if (other == character) continue;
                if (other.data.lastStoodOnPlayer == character)
                {
                    ThrowCharacter(other);
                    return;
                }
            }

            Plugin.Log.LogInfo("No character is currently standing on you.");
        }

        internal void AddForce(Character target, Vector3 move, float minRandomMultiplier = 1f, float maxRandomMultiplier = 1f)
        {
            foreach (Bodypart part in target.refs.ragdoll.partList)
            {
                Vector3 force = move;
                if (minRandomMultiplier != maxRandomMultiplier)
                {
                    force *= UnityEngine.Random.Range(minRandomMultiplier, maxRandomMultiplier);
                }

                part.AddForce(force, ForceMode.Acceleration);
            }
        }

        private void ThrowCharacter(Character target)
        {
            if (target == null)
            {
                Plugin.Log.LogWarning("Tried to push a null character.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Plugin.Log.LogWarning("Main camera not found.");
                return;
            }

            Vector3 direction = cam.transform.forward;
            float strength = Plugin.ThrowStrength.Value; // z. B. Config-Wert

            // Wende Force direkt auf Bodyparts an
            foreach (Bodypart part in target.refs.ragdoll.partList)
            {
                Vector3 force = direction * strength * UnityEngine.Random.Range(1f, 1.2f) * 200;
                part.AddForce(force, ForceMode.Acceleration);
            }

            Plugin.Log.LogInfo($"Pushed {target.name} in direction {direction} with strength {strength}.");
        }

    }
}
