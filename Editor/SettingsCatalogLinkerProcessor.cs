using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using UnscriptedEngine.Settings;

namespace UnscriptedEngine.Editor
{
    /// <summary>Preserves explicitly annotated catalogs; no player-wide reflection scan is required.</summary>
    public sealed class SettingsCatalogLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            string directory = Path.GetFullPath("Temp/UnscriptedEngine.Settings");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "link.xml");
            HashSet<string> playerAssemblyNames = new HashSet<string>(
                UnityEditor.Compilation.CompilationPipeline.GetAssemblies(
                    UnityEditor.Compilation.AssembliesType.Player).Select(assembly => assembly.name));
            IEnumerable<Type> playerCatalogs = TypeCache.GetTypesWithAttribute<SettingsCatalogRootAttribute>()
                .Where(type => playerAssemblyNames.Contains(type.Assembly.GetName().Name));
            File.WriteAllText(path, BuildPreservationXml(playerCatalogs));
            return path;
        }

        public static string BuildPreservationXml(IEnumerable<Type> catalogTypes)
        {
            HashSet<Type> preservedTypes = new HashSet<Type>();
            foreach (Type catalogType in catalogTypes) CollectNestedTypes(catalogType, preservedTypes);
            StringBuilder xml = new StringBuilder("<linker>\n");
            foreach (IGrouping<string, Type> assembly in preservedTypes
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .GroupBy(type => type.Assembly.GetName().Name))
            {
                xml.Append("  <assembly fullname=\"").Append(SecurityElement.Escape(assembly.Key)).Append("\">\n");
                foreach (Type type in assembly)
                    xml.Append("    <type fullname=\"").Append(SecurityElement.Escape(type.FullName.Replace('+', '/')))
                        .Append("\" preserve=\"all\" />\n");
                xml.Append("  </assembly>\n");
            }
            return xml.Append("</linker>\n").ToString();
        }

        private static void CollectNestedTypes(Type type, HashSet<Type> result)
        {
            if (!result.Add(type)) return;
            foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                CollectNestedTypes(nestedType, result);
        }
    }
}
