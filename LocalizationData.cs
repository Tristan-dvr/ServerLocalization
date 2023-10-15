using System.Collections.Generic;
using System.Linq;

namespace ServerLocalization
{
    class LocalizationData : ISerializableParameter
    {
        private Dictionary<string, Dictionary<string, string>> _localizationData = new Dictionary<string, Dictionary<string, string>>();

        private ZPackage _tempPkg = new ZPackage();

        public void ClearAll()
        {
            _localizationData.Clear();
        }

        public void Deserialize(ref ZPackage pkg)
        {
            Log.Debug($"Received server localization package. Size: {pkg.Size()}");

            _localizationData.Clear();

            _tempPkg = pkg.ReadCompressedPackage();

            var languagesCount = _tempPkg.ReadInt();
            Dictionary<string, string> languageData;
            for (int i = 0; i < languagesCount; i++)
            {
                var keysCount = _tempPkg.ReadInt();
                var language = _tempPkg.ReadString();

                languageData = new Dictionary<string, string>();
                _localizationData[language] = languageData;

                for (int j = 0; j < keysCount; j++)
                {
                    languageData.Add(_tempPkg.ReadString(), _tempPkg.ReadString());
                }
            }
        }

        public void Serialize(ref ZPackage pkg)
        {
            _tempPkg.Clear();
            _tempPkg.Write(_localizationData.Count);
            foreach (var language in _localizationData)
            {
                _tempPkg.Write(language.Value.Count);
                _tempPkg.Write(language.Key);
                foreach (var key in language.Value)
                {
                    _tempPkg.Write(key.Key);
                    _tempPkg.Write(key.Value);
                }
            }

            pkg.WriteCompressed(_tempPkg);
            Log.Debug($"Sent server localization package. Size: {pkg.Size()}");
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
