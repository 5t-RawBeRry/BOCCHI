using System.Reflection;
using BOCCHI.Common.Config.Fields;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Ocelot.Config.Renderers;
using Ocelot.Graphics;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.Lifestream;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.PluginStatus;
using Ocelot.Services.Translation;

namespace BOCCHI.Common.Config.Renderers;

public sealed class PluginDependencyStatusRenderer(
    IDalamudPluginInterface plugin,
    IPluginStatus pluginStatus,
    IVNavmeshIpc vnav,
    IBossModIpc bossMod,
    ILifestreamIpc lifestream,
    AutomatorConfig automator
) : IFieldRenderer<PluginDependencyStatusAttribute>
{
    private const string StatusKey = "config.dependencies.fields.status";

    private static readonly CombatAutorotationDisplay CombatDisplay = new();

    public bool Render(object target, PropertyInfo prop, PluginDependencyStatusAttribute attr, Type owner, ITranslator translator)
    {
        ImGui.TextWrapped(T(translator, "intro"));
        ImGui.Spacing();

        DrawSection(T(translator, "required"));
        Draw("vnavmesh", "vnavmesh", translator, VnavStatus);
        Draw("Lifestream", "Lifestream", translator, (_, t) => IpcStatus(lifestream.IsAvailable, t));

        ImGui.Spacing();
        DrawSection(T(translator, "optional"));
        if (automator.CombatAutorotation.UsesCombatAutomation())
        {
            ImGui.TextWrapped(string.Format(T(translator, "using"), CombatDisplay.Display(automator.CombatAutorotation)));
        }

        ImGui.TextWrapped(T(translator, "optional_intro"));
        ImGui.Spacing();

        Draw("Wrath Combo", "WrathCombo", translator, inUse: InUse("WrathCombo"));
        // Dalamud InternalName is still "RotationSolver" (display name is Rotation Solver Reborn).
        Draw("Rotation Solver Reborn", "RotationSolver", translator, inUse: InUse("RotationSolver"));
        Draw("BossMod", "BossMod", translator, BossModIpcIfLoaded, InUse("BossMod"));
        Draw("BossMod Reborn", "BossModReborn", translator, BossModIpcIfLoaded, InUse("BossModReborn"));

        return false;
    }

    private bool InUse(string internalName) => automator.CombatAutorotation switch
    {
        CombatAutorotation.WrathCombo => internalName is "WrathCombo" or "BossMod" or "BossModReborn",
        CombatAutorotation.RotationSolverReborn => internalName is "RotationSolver" or "BossMod" or "BossModReborn",
        CombatAutorotation.BossMod => internalName == "BossMod",
        CombatAutorotation.BossModReborn => internalName == "BossModReborn",
        _ => false,
    };

    private (string Label, bool Ok, bool Pending) VnavStatus(string _, ITranslator translator)
    {
        if (!vnav.IsAvailable())
        {
            return (T(translator, "not_working"), false, false);
        }

        return vnav.IsNavmeshReady()
            ? (T(translator, "ready"), true, false)
            : (T(translator, "map_loading"), true, true);
    }

    private (string Label, bool Ok, bool Pending) BossModIpcIfLoaded(string _, ITranslator translator) =>
        IpcStatus(bossMod.IsAvailable, translator);

    private static (string Label, bool Ok, bool Pending) IpcStatus(bool available, ITranslator translator) =>
        available
            ? (T(translator, "ready"), true, false)
            : (T(translator, "not_working"), false, false);

    private void Draw(
        string displayName,
        string internalName,
        ITranslator translator,
        Func<string, ITranslator, (string Label, bool Ok, bool Pending)>? ipc = null,
        bool inUse = false)
    {
        var (label, ok, pending) = ResolveStatus(internalName, translator, ipc);
        if (inUse && ok)
        {
            label = $"{label} · {T(translator, "in_use")}";
        }

        ImGui.TextUnformatted(displayName);
        ImGui.SameLine(280f);
        ImGui.TextColored(StatusColor(ok, pending), label);
    }

    private (string Label, bool Ok, bool Pending) ResolveStatus(
        string internalName,
        ITranslator translator,
        Func<string, ITranslator, (string Label, bool Ok, bool Pending)>? ipc)
    {
        if (pluginStatus.IsLoaded(internalName))
        {
            return ipc?.Invoke(internalName, translator) ?? (T(translator, "ready"), true, false);
        }

        if (plugin.InstalledPlugins.Any(p => p.InternalName == internalName))
        {
            return (T(translator, "not_enabled"), false, false);
        }

        return (T(translator, "not_installed"), false, false);
    }

    private static uint StatusColor(bool ok, bool pending) =>
        pending ? new Color(255, 196, 0).ToRgba() : (ok ? Color.Green : Color.Red).ToRgba();

    private static void DrawSection(string title)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(title);
        ImGui.Spacing();
    }

    private static string T(ITranslator translator, string field) =>
        translator.T($"{StatusKey}.{field}");
}
