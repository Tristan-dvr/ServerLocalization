using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
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
        public const string Guid = "org.tristan.serverlocalization";
        public const string Name = "Server Localization";
        public const string Version = "1.1.0";

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
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    _localizationData.AddLocalization(language, data);
                    Log.Info($"Added server localization {name}, language {language}");
                }
                catch (Exception e)
                {
                    Log.Info($"Cannot parse localization file {file}: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        [HarmonyPatch]
        private class Patches
        {
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

            private static void OnLocalizationDataReceived(ZRpc arg1, LocalizationData localizationData)
            {
                _localizationData.SetData(localizationData);
                Log.Info($"Server localization received. {localizationData}");
                var localization = Localization.instance;
                var selectedLanguage = localization.GetSelectedLanguage();
                foreach (var lang in _localizationData.GetLanguages().Where(l => localization.GetLanguages().Contains(l) && l != selectedLanguage))
                {
                    AddTranslations(lang);
                }
                if (_localizationData.GetLanguages().Contains(selectedLanguage))
                {
                    AddTranslations(selectedLanguage);
                }
            }

            private static void AddTranslations(string language)
            {
                var translations = _localizationData.GetTranslations(language);
                Log.Debug($"Added server localization {language}");
                foreach (var t in translations)
                {
                    Localization.instance.AddWord(t.Key, t.Value);
                    Log.Debug($"Added server localization key {t.Key}->{t.Value}");
                }
            }
        }
    }
}
