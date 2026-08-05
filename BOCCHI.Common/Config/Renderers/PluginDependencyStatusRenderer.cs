using System.Reflection;
using BOCCHI.Common.Config.Fields;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Ocelot.Config.Renderers;
using Ocelot.Graphics;
using Ocelot.Ipc.BossMod;
using Ocelot.Ipc.VNavmesh;
using Ocelot.Services.PluginStatus;
using Ocelot.Services.Translation;

namespace BOCCHI.Common.Config.Renderers;

public sealed class PluginDependencyStatusRenderer(
    IDalamudPluginInterface plugin,
    IPluginStatus pluginStatus,
    IVNavmeshIpc vnav,
    IBossModIpc bossMod,
    ILifestreamIpc lifestream
) : IFieldRenderer<PluginDependencyStatusAttribute>
{
    private const string StatusKey = "config.dependencies.fields.status";

    public bool Render(object target, PropertyInfo prop, PluginDependencyStatusAttribute attr, Type owner, ITranslator translator)
    {
        ImGui.TextWrapped(T(translator, "intro"));
        ImGui.Spacing();

        DrawSection(T(translator, "required"));
        Draw("vnavmesh", "vnavmesh", translator, VnavIpc);
        Draw("Lifestream", "Lifestream", translator, (_, t) => Ipc(lifestream.IsAvailable, t));

        ImGui.Spacing();
        DrawSection(T(translator, "optional"));
        // Shared BossMod.Presets.* IPC — only show when that specific plugin is loaded.
        Draw("BossMod", "BossMod", translator, BossModIpcIfLoaded);
        Draw("BossMod Reborn", "BossModReborn", translator, BossModIpcIfLoaded);
        Draw("Wrath Combo", "WrathCombo", translator);
        // Dalamud InternalName is still "RotationSolver" (display name is Rotation Solver Reborn).
        Draw("Rotation Solver Reborn", "RotationSolver", translator);

        return false;
    }

    private (string? Detail, bool? Ok) VnavIpc(string _, ITranslator translator)
    {
        if (!vnav.IsAvailable())
        {
            return (T(translator, "ipc_missing"), false);
        }

        return vnav.IsNavmeshReady()
            ? (T(translator, "ready"), true)
            : (T(translator, "ipc_ok_mesh_pending"), true);
    }

    private (string? Detail, bool? Ok) BossModIpcIfLoaded(string internalName, ITranslator translator)
    {
        if (!pluginStatus.IsLoaded(internalName))
        {
            return (null, null);
        }

        return Ipc(bossMod.IsAvailable, translator);
    }

    private static (string? Detail, bool? Ok) Ipc(bool available, ITranslator translator) =>
        available ? (T(translator, "ipc_ok"), true) : (T(translator, "ipc_missing"), false);

    private void Draw(
        string displayName,
        string internalName,
        ITranslator translator,
        Func<string, ITranslator, (string? Detail, bool? Ok)>? ipc = null)
    {
        var (installLabel, installOk) = InstallLoadStatus(internalName, translator);
        var (ipcDetail, ipcOk) = ipc?.Invoke(internalName, translator) ?? (null, null);

        var status = ipcDetail is null ? installLabel : $"{installLabel} · {ipcDetail}";
        var ok = ipcOk is null ? installOk : installOk && ipcOk.Value;

        ImGui.TextUnformatted(displayName);
        ImGui.SameLine(240f);
        ImGui.TextColored(ok ? Color.Green.ToRgba() : Color.Red.ToRgba(), status);
    }

    private (string Label, bool Ok) InstallLoadStatus(string internalName, ITranslator translator)
    {
        if (pluginStatus.IsLoaded(internalName))
        {
            return (T(translator, "loaded"), true);
        }

        if (plugin.InstalledPlugins.Any(p => p.InternalName == internalName))
        {
            return (T(translator, "installed_not_loaded"), false);
        }

        return (T(translator, "not_installed"), false);
    }

    private static void DrawSection(string title)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(title);
        ImGui.Spacing();
    }

    private static string T(ITranslator translator, string field) =>
        translator.T($"{StatusKey}.{field}");
}
