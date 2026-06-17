/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using HaCreator.MapEditor.UndoRedo;
using HaSharedLibrary.Render.DX;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;
using XnaPoint = Microsoft.Xna.Framework.Point;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HaCreator.GUI.InstanceEditor
{
    public static class InstanceEditors
    {
        public static void DispatchAsync(BoardItem item, Window? owner)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if      (item is TileInstance tile)         await ShowTileEditorAsync(tile, owner);
                    else if (item is ObjectInstance obj)        await ShowObjectEditorAsync(obj, owner);
                    else if (item is LifeInstance life)         await ShowLifeEditorAsync(life, owner);
                    else if (item is ReactorInstance reactor)   await ShowReactorEditorAsync(reactor, owner);
                    else if (item is PortalInstance portal)     await ShowPortalEditorAsync(portal, owner);
                    else if (item is BackgroundInstance bg)     await ShowBackgroundEditorAsync(bg, owner);
                    else if (item is ToolTipInstance tooltip)   await ShowTooltipEditorAsync(tooltip, owner);
                    else if (item is RopeAnchor rope)           await ShowRopeEditorAsync(rope, owner);
                    else if (item is FootholdAnchor fhAnchor)
                    {
                        var lines = fhAnchor.connectedLines.OfType<FootholdLine>().ToArray();
                        if (lines.Length > 0) await ShowFootholdEditorAsync(lines, owner);
                    }
                    // FootholdLine is MapleLine, not BoardItem — anchor case above handles it
                    else                                         await ShowGeneralEditorAsync(item, owner);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"InstanceEditor error: {ex}");
                }
            });
        }

        // ── General (x, y, z) ────────────────────────────────────────────

        public static async Task ShowGeneralEditorAsync(BoardItem item, Window? owner)
        {
            var xNud = Nud(-9999, 9999, item.X);
            var yNud = Nud(-9999, 9999, item.Y);
            var zNud = Nud(-9999, 9999, item.Z); zNud.IsEnabled = item.Z != -1;

            bool ok = await ShowDialogAsync(owner, "Edit Item",
                HaCreatorStateManager.CreateItemDescription(item),
                new[] { R("X", xNud), R("Y", yNud), R("Z", zNud) });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                if (zNud.IsEnabled && item.Z != (int)zNud.Value!)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zNud.Value!));
                    item.Z = (int)zNud.Value!;
                    item.Board.BoardItems.Sort();
                }
                Commit(actions, item.Board);
            }
        }

        // ── Tile (x, y, z) ──────────────────────────────────────────────

        public static async Task ShowTileEditorAsync(TileInstance item, Window? owner)
        {
            var xNud = Nud(-9999, 9999, item.X);
            var yNud = Nud(-9999, 9999, item.Y);
            var zNud = Nud(-9999, 9999, item.Z);

            bool ok = await ShowDialogAsync(owner, "Edit Tile",
                HaCreatorStateManager.CreateItemDescription(item),
                new[] { R("X", xNud), R("Y", yNud), R("Z", zNud) });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                if (item.Z != (int)zNud.Value!)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zNud.Value!));
                    item.Z = (int)zNud.Value!;
                    item.Board.BoardItems.Sort();
                }
                Commit(actions, item.Board);
            }
        }

        // ── Life (mob/npc) ───────────────────────────────────────────────

        public static async Task ShowLifeEditorAsync(LifeInstance item, Window? owner)
        {
            var xNud  = Nud(-9999, 9999, item.X);
            var yNud  = Nud(-9999, 9999, item.Y);
            var rx0   = Nud(-9999, 9999, item.rx0Shift);
            var rx1   = Nud(-9999, 9999, item.rx1Shift);
            var yShft = Nud(-9999, 9999, item.yShift);

            var (infoEn, infoNud)     = OptNud(item.Info);
            var (teamEn, teamNud)     = OptNud(item.Team);
            var (mobEn,  mobNud)      = OptNud(item.MobTime);
            var (nameEn, nameBox)     = OptText(item.LimitedName);
            var hideBox  = new CheckBox { Content = "Hide",  IsChecked = item.Hide };
            var flipBox  = new CheckBox { Content = "Flip",  IsChecked = item.Flip };

            bool ok = await ShowDialogAsync(owner, "Edit Life",
                HaCreatorStateManager.CreateItemDescription(item),
                new[]
                {
                    R("X", xNud), R("Y", yNud),
                    R("rx0Shift", rx0), R("rx1Shift", rx1), R("yShift", yShft),
                    R("Info", InlineOpt(infoEn, infoNud)),
                    R("Team", InlineOpt(teamEn, teamNud)),
                    R("MobTime", InlineOpt(mobEn, mobNud)),
                    R("LimitedName", InlineOpt(nameEn, nameBox)),
                    R("", hideBox), R("", flipBox)
                });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                Commit(actions, item.Board);
                item.rx0Shift   = (int)rx0.Value!;
                item.rx1Shift   = (int)rx1.Value!;
                item.yShift     = (int)yShft.Value!;
                item.Info       = infoEn.IsChecked == true  ? (int?)infoNud.Value : null;
                item.Team       = teamEn.IsChecked == true  ? (int?)teamNud.Value : null;
                item.MobTime    = mobEn.IsChecked  == true  ? (int?)mobNud.Value  : null;
                item.LimitedName= nameEn.IsChecked == true  ? nameBox.Text : null;
                item.Hide       = hideBox.IsChecked == true;
                item.Flip       = flipBox.IsChecked == true;
            }
        }

        // ── Reactor ───────────────────────────────────────────────────────

        public static async Task ShowReactorEditorAsync(ReactorInstance item, Window? owner)
        {
            var xNud    = Nud(-9999, 9999, item.X);
            var yNud    = Nud(-9999, 9999, item.Y);
            var timeNud = Nud(0, 99999, item.ReactorTime);
            var (nameEn, nameBox) = OptText(item.Name);

            bool ok = await ShowDialogAsync(owner, "Edit Reactor",
                HaCreatorStateManager.CreateItemDescription(item),
                new[] { R("X", xNud), R("Y", yNud), R("Time", timeNud), R("Name", InlineOpt(nameEn, nameBox)) });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                Commit(actions, item.Board);
                item.Name        = nameEn.IsChecked == true ? nameBox.Text : null;
                item.ReactorTime = (int)timeNud.Value!;
            }
        }

        // ── Rope / Ladder ─────────────────────────────────────────────────

        public static async Task ShowRopeEditorAsync(RopeAnchor item, Window? owner)
        {
            var xNud    = Nud(-9999, 9999, item.X);
            var yNud    = Nud(-9999, 9999, item.Y);
            var ufBox   = new CheckBox { Content = "UF",     IsChecked = item.ParentRope.uf };
            var ladBox  = new CheckBox { Content = "Ladder", IsChecked = item.ParentRope.ladder };

            bool ok = await ShowDialogAsync(owner, "Edit Rope/Ladder",
                HaCreatorStateManager.CreateItemDescription(item),
                new[] { R("X", xNud), R("Y", yNud), R("", ufBox), R("", ladBox) });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                Commit(actions, item.Board);
                item.ParentRope.uf = ufBox.IsChecked == true;
                if (item.ParentRope.ladder != (ladBox.IsChecked == true))
                {
                    item.ParentRope.OnUserTouchedLadder();
                    item.ParentRope.ladder = ladBox.IsChecked == true;
                }
            }
        }

        // ── Tooltip ───────────────────────────────────────────────────────

        public static async Task ShowTooltipEditorAsync(ToolTipInstance item, Window? owner)
        {
            var xNud = Nud(-9999, 9999, item.X);
            var yNud = Nud(-9999, 9999, item.Y);
            var (titleEn, titleBox) = OptText(item.Title);
            var (descEn,  descBox)  = OptText(item.Desc);

            bool ok = await ShowDialogAsync(owner, "Edit Tooltip",
                HaCreatorStateManager.CreateItemDescription(item),
                new[] { R("X", xNud), R("Y", yNud), R("Title", InlineOpt(titleEn, titleBox)), R("Desc", InlineOpt(descEn, descBox)) });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                Commit(actions, item.Board);
                item.Title = titleEn.IsChecked == true ? titleBox.Text : null;
                item.Desc  = descEn.IsChecked  == true ? descBox.Text  : null;
            }
        }

        // ── Foothold ──────────────────────────────────────────────────────

        public static async Task ShowFootholdEditorAsync(FootholdLine[] footholds, Window? owner)
        {
            if (footholds.Length == 0) return;

            // Detect indeterminate: all same vs. mixed
            bool? cantThrough = AllSame(footholds, f => f.CantThrough);
            bool? forbidFall  = AllSame(footholds, f => f.ForbidFallDown);
            int?  sharedForce = AllSameNullable(footholds, f => f.Force);
            int?  sharedPiece = AllSameNullable(footholds, f => f.Piece);
            bool  forceIndet  = sharedForce == null && footholds.Any(f => f.Force != null) && footholds.Any(f => f.Force == null);
            bool  pieceIndet  = sharedPiece == null && footholds.Any(f => f.Piece != null) && footholds.Any(f => f.Piece == null);

            var (forceEn, forceNud)   = OptNud(footholds[0].Force);
            var (pieceEn, pieceNud)   = OptNud(footholds[0].Piece);
            var cantBox  = new CheckBox { Content = "Can't Through",   IsChecked = cantThrough };
            var forbidBox= new CheckBox { Content = "Forbid Fall Down", IsChecked = forbidFall  };

            bool ok = await ShowDialogAsync(owner, "Edit Foothold",
                $"Editing {footholds.Length} foothold(s)",
                new[] { R("Force",         InlineOpt(forceEn, forceNud)),
                        R("Piece",         InlineOpt(pieceEn, pieceNud)),
                        R("", cantBox), R("", forbidBox) });

            if (!ok) return;
            lock (footholds[0].Board.ParentControl)
            {
                if (!forceIndet)
                {
                    int? f = forceEn.IsChecked == true ? (int?)forceNud.Value : null;
                    foreach (var fl in footholds) fl.Force = f;
                }
                if (!pieceIndet)
                {
                    int? p = pieceEn.IsChecked == true ? (int?)pieceNud.Value : null;
                    foreach (var fl in footholds) fl.Piece = p;
                }
                if (cantThrough.HasValue)
                    foreach (var fl in footholds) fl.CantThrough = cantBox.IsChecked == true;
                if (forbidFall.HasValue)
                    foreach (var fl in footholds) fl.ForbidFallDown = forbidBox.IsChecked == true;
            }
        }

        // ── Object ────────────────────────────────────────────────────────

        public static async Task ShowObjectEditorAsync(ObjectInstance item, Window? owner)
        {
            var xNud = Nud(-9999, 9999, item.X);
            var yNud = Nud(-9999, 9999, item.Y);
            var zNud = Nud(-9999, 9999, item.Z);
            var flipBox = new CheckBox { Content = "Flip",    IsChecked = item.Flip };
            var rBox    = new CheckBox { Content = "r",       IsChecked = (bool?)item.r };
            var hideBox = new CheckBox { Content = "Hide",    IsChecked = item.hide.HasValue ? (bool?)item.hide.Value : false };
            var flowBox = new CheckBox { Content = "Flow",    IsChecked = item.flow };
            var (nameEn, nameBox) = OptText(item.Name);
            var (rxEn, rxNud)     = OptNud(item.rx);
            var (ryEn, ryNud)     = OptNud(item.ry);
            var (cxEn, cxNud)     = OptNud(item.cx);
            var (cyEn, cyNud)     = OptNud(item.cy);
            var (tagsEn, tagsBox) = OptText(item.tags);

            // Quest info (simplified: editable list of QuestId - State strings)
            var questItems = new List<ObjectInstanceQuest>(item.QuestInfo ?? new List<ObjectInstanceQuest>());
            var questListBox = new ListBox { Height = 80 };
            foreach (var q in questItems) questListBox.Items.Add(q.ToString());
            var questEn = new CheckBox { Content = "Quest Info", IsChecked = item.QuestInfo != null && item.QuestInfo.Count > 0 };

            var addQuestBtn = new Button { Content = "+", Width = 28, Margin = new Thickness(2) };
            var removeQuestBtn = new Button { Content = "−", Width = 28, Margin = new Thickness(2) };
            var questBtnsRow = new StackPanel { Orientation = Orientation.Horizontal, Children = { addQuestBtn, removeQuestBtn } };

            addQuestBtn.Click += async (_, _) =>
            {
                var result = await ShowQuestInputAsync(owner);
                if (result.HasValue)
                {
                    questItems.Add(result.Value);
                    questListBox.Items.Add(result.Value.ToString());
                }
            };
            removeQuestBtn.Click += (_, _) =>
            {
                int idx = questListBox.SelectedIndex;
                if (idx >= 0) { questItems.RemoveAt(idx); questListBox.Items.RemoveAt(idx); }
            };

            bool ok = await ShowDialogAsync(owner, "Edit Object",
                HaCreatorStateManager.CreateItemDescription(item),
                new[]
                {
                    R("X", xNud), R("Y", yNud), R("Z", zNud),
                    R("", flipBox), R("", rBox), R("", hideBox), R("", flowBox),
                    R("Name", InlineOpt(nameEn, nameBox)),
                    R("rx", InlineOpt(rxEn, rxNud)), R("ry", InlineOpt(ryEn, ryNud)),
                    R("cx", InlineOpt(cxEn, cxNud)), R("cy", InlineOpt(cyEn, cyNud)),
                    R("Tags", InlineOpt(tagsEn, tagsBox)),
                    R("", questEn), R("Quest", questListBox), R("", questBtnsRow)
                });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                if (item.Z != (int)zNud.Value!)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zNud.Value!));
                    item.Z = (int)zNud.Value!;
                    item.Board.BoardItems.Sort();
                }
                Commit(actions, item.Board);
                item.Flip = flipBox.IsChecked == true;
                item.r    = rBox.IsChecked    == true;
                item.hide = hideBox.IsChecked == true;
                item.flow = flowBox.IsChecked == true;
                item.Name = nameEn.IsChecked  == true ? nameBox.Text : null;
                item.rx   = rxEn.IsChecked    == true ? (int?)rxNud.Value : null;
                item.ry   = ryEn.IsChecked    == true ? (int?)ryNud.Value : null;
                item.cx   = cxEn.IsChecked    == true ? (int?)cxNud.Value : null;
                item.cy   = cyEn.IsChecked    == true ? (int?)cyNud.Value : null;
                item.tags = tagsEn.IsChecked  == true ? tagsBox.Text : null;
                item.QuestInfo = questEn.IsChecked == true && questItems.Count > 0 ? questItems : null;
            }
        }

        // ── Portal ────────────────────────────────────────────────────────

        public static async Task ShowPortalEditorAsync(PortalInstance item, Window? owner)
        {
            var xNud = Nud(-9999, 9999, item.X);
            var yNud = Nud(-9999, 9999, item.Y);

            // Portal type combo
            var ptCombo = new ComboBox();
            for (int i = 0; i < Program.InfoManager.PortalEditor_TypeById.Count; i++)
            {
                try { ptCombo.Items.Add(PortalTypeExtensions.GetFriendlyName(Program.InfoManager.PortalEditor_TypeById[i])); }
                catch (KeyNotFoundException) { ptCombo.Items.Add($"type_{i}"); }
            }
            ptCombo.SelectedIndex = Program.InfoManager.PortalIdByType.TryGetValue(item.pt, out int ptIdx) ? ptIdx : 0;

            var pnBox     = new TextBox { Text = item.pn ?? "", Width = 160 };
            var tmNud     = Nud(0, int.MaxValue, item.tm);
            var thisMapCb = new CheckBox { Content = "This Map", IsChecked = item.tm == item.Board.MapInfo.id };
            var tnBox     = new TextBox { Text = item.tn ?? "", Width = 160 };
            var scriptBox = new TextBox { Text = item.script ?? "", Width = 200 };
            var (delayEn, delayNud)   = OptNud(item.delay);
            var (hRangeEn, hRangeNud) = OptNud(item.hRange);
            var (vRangeEn, vRangeNud) = OptNud(item.vRange);
            var (hImpEn, hImpNud)     = OptNud(item.horizontalImpact);
            var vImpNud = Nud(-9999, 9999, item.verticalImpact ?? 0);
            var onlyOnce   = new CheckBox { Content = "Only Once",    IsChecked = item.onlyOnce   == true };
            var hideTooltip= new CheckBox { Content = "Hide Tooltip", IsChecked = item.hideTooltip == true };

            bool ok = await ShowDialogAsync(owner, "Edit Portal",
                HaCreatorStateManager.CreateItemDescription(item),
                new[]
                {
                    R("X", xNud), R("Y", yNud),
                    R("Type", ptCombo),
                    R("pn (name)", pnBox),
                    R("tm (map)", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { tmNud, thisMapCb } }),
                    R("tn (portal)", tnBox),
                    R("script", scriptBox),
                    R("delay", InlineOpt(delayEn, delayNud)),
                    R("hRange", InlineOpt(hRangeEn, hRangeNud)),
                    R("vRange", InlineOpt(vRangeEn, vRangeNud)),
                    R("hImpact", InlineOpt(hImpEn, hImpNud)),
                    R("vImpact", vImpNud),
                    R("", onlyOnce), R("", hideTooltip)
                });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                MoveItem(actions, item, (int)xNud.Value!, (int)yNud.Value!);
                Commit(actions, item.Board);
                int selIdx = ptCombo.SelectedIndex;
                if (selIdx >= 0 && selIdx < Program.InfoManager.PortalEditor_TypeById.Count)
                    item.pt = Program.InfoManager.PortalEditor_TypeById[selIdx];
                item.pn             = pnBox.Text;
                item.tm             = thisMapCb.IsChecked == true ? item.Board.MapInfo.id : (int)tmNud.Value!;
                item.tn             = tnBox.Text;
                item.script         = string.IsNullOrEmpty(scriptBox.Text) ? null : scriptBox.Text;
                item.delay          = delayEn.IsChecked  == true ? (int?)delayNud.Value  : null;
                item.hRange         = hRangeEn.IsChecked == true ? (int?)hRangeNud.Value : null;
                item.vRange         = vRangeEn.IsChecked == true ? (int?)vRangeNud.Value : null;
                item.horizontalImpact = hImpEn.IsChecked == true ? (int?)hImpNud.Value   : null;
                item.verticalImpact = (int)vImpNud.Value!;
                item.onlyOnce       = onlyOnce.IsChecked    == true;
                item.hideTooltip    = hideTooltip.IsChecked == true;
            }
        }

        // ── Background ────────────────────────────────────────────────────

        public static async Task ShowBackgroundEditorAsync(BackgroundInstance item, Window? owner)
        {
            var xNud   = Nud(-9999, 9999, item.BaseX);
            var yNud   = Nud(-9999, 9999, item.BaseY);
            var zNud   = Nud(-9999, 9999, item.Z); zNud.IsEnabled = item.Z != -1;
            var alpNud = Nud(0, 255, item.a);
            var frontCb= new CheckBox { Content = "Front", IsChecked = item.front };
            var rxNud  = Nud(-999, 999, item.rx);
            var ryNud  = Nud(-999, 999, item.ry);
            var cxNud  = Nud(0, 9999, Math.Max(0, item.cx));
            var cyNud  = Nud(0, 9999, Math.Max(0, item.cy));

            var typeCombo = new ComboBox();
            foreach (BackgroundType bt in Enum.GetValues(typeof(BackgroundType)))
                typeCombo.Items.Add(bt.GetFriendlyName());
            typeCombo.SelectedIndex = (int)item.type;

            var screenModeCombo = new ComboBox();
            var rrValues = Enum.GetValues(typeof(RenderResolution)).Cast<RenderResolution>().ToArray();
            foreach (var rr in rrValues) screenModeCombo.Items.Add(rr.ToReadableString());
            int smIdx = Array.FindIndex(rrValues, r => (int)r == item.screenMode);
            screenModeCombo.SelectedIndex = smIdx >= 0 ? smIdx : 0;

            bool ok = await ShowDialogAsync(owner, "Edit Background",
                HaCreatorStateManager.CreateItemDescription(item),
                new[]
                {
                    R("X", xNud), R("Y", yNud), R("Z", zNud),
                    R("Type", typeCombo),
                    R("Alpha", alpNud),
                    R("", frontCb),
                    R("rx (parallax X)", rxNud), R("ry (parallax Y)", ryNud),
                    R("cx (tile X)", cxNud), R("cy (tile Y)", cyNud),
                    R("Screen Mode", screenModeCombo)
                });

            if (!ok) return;
            lock (item.Board.ParentControl)
            {
                var actions = new List<UndoRedoAction>();
                // Background uses BaseX/BaseY directly
                if (item.BaseX != (int)xNud.Value! || item.BaseY != (int)yNud.Value!)
                {
                    actions.Add(UndoRedoManager.ItemMoved(item, new XnaPoint(item.BaseX, item.BaseY), new XnaPoint((int)xNud.Value!, (int)yNud.Value!)));
                    item.Move((int)xNud.Value!, (int)yNud.Value!);
                }
                if (zNud.IsEnabled && item.Z != (int)zNud.Value!)
                {
                    actions.Add(UndoRedoManager.ItemZChanged(item, item.Z, (int)zNud.Value!));
                    item.Z = (int)zNud.Value!;
                    item.Board.BoardItems.Sort();
                }
                Commit(actions, item.Board);
                item.type       = (BackgroundType)Math.Max(0, typeCombo.SelectedIndex);
                item.a          = (int)alpNud.Value!;
                item.front      = frontCb.IsChecked == true;
                item.rx         = (int)rxNud.Value!;
                item.ry         = (int)ryNud.Value!;
                item.cx         = (int)cxNud.Value!;
                item.cy         = (int)cyNud.Value!;
                int smSel = screenModeCombo.SelectedIndex;
                item.screenMode = smSel >= 0 && smSel < rrValues.Length ? (int)rrValues[smSel] : item.screenMode;
            }
        }

        // ── Quest input sub-dialog ────────────────────────────────────────

        private static async Task<ObjectInstanceQuest?> ShowQuestInputAsync(Window? owner)
        {
            var idNud = Nud(0, 99999, 0);
            var stateCombo = new ComboBox();
            foreach (QuestStateType s in Enum.GetValues(typeof(QuestStateType)))
                stateCombo.Items.Add(s.ToString());
            stateCombo.SelectedIndex = 0;

            bool ok = await ShowDialogAsync(owner, "Add Quest Requirement", "",
                new[] { R("Quest ID", idNud), R("State", stateCombo) });

            if (!ok) return null;
            return new ObjectInstanceQuest((int)idNud.Value!, (QuestStateType)stateCombo.SelectedIndex);
        }

        // ── UI helpers ────────────────────────────────────────────────────

        private static async Task<bool> ShowDialogAsync(Window? owner, string title, string description, (string Label, Control Ctrl)[] rows)
        {
            bool result = false;
            var dlg = new Window
            {
                Title = title,
                Width = 420,
                MinHeight = 200,
                MaxHeight = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true,
                SizeToContent = SizeToContent.Height
            };

            var gridRows = new Grid();
            int rowIndex = 0;

            if (!string.IsNullOrWhiteSpace(description))
            {
                gridRows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var pathLbl = new TextBlock
                {
                    Text = description,
                    Foreground = Brushes.Gray,
                    FontSize = 10,
                    Margin = new Thickness(4, 2, 4, 6),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(pathLbl, rowIndex++);
                gridRows.Children.Add(pathLbl);
            }

            foreach (var (label, ctrl) in rows)
            {
                gridRows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Control rowCtrl = string.IsNullOrEmpty(label)
                    ? ctrl
                    : (Control)new Grid
                    {
                        Margin = new Thickness(0, 1, 0, 1),
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(new GridLength(110, GridUnitType.Pixel)),
                            new ColumnDefinition(new GridLength(1, GridUnitType.Star))
                        },
                        Children =
                        {
                            SetGridCol(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), TextWrapping = TextWrapping.Wrap }, 0),
                            SetGridCol(ctrl, 1)
                        }
                    };
                Grid.SetRow(rowCtrl, rowIndex++);
                gridRows.Children.Add(rowCtrl);
            }

            // Buttons
            gridRows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var okBtn     = new Button { Content = "OK",     Width = 80, Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(4) };
            var btnRow    = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { okBtn, cancelBtn }
            };
            Grid.SetRow(btnRow, rowIndex);
            gridRows.Children.Add(btnRow);

            okBtn.Click     += (_, _) => { result = true;  dlg.Close(); };
            cancelBtn.Click += (_, _) => { result = false; dlg.Close(); };

            dlg.Content = new ScrollViewer
            {
                Content = new Border { Padding = new Thickness(8), Child = gridRows },
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            };

            if (owner != null) await dlg.ShowDialog(owner);
            else dlg.Show();

            return result;
        }

        private static T SetGridCol<T>(T ctrl, int col) where T : Control { Grid.SetColumn(ctrl, col); return ctrl; }

        private static (string Label, Control Ctrl) R(string label, Control ctrl) => (label, ctrl);

        private static NumericUpDown Nud(decimal min, decimal max, decimal? val) =>
            new NumericUpDown { Minimum = min, Maximum = max, Value = val ?? 0, Margin = new Thickness(0), Width = 160 };

        private static (CheckBox cb, NumericUpDown nud) OptNud(int? val)
        {
            var cb  = new CheckBox { IsChecked = val.HasValue, VerticalAlignment = VerticalAlignment.Center };
            var nud = new NumericUpDown { Minimum = -99999, Maximum = 99999, Value = val ?? 0, IsEnabled = val.HasValue, Width = 120 };
            cb.IsCheckedChanged += (_, _) => nud.IsEnabled = cb.IsChecked == true;
            return (cb, nud);
        }

        private static (CheckBox cb, TextBox box) OptText(string? val)
        {
            var cb  = new CheckBox { IsChecked = val != null, VerticalAlignment = VerticalAlignment.Center };
            var box = new TextBox  { Text = val ?? "", IsEnabled = val != null, Width = 140 };
            cb.IsCheckedChanged += (_, _) => box.IsEnabled = cb.IsChecked == true;
            return (cb, box);
        }

        private static StackPanel InlineOpt(CheckBox cb, Control ctrl) =>
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                Children = { cb, ctrl }
            };

        private static void MoveItem(List<UndoRedoAction> actions, BoardItem item, int newX, int newY)
        {
            if (newX != item.X || newY != item.Y)
            {
                actions.Add(UndoRedoManager.ItemMoved(item, new XnaPoint(item.X, item.Y), new XnaPoint(newX, newY)));
                item.Move(newX, newY);
            }
        }

        private static void Commit(List<UndoRedoAction> actions, Board board)
        {
            if (actions.Count > 0)
                board.UndoRedoMan.AddUndoBatch(actions);
        }

        private static bool? AllSame<T>(T[] items, Func<T, bool> sel)
        {
            bool first = sel(items[0]);
            return items.All(x => sel(x) == first) ? (bool?)first : null;
        }

        private static int? AllSameNullable<T>(T[] items, Func<T, int?> sel)
        {
            int? first = sel(items[0]);
            return items.All(x => sel(x) == first) ? first : null;
        }
    }
}
