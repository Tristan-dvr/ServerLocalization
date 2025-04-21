using BepInEx;
using fastJSON;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ServerLocalization
{
    [BepInPlugin(Guid, Name, Version)]
    internal class Plugin : BaseUnityPlugin
    {
        private const string DefaultLanguage = "English";

        public const string Guid = "org.tristan.serverlocalization";
        public const string Name = "Server Localization";
        public const string Version = "1.1.5";

        private const string LocalizationDataRpc = "ServerLocalization_LocalizationDataRpc";

        private static LocalizationData _localizationData = new LocalizationData();

        private void Awake()
        {
            Log.CreateInstance(Logger);

            var directory = Path.Combine(Paths.ConfigPath, "ServerLocalization");
            LoadLocalizationData(directory);
            Helper.WatchFolderChanges(directory, () => LoadLocalizationData(directory));

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), Guid);
        }

        private void LoadLocalizationData(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var language = Path.GetFileNameWithoutExtension(Path.GetDirectoryName(file));

                    var json = File.ReadAllText(file);
                    var data = JSON.ToObject<Dictionary<string, string>>(json);
                    _localizationData.AddLocalization(language, data);
                    Log.Info($"Loaded server localization {name}, language {language}");
                }
                catch (Exception e)
                {
                    Log.Error($"Cannot parse localization file {file}: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        [HarmonyPatch]
        private class Patches
        {
            private static bool _localizationInitialized = false;

            [HarmonyPostfix, HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
            private static void ZNet_OnNewConnection(ZNet __instance, ZNetPeer peer)
            {
                if (!__instance.IsServer())
                    peer.m_rpc.Register<LocalizationData>(LocalizationDataRpc, OnLocalizationDataReceived);
            }

            [HarmonyPostfix, HarmonyPatch(typeof(ZNet), nameof(ZNet.SendPeerInfo))]
            private static void ZNet_SendPeerInfo(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer())
                    rpc.Invoke(LocalizationDataRpc, _localizationData);
            }

            [HarmonyPostfix, HarmonyPatch(typeof(Localization), MethodType.Constructor)]
            private static void Localization_Initialize(Localization __instance)
            {
                SetupServerLocalization(__instance, __instance.GetSelectedLanguage());
                _localizationInitialized = true;
            }

            [HarmonyPostfix, HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
            private static void Localization_SetupLanguage(Localization __instance, string language)
            {
                if (!_localizationInitialized)
                    return;

                SetupServerLocalization(__instance, language);
            }

            private static void OnLocalizationDataReceived(ZRpc arg1, LocalizationData localizationData)
            {
                _localizationData.SetData(localizationData);
                Log.Info($"Server localization received. {localizationData}");

                var localization = Localization.instance;
                SetupServerLocalization(localization, localization.GetSelectedLanguage());
            }

            private static void SetupServerLocalization(Localization localization, string language)
            {
                var languages = _localizationData.GetLanguages();
                if (languages.Contains(DefaultLanguage))
                {
                    AddTranslations(localization, DefaultLanguage);
                }
                if (!string.Equals(DefaultLanguage, language) && languages.Contains(language))
                {
                    AddTranslations(localization, language);
                }
            }

            private static void AddTranslations(Localization localization, string language)
            {
                var translations = _localizationData.GetTranslations(language);
                Log.Info($"Added server localization {language}");
                foreach (var t in translations)
                {
                    localization.AddWord(t.Key.TrimStart('$'), t.Value);
                    Log.Debug($"Added server localization key {t.Key}->{t.Value}");
                }
            }
        }
    }
}
