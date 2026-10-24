using System;

namespace UnscriptedEngine.Settings
{
    /// <summary>Preserves this catalog and nested catalogs during Unity player stripping.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SettingsCatalogRootAttribute : Attribute { }
}
