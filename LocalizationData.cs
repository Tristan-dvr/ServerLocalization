using System.Collections.Generic;
using System.Linq;

namespace ServerLocalization
{
    class LocalizationData : ISerializableParameter
    {
        private Dictionary<string, Dictionary<string, string>> _localizationData = new Dictionary<string, Dictionary<string, string>>();

        public void ClearAll()
        {
            _localizationData.Clear();
        }

        public void Deserialize(ref ZPackage pkg)
        {
            _localizationData.Clear();

            var languagesCount = pkg.ReadInt();
            Dictionary<string, string> _languageData;
            for (int i = 0; i < languagesCount; i++)
            {
                var keysCount = pkg.ReadInt();
                var language = pkg.ReadString();

                _languageData = new Dictionary<string, string>();
                _localizationData[language] = _languageData;

                for (int j = 0; j < keysCount; j++)
                {
                    _languageData.Add(pkg.ReadString(), pkg.ReadString());
                }
            }
            Log.Debug($"Received server localization package. Size: {pkg.Size()}");
        }

        public void Serialize(ref ZPackage pkg)
        {
            pkg.Write(_localizationData.Count);
            foreach (var language in _localizationData)
            {
                pkg.Write(language.Value.Count);
                pkg.Write(language.Key);
                foreach (var key in language.Value)
                {
                    pkg.Write(key.Key);
                    pkg.Write(key.Value);
                }
            }
        }

        public void SetData(LocalizationData localizationData)
        {
            _localizationData = localizationData._localizationData;
        }

        public void AddLocalization(string language, Dictionary<string, string> keys)
        {
            if (!_localizationData.TryGetValue(language, out var data))
            {
                data = new Dictionary<string, string>();
                _localizationData.Add(language, data);
            }
            foreach (var key in keys)
                data[key.Key] = key.Value;
        }

        public IReadOnlyCollection<string> GetLanguages()
        {
            return _localizationData.Keys;
        }

        public IReadOnlyDictionary<string, string> GetTranslations(string language)
        {
            return _localizationData.TryGetValue(language, out var result) ? result : null;
        }

        public override string ToString()
        {
            var keysCount = _localizationData.Sum(language => language.Value.Count);
            return $"Languages {_localizationData.Count}. Keys {keysCount}";
        }
    }
}
