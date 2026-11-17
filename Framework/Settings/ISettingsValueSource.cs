using System;
using System.Collections.Generic;

namespace UnscriptedEngine.Settings
{
    /// <summary>Editable values used by generated controls, independent of preference storage.</summary>
    public interface ISettingsValueSource
    {
        IReadOnlyList<SettingsGroup> ActiveGroups { get; }
        event Action StructureChanged;
        object Get(SettingDefinition definition);
        bool CanWrite(SettingDefinition definition);
        bool TrySet(SettingDefinition definition, object value);
        bool Reset(SettingDefinition definition);
        IDisposable Observe(SettingDefinition definition, Action<object> callback, bool emitCurrent = true);
    }
}
