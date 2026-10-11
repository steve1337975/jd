using NChecker.models.local;
using Newtonsoft.Json;

namespace NChecker.classes
{
    internal class SettingsManager
    {
        internal SettingsData Settings { get; private set; }
        internal SettingsManager() => Settings = LoadSettings();
        SettingsData LoadSettings()
        {
            try
            {
                return JsonConvert.DeserializeObject<SettingsData>(Properties.Resources.settings);
            }
            catch (Exception exc) { }
            return new SettingsData();
        }
    }
}
