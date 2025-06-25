using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
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

    public class PalThrowManager : MonoBehaviourPun
    {
        private Character character;

        private void Start()
        {
            character = GetComponent<Character>();
        }

        private void Update()
        {
            if (!character.IsLocal || !Input.GetKeyDown(KeyCode.X)) return;

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

        private void ThrowCharacter(Character target)
        {
            if (target == null || target.data.lastStoodOnPlayer != character)
            {
                Plugin.Log.LogWarning("Invalid throw target.");
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                Plugin.Log.LogWarning("Main camera not found.");
                return;
            }

            Vector3 direction = cam.transform.forward;
            float strength = Plugin.ThrowStrength.Value;

            // Call Throw on all clients via RPC
            photonView.RPC(nameof(ThrowCharacterRpc), RpcTarget.All, target.refs.view.ViewID, direction, strength);

            // Prevent repeated throws
            target.data.lastStoodOnPlayer = null;
        }

        [PunRPC]
        public void ThrowCharacterRpc(int viewID, Vector3 direction, float strength)
        {
            Character target = FindCharacterByViewID(viewID);
            if (target == null)
            {
                Plugin.Log.LogWarning("Target character not found via ViewID.");
                return;
            }

            foreach (Bodypart part in target.refs.ragdoll.partList)
            {
                Vector3 force = direction * strength * UnityEngine.Random.Range(1f, 1.2f) * 2000;
                part.AddForce(force, ForceMode.Acceleration);
            }

            Plugin.Log.LogInfo($"[RPC] Pushed {target.name} in direction {direction} with strength {strength}.");
        }

        private Character FindCharacterByViewID(int viewID)
        {
            foreach (Character c in Character.AllCharacters)
            {
                if (c.refs.view != null && c.refs.view.ViewID == viewID)
                    return c;
            }
            return null;
        }
    }
}
