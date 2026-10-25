using System.Collections.Generic;
using System.Text;

namespace UnscriptedEngine.Editor.Scaffolding
{
    public static class ItemScaffoldSources
    {
        public static Dictionary<string, string> Create(ItemScaffoldPlan plan)
        {
            plan.Validate();

            string n = plan.name;
            Dictionary<string, string> sources = new Dictionary<string, string>();

            sources[plan.ScriptPath("Visual")] = Wrap("using UnityEngine;", $@"public sealed class {n}Visual : MonoBehaviour
{{
    // Assign presentation references here: muzzle, Animator, particles, audio, etc.
}}");

            sources[plan.ScriptPath("Fragment")] = Wrap(@"using System;
using UnityEngine;
using UnscriptedEngine;
using UnscriptedEngine.Item;", $@"[Serializable]
public sealed class {n}Fragment : UItemFragBehaviour
{{
    // Add shared gameplay settings here. Never store per-item mutable state here.
    public override ItemBehaviour CreateBehaviour(ItemBehaviourContext context)
    {{
        {n}Visual visual = context.GetVisualComponent<{n}Visual>();

        if (visual == null)
        {{
            throw new InvalidOperationException(""{n} requires its held visual component."");
        }}

        return new {n}Behaviour(context, this, visual);
    }}
}}");

            sources[plan.ScriptPath("Behaviour")] = Wrap("using UnscriptedEngine.Item;", $@"public sealed class {n}Behaviour : ItemBehaviour
{{
    private readonly ItemBehaviourContext context;
    private readonly {n}Fragment settings;
    private readonly {n}Visual visual;

    public {n}Behaviour(ItemBehaviourContext context, {n}Fragment settings, {n}Visual visual)
    {{
        this.context = context;
        this.settings = settings;
        this.visual = visual;
    }}

    public override void Equip()
    {{
    }}

    public override void Tick(float deltaTime)
    {{
    }}

    public override bool Use(string action, bool pressed)
    {{
        // Implement actions here, then return true for handled input.
        // Select server authority or local presentation explicitly for networked games.
        return false;
    }}

    public override void CancelUse()
    {{
    }}

    public override void Unequip()
    {{
    }}

    public override void Dispose()
    {{
    }}
}}");

            sources[plan.ScriptPath("WorldItem")] = Wrap("using UnscriptedEngine.Item;", $@"public sealed class {n}WorldItem : UWorldItem
{{
    // Default interaction is configured on the world prefab. Add world-only behaviour here.
}}");

            return sources;
        }

        private static string Wrap(string imports, string body)
        {
            StringBuilder source = new StringBuilder();
            source.Append(imports.Replace("\r\n", "\n"));
            source.Append("\n\nnamespace ").Append(ItemScaffoldPlan.GeneratedNamespace).Append("\n{\n");

            foreach (string line in body.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length > 0)
                {
                    source.Append("    ").Append(line);
                }

                source.Append('\n');
            }

            source.Append("}\n");

            return source.ToString();
        }
    }
}
