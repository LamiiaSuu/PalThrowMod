﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PalThrow
{
    [BepInPlugin("com.lamia.palthrow", "PalThrow", "1.6.5")]
    public class Plugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> BaseThrowStrength;
        public static ConfigEntry<float> MaxChargeTime;
        public static ConfigEntry<float> StaminaCost;
        public static ConfigEntry<float> MinThrowStrength;
        public static ConfigEntry<float> MaxThrowStrength;
        public static ConfigEntry<KeyCode> ThrowKey;
        internal static ManualLogSource Log;

        private void Awake()
        {
            ThrowKey = Config.Bind("Controls", "ThrowKey", KeyCode.X,"The key to activate the slide");
            MaxChargeTime = Config.Bind("General", "MaxChargeTime", 3f, "Max charge time in seconds.");
            StaminaCost = Config.Bind("General", "StaminaCost", 1f, "Stamina drain per second multiplier.");
            MinThrowStrength = Config.Bind("Throwing", "MinThrowStrength", 0.5f, "Throw strength at 0% charge.");
            MaxThrowStrength = Config.Bind("Throwing", "MaxThrowStrength", 2.5f, "Throw strength at 100% charge.");
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

        private float chargeTime = 0f;
        private float lastThrowTime = -999f;
        private bool isCharging = false;

        private void Start()
        {
            character = GetComponent<Character>();
        }

        private void Update()
        {
            if (!character.IsLocal)
                return;

            if (Time.time < lastThrowTime + 3f)
            {
                isCharging = false;
                chargeTime = 0f;
                return;
            }

            // Only proceed if someone is standing on you
            Character target = GetCharacterStandingOnMe();
            if (target == null)
            {
                if (isCharging)
                {
                    isCharging = false;
                    chargeTime = 0f;
                }
                return;
            }

            if (Input.GetKey(Plugin.ThrowKey.Value))
            {
                if (!isCharging)
                {
                    isCharging = true;
                    chargeTime = 0f;
                }

                chargeTime += Time.deltaTime;
                if (chargeTime >= Plugin.MaxChargeTime.Value)
                {
                    chargeTime = Plugin.MaxChargeTime.Value;
                    character.data.sinceUseStamina = 0f;
                    return; // Stop charging after max time
                }

                float drainRate = Plugin.StaminaCost.Value *0.2f* Time.deltaTime;
                if (character.GetTotalStamina() > drainRate)
                {
                    character.AddStamina(-drainRate);
                    character.data.sinceUseStamina = 0f;
                }
                else
                {
                    chargeTime = Mathf.Max(0.01f);
                    character.PassOutInstantly();
                    isCharging = false;
                }
            }
            else if (isCharging)
            {
                Throw(target);
                target = null;
            }
        }

        private void Throw(Character target)
        {
            isCharging = false;

            if (target == null)
            {
                Plugin.Log.LogInfo("No character is currently standing on you.");
                chargeTime = 0f;
                return;
            }

            // Clamp chargeTime to ensure it's between 0 and MaxChargeTime
            chargeTime = Mathf.Clamp(chargeTime, 0f, Plugin.MaxChargeTime.Value);

            // Calculate percentage of max charge
            float chargePercent = chargeTime / Plugin.MaxChargeTime.Value;

            // Linearly interpolate strength based on percentage
            float strength = Mathf.Lerp(Plugin.MinThrowStrength.Value, Plugin.MaxThrowStrength.Value, chargePercent) * 1000;

            DoThrow(target, strength);
            lastThrowTime = Time.time;
            chargeTime = 0f;
        }

        private Character GetCharacterStandingOnMe()
        {
            foreach (var other in FindObjectsOfType<Character>())
            {
                if (other != character && other.data.lastStoodOnPlayer == character && other.data.sinceStandOnPlayer <= 0.3f)
                    return other;
            }
            return null;
        }

        private void DoThrow(Character target, float strength)
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
            photonView.RPC(nameof(ThrowCharacterRpc), RpcTarget.All, target.refs.view.ViewID, direction, strength);

            target.data.lastStoodOnPlayer = null;
            Plugin.Log.LogInfo($"Threw {target.name} with strength {strength:F2}.");
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
                Vector3 force = direction * strength;
                part.AddForce(force, ForceMode.Acceleration);
            }

            Plugin.Log.LogInfo($"[RPC] Threw {target.name} in direction {direction} with strength {strength}.");
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
