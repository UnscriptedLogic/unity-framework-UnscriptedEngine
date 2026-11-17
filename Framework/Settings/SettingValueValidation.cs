namespace UnscriptedEngine.Settings
{
    internal static class SettingValueValidation
    {
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
