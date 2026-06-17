/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using HaCreator.MapEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HaCreator.GUI
{
    public class HaRibbon
    {
        // ── Menu/toolbar events ────────────────────────────────────────
        public event Action NewClicked;
        public event Action OpenClicked;
        public event Action SaveClicked;
        public event Action RepackClicked;
        public event Action AboutClicked;
        public event Action HelpClicked;
        public event Action SettingsClicked;
        public event Action ExitClicked;
        public event Action ExportClicked;
        public event Action HaRepackerClicked;
        public event Action FinalizeClicked;
        public event Action NewPlatformClicked;
        public event Action UserObjsClicked;
        public event Action MapSimulationClicked;
        public event Action RegenerateMinimapClicked;
        public event Action MapPhysicsClicked;
        public event Action ShowQuestEditorWindowClicked;
        public event Action ShowMapPropertiesClicked;
        public event Action<Avalonia.Controls.TabItem> MapInfoClicked;

        public event Action<bool> ShowMinimapToggled;
        public event Action<bool> ParallaxToggled;
        public event Action<bool> SnappingToggled;
        public event Action<bool> RandomTilesToggled;
        public event Action<bool> InfoModeToggled;

        public delegate void ViewToggledDelegate(bool? tiles, bool? objs, bool? npcs, bool? mobs,
            bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs,
            bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField);
        public event ViewToggledDelegate ViewToggled;

        public delegate void LayerViewChangedDelegate(int layer, int platform, bool allLayers, bool allPlats, string tileSet);
        public event LayerViewChangedDelegate LayerViewChanged;

        public event EventHandler RibbonKeyDown;

        // ── Fire helpers (called by HaEditorWindow menu handlers) ─────
        public void FireNew()                     => NewClicked?.Invoke();
        public void FireOpen()                    => OpenClicked?.Invoke();
        public void FireSave()                    => SaveClicked?.Invoke();
        public void FireRepack()                  => RepackClicked?.Invoke();
        public void FireAbout()                   => AboutClicked?.Invoke();
        public void FireHelp()                    => HelpClicked?.Invoke();
        public void FireSettings()                => SettingsClicked?.Invoke();
        public void FireExit()                    => ExitClicked?.Invoke();
        public void FireExport()                  => ExportClicked?.Invoke();
        public void FireMapSimulation()           => MapSimulationClicked?.Invoke();
        public void FireRegenerateMinimap()       => RegenerateMinimapClicked?.Invoke();
        public void FireMapPhysics()              => MapPhysicsClicked?.Invoke();
        public void FireFinalize()                => FinalizeClicked?.Invoke();
        public void FireNewPlatform()             => NewPlatformClicked?.Invoke();
        public void FireUserObjs()                => UserObjsClicked?.Invoke();
        public void FireHaRepacker()              => HaRepackerClicked?.Invoke();
        public void FireShowMinimap(bool v)       => ShowMinimapToggled?.Invoke(v);
        public void FireParallax(bool v)          => ParallaxToggled?.Invoke(v);
        public void FireSnapping(bool v)          => SnappingToggled?.Invoke(v);
        public void FireRandomTiles(bool v)       => RandomTilesToggled?.Invoke(v);
        public void FireInfoMode(bool v)          => InfoModeToggled?.Invoke(v);
        public void FireMapInfo(Avalonia.Controls.TabItem tab)   => MapInfoClicked?.Invoke(tab);
        public void FireShowQuestEditor()                         => ShowQuestEditorWindowClicked?.Invoke();
        public void FireShowMapProperties()                       => ShowMapPropertiesClicked?.Invoke();
        public void FireViewToggled(bool? tiles, bool? objs, bool? npcs, bool? mobs,
            bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs,
            bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField)
            => ViewToggled?.Invoke(tiles, objs, npcs, mobs, reactors, portals, footholds,
                                   ropes, chairs, tooltips, backgrounds, misc, mirrorField);
        public void FireLayerViewChanged(int layer, int platform, bool allLayers, bool allPlats, string tileSet)
            => LayerViewChanged?.Invoke(layer, platform, allLayers, allPlats, tileSet);
        public void FireRibbonKeyDown(object sender, EventArgs e) => RibbonKeyDown?.Invoke(sender, e);

        // ── State setters (called by HaCreatorStateManager) ──────────
        public void SetEnabled(bool enabled) { }

        public void SetOptions(bool useMiniMap, bool emulateParallax, bool useSnapping,
                               bool randomTiles, bool infoMode) { }

        public void SetLayers(ReadOnlyCollection<Layer> layers) { }

        public void SetSelectedLayer(int layerIndex, int platform, bool allLayers, bool allPlats) { }

        public void SetHasMinimap(bool hasMm) { }

        public void SetLayer(Layer layer) { }

        public void SetVisibilityCheckboxes(bool? tiles, bool? objs, bool? npcs, bool? mobs,
            bool? reactors, bool? portals, bool? footholds, bool? ropes, bool? chairs,
            bool? tooltips, bool? backgrounds, bool? misc, bool? mirrorField) { }
    }
}
