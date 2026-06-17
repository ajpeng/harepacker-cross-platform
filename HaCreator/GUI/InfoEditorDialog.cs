/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.Wz;
using MapleLib.WzLib.WzStructure;
using MapleLib.WzLib.WzStructure.Data;
using MapleLib.WzLib.WzStructure.Data.MapStructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace HaCreator.GUI
{
    public static class InfoEditorDialog
    {
        // Called from within a board lock — queue onto UI thread to avoid deadlock
        public static void DispatchAsync(Board board, MapInfo info, MultiBoard multiBoard, TabItem tabItem, Window? owner)
        {
            Dispatcher.UIThread.Post(async () =>
                await ShowAsync(board, info, multiBoard, tabItem, owner));
        }

        private static async Task ShowAsync(Board board, MapInfo info, MultiBoard multiBoard, TabItem tabItem, Window? owner)
        {
            // ── Tab 1: General ────────────────────────────────────────

            string idText = info.mapType switch
            {
                MapType.CashShopPreview => "CashShopPreview",
                MapType.MapLogin        => "MapLogin",
                MapType.ITCPreview      => "ITCPreview",
                _                       => info.id == -1 ? "<NEW>" : info.id.ToString()
            };

            bool isRegular = info.mapType == MapType.RegularMap;
            bool hasBgm    = info.mapType != MapType.CashShopPreview && info.mapType != MapType.ITCPreview;

            var tbName     = new TextBox { Text = info.strMapName,      IsEnabled = isRegular };
            var tbStreet   = new TextBox { Text = info.strStreetName,   IsEnabled = isRegular };
            var tbCategory = new TextBox { Text = info.strCategoryName, IsEnabled = isRegular };

            var sortedBGMs  = Program.InfoManager.BGMs.Keys.OrderBy(k => k).ToList();
            var sortedMarks = Program.InfoManager.MapMarks.Keys.OrderBy(k => k).ToList();
            var cbxBGM  = new ComboBox { ItemsSource = sortedBGMs,  SelectedItem = info.bgm,     IsEnabled = hasBgm, MinWidth = 220 };
            var cbxMark = new ComboBox { ItemsSource = sortedMarks, SelectedItem = info.mapMark, IsEnabled = hasBgm, MinWidth = 180 };

            var cbCannotReturn = new CheckBox { Content = "Cannot Return (same map)", IsChecked = info.returnMap == info.id };
            var nudReturn = new NumericUpDown
            {
                Value = info.returnMap, Minimum = 0, Maximum = 999999999,
                FormatString = "F0", Width = 130,
                IsEnabled = info.returnMap != info.id
            };
            cbCannotReturn.IsCheckedChanged += (_, _) => nudReturn.IsEnabled = !(cbCannotReturn.IsChecked == true);

            var cbReturnHere = new CheckBox { Content = "Return Here (999999999)", IsChecked = info.forcedReturn == 999999999 };
            var nudForced = new NumericUpDown
            {
                Value = info.forcedReturn == 999999999 ? 0 : info.forcedReturn,
                Minimum = 0, Maximum = 999999999, FormatString = "F0", Width = 130,
                IsEnabled = info.forcedReturn != 999999999
            };
            cbReturnHere.IsCheckedChanged += (_, _) => nudForced.IsEnabled = !(cbReturnHere.IsChecked == true);

            var nudMobRate = new NumericUpDown
            {
                Value = (decimal)info.mobRate, Minimum = 0, Maximum = 10,
                Increment = 0.1m, FormatString = "F2", Width = 100
            };
            var nudSizeX = new NumericUpDown { Value = board.MapSize.X, Minimum = 0, Maximum = 9999999, FormatString = "F0", Width = 100 };
            var nudSizeY = new NumericUpDown { Value = board.MapSize.Y, Minimum = 0, Maximum = 9999999, FormatString = "F0", Width = 100 };

            var tabGeneral = MakeTab("General", new StackPanel
            {
                Margin = new Avalonia.Thickness(10), Spacing = 6,
                Children =
                {
                    new TextBlock { Text = $"Map ID: {idText}", Margin = new Avalonia.Thickness(0,0,0,4) },
                    LR("Map Name:",      tbName),
                    LR("Street Name:",   tbStreet),
                    LR("Category:",      tbCategory),
                    LR("BGM:",           cbxBGM),
                    LR("Map Mark:",      cbxMark),
                    LR("Return Map:",    Row(cbCannotReturn, nudReturn)),
                    LR("Forced Return:", Row(cbReturnHere,  nudForced)),
                    LR("Mob Rate:",      nudMobRate),
                    LR("Map Width:",     nudSizeX),
                    LR("Map Height:",    nudSizeY),
                }
            });

            // ── Tab 2: Flags ──────────────────────────────────────────

            var cbCloud       = BoolCb("Cloud",              info.cloud);
            var cbSwim        = BoolCb("Swim",               info.swim);
            var cbHideMini    = BoolCb("Hide Minimap",       info.hideMinimap);
            var cbTown        = BoolCb("Town",               info.town);
            var cbFly         = MBCb("Fly",                 info.fly);
            var cbNoMapCmd    = MBCb("No Map Command",      info.noMapCmd);
            var cbPartyOnly   = MBCb("Party Only",          info.partyOnly);
            var cbReactorShuf = MBCb("Reactor Shuffle",     info.reactorShuffle);
            var cbPersonal    = MBCb("Personal Shop",       info.personalShop);
            var cbEntrusted   = MBCb("Entrusted Shop",      info.entrustedShop);
            var cbExpedOnly   = MBCb("Expedition Only",     info.expeditionOnly);
            var cbSnow        = MBCb("Snow",                info.snow);
            var cbRain        = MBCb("Rain",                info.rain);
            var cbNoRegen     = MBCb("No Regen Map",        info.noRegenMap);
            var cbBlockPBoss  = MBCb("Block PBoss Change",  info.blockPBossChange);
            var cbEverlast    = MBCb("Everlast",            info.everlast);
            var cbDmgFree     = MBCb("Damage Check Free",   info.damageCheckFree);
            var cbScrollDis   = MBCb("Scroll Disable",      info.scrollDisable);
            var cbNeedFly     = MBCb("Need Skill for Fly",  info.needSkillForFly);
            var cbZakum2      = MBCb("Zakum2 Hack",         info.zakum2Hack);
            var cbAllMove     = MBCb("All Move Check",      info.allMoveCheck);
            var cbVRLimit     = MBCb("VR Limit",            info.VRLimit);
            var cbMirrorBot   = MBCb("Mirror Bottom",       info.mirror_Bottom);
            var cbMiniOnOff   = MBCb("Minimap On/Off",      info.miniMapOnOff);

            var flagsPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var cb in new Control[]
            {
                cbCloud, cbSwim, cbHideMini, cbTown, cbFly, cbNoMapCmd,
                cbPartyOnly, cbReactorShuf, cbPersonal, cbEntrusted,
                cbExpedOnly, cbSnow, cbRain, cbNoRegen, cbBlockPBoss,
                cbEverlast, cbDmgFree, cbScrollDis, cbNeedFly, cbZakum2,
                cbAllMove, cbVRLimit, cbMirrorBot, cbMiniOnOff
            })
            {
                cb.Margin  = new Avalonia.Thickness(6, 3);
                ((CheckBox)cb).MinWidth = 185;
                flagsPanel.Children.Add(cb);
            }

            var tabFlags = MakeTab("Flags", flagsPanel);

            // ── Tab 3: Optional Fields ────────────────────────────────

            var (rowTimeLimit,   getTimeLimit)  = OptInt("Time Limit (s)",        info.timeLimit,   0, 999999);
            var (rowLvLimit,     getLvLimit)     = OptInt("Level Limit",           info.lvLimit,     0, 999);
            var (rowLvForce,     getLvForce)     = OptInt("Level Force Move",      info.lvForceMove, 0, 999);
            var (rowOnFirst,     getOnFirst)     = OptStr("On First User Enter",   info.onFirstUserEnter);
            var (rowOnUser,      getOnUser)      = OptStr("On User Enter",         info.onUserEnter);
            var (rowEffect,      getEffect)      = OptStr("Effect",                info.effect);
            var (rowMoveLimit,   getMoveLimit)   = OptInt("Move Limit",            info.moveLimit,   0, 999999);
            var (rowDropExpire,  getDropExpire)  = OptInt("Drop Expire (s)",       info.dropExpire,  0, 999999);
            var (rowDropRate,    getDropRate)    = OptFloat("Drop Rate",           info.dropRate,   0f, 100f);
            var (rowRecovery,    getRecovery)    = OptFloat("Recovery Rate",       info.recovery,   0f, 100f);
            var (rowReactorName, getReactorName) = OptStr("Reactor Shuffle Name", info.reactorShuffleName);
            var (rowFs,          getFs)          = OptFloat("Slip Speed (fs)",     info.fs,         0f, 10f);
            var (rowMapDesc,     getMapDesc)     = OptStr("Map Desc",              info.mapDesc);
            var (rowMapName2,    getMapName2)    = OptStr("Map Name (wz key)",     info.mapName);
            var (rowStreetName2, getStreetName2) = OptStr("Street Name (wz key)",  info.streetName);

            // FieldType combo — parallel list preserves order
            var fieldTypeValues  = Enum.GetValues<FieldType>().OrderBy(ft => (int)ft).ToList();
            var fieldTypeStrings = fieldTypeValues.Select(ft => ft.ToReadableString()).ToList();
            fieldTypeStrings.Insert(0, "(none)");
            var cbxFieldType = new ComboBox { ItemsSource = fieldTypeStrings, SelectedIndex = 0, MinWidth = 200 };
            if (info.fieldType.HasValue)
            {
                string fts = info.fieldType.Value.ToReadableString();
                int idx = fieldTypeStrings.IndexOf(fts);
                if (idx >= 0) cbxFieldType.SelectedIndex = idx;
            }
            var cbFieldTypeEnable = new CheckBox { IsChecked = info.fieldType.HasValue };
            cbxFieldType.IsEnabled = info.fieldType.HasValue;
            cbFieldTypeEnable.IsCheckedChanged += (_, _) => cbxFieldType.IsEnabled = cbFieldTypeEnable.IsChecked == true;

            var tabOptional = MakeTab("Optional", new StackPanel
            {
                Margin = new Avalonia.Thickness(10), Spacing = 6,
                Children =
                {
                    rowTimeLimit, rowLvLimit, rowLvForce,
                    rowOnFirst, rowOnUser, rowEffect,
                    rowMoveLimit, rowDropExpire, rowDropRate, rowRecovery,
                    rowReactorName, rowFs, rowMapDesc, rowMapName2, rowStreetName2,
                    LR("Field Type:", Row(cbFieldTypeEnable, cbxFieldType)),
                }
            });

            // ── Tab 4: Advanced ───────────────────────────────────────

            // Field Limit checkboxes
            var fieldLimitCbs = new Dictionary<FieldLimitType, CheckBox>();
            var flWrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (FieldLimitType fl in Enum.GetValues<FieldLimitType>().OrderBy(f => (int)f))
            {
                bool isSet = FieldLimitTypeExtension.Check((int)fl, info.fieldLimit);
                var flCb = new CheckBox
                {
                    Content = fl.ToString().Replace("_", " "),
                    IsChecked = isSet,
                    Margin = new Avalonia.Thickness(4, 2),
                    MinWidth = 230
                };
                fieldLimitCbs[fl] = flCb;
                flWrap.Children.Add(flCb);
            }

            // decHP / decInterval / protectItem
            var (rowDecHp,       getDecHp)       = OptInt("HP Decrease",        info.decHP,       0, 999999);
            var (rowDecInt,      getDecInt)       = OptInt("HP Dec Interval",    info.decInterval, 0, 999999);
            var (rowCreateMobI,  getCreateMobI)   = OptInt("Create Mob Interval",info.createMobInterval, 0, 999999);
            var (rowFixedMobCap, getFixedMobCap)  = OptInt("Fixed Mob Capacity", info.fixedMobCapacity,  0, 9999);

            var protectItems = new ObservableCollection<string>(
                (info.protectItem ?? new List<int>()).Select(i => i.ToString()));
            var lstProtect       = new ListBox { ItemsSource = protectItems, Height = 80 };
            var cbProtectEnable  = new CheckBox { Content = "Protect Items (cold)",  IsChecked = info.protectItem != null };
            lstProtect.IsEnabled = info.protectItem != null;
            cbProtectEnable.IsCheckedChanged += (_, _) => lstProtect.IsEnabled = cbProtectEnable.IsChecked == true;
            var btnProtectAdd    = new Button { Content = "Add" };
            var btnProtectRemove = new Button { Content = "Remove" };
            btnProtectAdd.Click += async (_, _) =>
            {
                string? id = await ShowInputAsync("Add Protect Item", "Enter item ID:", owner);
                if (id != null && id.Length > 0) protectItems.Add(id);
            };
            btnProtectRemove.Click += (_, _) =>
            {
                if (lstProtect.SelectedIndex >= 0) protectItems.RemoveAt(lstProtect.SelectedIndex);
            };

            var allowedItemsList = new ObservableCollection<string>(
                (info.allowedItem ?? new List<int>()).Select(i => i.ToString()));
            var lstAllowed       = new ListBox { ItemsSource = allowedItemsList, Height = 80 };
            var cbAllowedEnable  = new CheckBox { Content = "Allowed Items", IsChecked = info.allowedItem != null };
            lstAllowed.IsEnabled = info.allowedItem != null;
            cbAllowedEnable.IsCheckedChanged += (_, _) => lstAllowed.IsEnabled = cbAllowedEnable.IsChecked == true;
            var btnAllowedAdd    = new Button { Content = "Add" };
            var btnAllowedRemove = new Button { Content = "Remove" };
            btnAllowedAdd.Click += async (_, _) =>
            {
                string? id = await ShowInputAsync("Add Allowed Item", "Enter item ID:", owner);
                if (id != null && id.Length > 0) allowedItemsList.Add(id);
            };
            btnAllowedRemove.Click += (_, _) =>
            {
                if (lstAllowed.SelectedIndex >= 0) allowedItemsList.RemoveAt(lstAllowed.SelectedIndex);
            };

            // Timed Mob
            TimeMob? tMob      = info.timeMob;
            var cbTimeMob      = new CheckBox { Content = "Timed Mob", IsChecked = tMob.HasValue };
            var (rowTMobStart, getTMobStart) = OptInt("Start Hour", tMob?.startHour, 0, 23);
            var (rowTMobEnd,   getTMobEnd)   = OptInt("End Hour",   tMob?.endHour,   0, 23);
            var nudTimeMobId   = new NumericUpDown { Value = tMob?.id ?? 0, Minimum = 0, Maximum = 9999999, FormatString = "F0", Width = 130 };
            var tbTimeMobMsg   = new TextBox { Text = tMob?.message?.Replace(@"\n", "\n") ?? "", AcceptsReturn = true, Height = 60 };
            var spTimeMob      = new StackPanel
            {
                Spacing = 4, IsEnabled = tMob.HasValue,
                Children = { rowTMobStart, rowTMobEnd, LR("Mob ID:", nudTimeMobId), LR("Message:", tbTimeMobMsg) }
            };
            cbTimeMob.IsCheckedChanged += (_, _) => spTimeMob.IsEnabled = cbTimeMob.IsChecked == true;

            // Auto Lie Detector
            AutoLieDetector? ald = info.autoLieDetector;
            var cbLieDetect  = new CheckBox { Content = "Auto Lie Detector", IsChecked = ald != null };
            var nudLieStart  = new NumericUpDown { Value = ald?.startHour ?? 0,  Minimum = 0, Maximum = 23,     FormatString = "F0", Width = 80 };
            var nudLieEnd    = new NumericUpDown { Value = ald?.endHour   ?? 0,  Minimum = 0, Maximum = 23,     FormatString = "F0", Width = 80 };
            var nudLieInt    = new NumericUpDown { Value = ald?.interval  ?? 1,  Minimum = 0, Maximum = 999999, FormatString = "F0", Width = 100 };
            var nudLieProp   = new NumericUpDown { Value = ald?.prop      ?? 0,  Minimum = 0, Maximum = 999999, FormatString = "F0", Width = 100 };
            var spLieDet     = new StackPanel
            {
                Spacing = 4, IsEnabled = ald != null,
                Children = { LR("Start Hour:", nudLieStart), LR("End Hour:", nudLieEnd),
                             LR("Interval:",   nudLieInt),   LR("Prop:",     nudLieProp) }
            };
            cbLieDetect.IsCheckedChanged += (_, _) => spLieDet.IsEnabled = cbLieDetect.IsChecked == true;

            // Help text
            var cbHelpEnable = new CheckBox { Content = "Help Text", IsChecked = info.help != null };
            var tbHelp       = new TextBox  { Text = info.help?.Replace(@"\n", "\n") ?? "", IsEnabled = info.help != null,
                                             AcceptsReturn = true, Height = 80 };
            cbHelpEnable.IsCheckedChanged += (_, _) => tbHelp.IsEnabled = cbHelpEnable.IsChecked == true;

            var tabAdvanced = MakeTab("Advanced", new StackPanel
            {
                Margin = new Avalonia.Thickness(10), Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Field Limit Flags:", FontWeight = Avalonia.Media.FontWeight.Bold },
                    new Border
                    {
                        BorderBrush = Avalonia.Media.Brushes.Gray,
                        BorderThickness = new Avalonia.Thickness(1),
                        Padding = new Avalonia.Thickness(4),
                        Margin  = new Avalonia.Thickness(0, 0, 0, 6),
                        Child   = new ScrollViewer { Height = 150, Content = flWrap }
                    },
                    rowDecHp, rowDecInt, rowCreateMobI, rowFixedMobCap,
                    cbProtectEnable,
                    Row(lstProtect, new StackPanel { Spacing = 4, Children = { btnProtectAdd, btnProtectRemove } }),
                    cbAllowedEnable,
                    Row(lstAllowed, new StackPanel { Spacing = 4, Children = { btnAllowedAdd, btnAllowedRemove } }),
                    new Separator { Margin = new Avalonia.Thickness(0, 4) },
                    cbTimeMob, spTimeMob,
                    new Separator { Margin = new Avalonia.Thickness(0, 4) },
                    cbLieDetect, spLieDet,
                    new Separator { Margin = new Avalonia.Thickness(0, 4) },
                    cbHelpEnable, tbHelp,
                }
            });

            // ── Build window ──────────────────────────────────────────

            var tabCtrl = new TabControl();
            tabCtrl.Items.Add(tabGeneral);
            tabCtrl.Items.Add(tabFlags);
            tabCtrl.Items.Add(tabOptional);
            tabCtrl.Items.Add(tabAdvanced);

            var btnOK     = new Button { Content = "OK",     HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
            var btnCancel = new Button { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right };
            var btnPanel  = new StackPanel
            {
                Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Avalonia.Thickness(6), Spacing = 6,
                Children = { btnOK, btnCancel }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            mainGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(tabCtrl,  0);
            Grid.SetRow(btnPanel, 1);
            mainGrid.Children.Add(tabCtrl);
            mainGrid.Children.Add(btnPanel);

            bool confirmed = false;
            var win = new Window
            {
                Title = "Map Properties",
                Width = 620, Height = 560,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = mainGrid
            };

            btnOK.Click     += (_, _) => { confirmed = true; win.Close(); };
            btnCancel.Click += (_, _) => win.Close();

            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
            if (!confirmed) return;

            // ── Apply results (inside board lock) ─────────────────────

            lock (multiBoard)
            {
                if (hasBgm)
                {
                    info.bgm     = (string?)cbxBGM.SelectedItem  ?? info.bgm;
                    info.mapMark = (string?)cbxMark.SelectedItem ?? info.mapMark;
                }
                if (isRegular)
                {
                    info.strMapName      = tbName.Text     ?? info.strMapName;
                    info.strStreetName   = tbStreet.Text   ?? info.strStreetName;
                    info.strCategoryName = tbCategory.Text ?? info.strCategoryName;
                    tabItem.Header = MapLoader.GetFormattedMapNameForTabItem(info.id, info.strStreetName, info.strMapName);
                    if (tabItem.Tag is TabItemContainer tic) tic.Text = info.strMapName;
                }

                info.returnMap    = cbCannotReturn.IsChecked == true ? info.id : (int)(nudReturn.Value ?? 999999999);
                info.forcedReturn = cbReturnHere.IsChecked  == true ? 999999999 : (int)(nudForced.Value ?? 0);
                info.mobRate      = (float)(nudMobRate.Value ?? 1.5m);

                var newSize = new XnaPoint((int)(nudSizeX.Value ?? board.MapSize.X), (int)(nudSizeY.Value ?? board.MapSize.Y));
                if (board.MapSize != newSize) board.MapSize = newSize;

                // Flags
                info.cloud        = cbCloud.IsChecked     == true;
                info.swim         = cbSwim.IsChecked      == true;
                info.hideMinimap  = cbHideMini.IsChecked  == true;
                info.town         = cbTown.IsChecked      == true;
                info.fly              = cbFly.IsChecked;
                info.noMapCmd         = cbNoMapCmd.IsChecked;
                info.partyOnly        = cbPartyOnly.IsChecked;
                info.reactorShuffle   = cbReactorShuf.IsChecked;
                info.personalShop     = cbPersonal.IsChecked;
                info.entrustedShop    = cbEntrusted.IsChecked;
                info.expeditionOnly   = cbExpedOnly.IsChecked;
                info.snow             = cbSnow.IsChecked;
                info.rain             = cbRain.IsChecked;
                info.noRegenMap       = cbNoRegen.IsChecked;
                info.blockPBossChange = cbBlockPBoss.IsChecked;
                info.everlast         = cbEverlast.IsChecked;
                info.damageCheckFree  = cbDmgFree.IsChecked;
                info.scrollDisable    = cbScrollDis.IsChecked;
                info.needSkillForFly  = cbNeedFly.IsChecked;
                info.zakum2Hack       = cbZakum2.IsChecked;
                info.allMoveCheck     = cbAllMove.IsChecked;
                info.VRLimit          = cbVRLimit.IsChecked;
                info.mirror_Bottom    = cbMirrorBot.IsChecked;
                info.miniMapOnOff     = cbMiniOnOff.IsChecked;

                // Optional
                info.timeLimit         = getTimeLimit();
                info.lvLimit           = getLvLimit();
                info.lvForceMove       = getLvForce();
                info.onFirstUserEnter  = getOnFirst();
                info.onUserEnter       = getOnUser();
                info.effect            = getEffect();
                info.moveLimit         = getMoveLimit();
                info.dropExpire        = getDropExpire();
                info.dropRate          = getDropRate();
                info.recovery          = getRecovery();
                info.reactorShuffleName = getReactorName();
                info.fs                = getFs();
                info.mapDesc           = getMapDesc();
                info.mapName           = getMapName2();
                info.streetName        = getStreetName2();

                info.fieldType = cbFieldTypeEnable.IsChecked == true && cbxFieldType.SelectedIndex > 0
                    ? fieldTypeValues[cbxFieldType.SelectedIndex - 1]
                    : (FieldType?)null;

                // Field Limit
                long flBits = 0;
                foreach (var kvp in fieldLimitCbs)
                    if (kvp.Value.IsChecked == true) flBits |= 1L << (int)kvp.Key;
                info.fieldLimit = flBits;

                // Advanced
                info.decHP             = getDecHp();
                info.decInterval       = getDecInt();
                info.createMobInterval = getCreateMobI();
                info.fixedMobCapacity  = getFixedMobCap();
                info.protectItem = cbProtectEnable.IsChecked == true
                    ? protectItems.Select(s => int.TryParse(s, out int id) ? id : -1).Where(id => id >= 0).ToList()
                    : null;
                info.allowedItem = cbAllowedEnable.IsChecked == true
                    ? allowedItemsList.Select(s => int.TryParse(s, out int id) ? id : -1).Where(id => id >= 0).ToList()
                    : null;
                info.timeMob = cbTimeMob.IsChecked == true
                    ? new TimeMob(getTMobStart(), getTMobEnd(), (int)(nudTimeMobId.Value ?? 0),
                                  tbTimeMobMsg.Text?.Replace("\n", @"\n") ?? "")
                    : (TimeMob?)null;
                info.autoLieDetector = cbLieDetect.IsChecked == true
                    ? new AutoLieDetector((int)(nudLieStart.Value ?? 0), (int)(nudLieEnd.Value ?? 0),
                                          (int)(nudLieInt.Value ?? 1),   (int)(nudLieProp.Value ?? 0))
                    : null;
                info.help = cbHelpEnable.IsChecked == true
                    ? tbHelp.Text?.Replace("\n", @"\n")
                    : null;
            }
        }

        // ── UI helpers ────────────────────────────────────────────────

        static TabItem MakeTab(string header, Control content)
            => new TabItem { Header = header, Content = new ScrollViewer { Content = content } };

        static StackPanel LR(string label, Control ctrl)
            => new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Margin = new Avalonia.Thickness(0, 2),
                Children =
                {
                    new TextBlock { Text = label, Width = 175, VerticalAlignment = VerticalAlignment.Center },
                    ctrl
                }
            };

        static StackPanel Row(params Control[] controls)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var c in controls) sp.Children.Add(c);
            return sp;
        }

        static CheckBox BoolCb(string label, bool value)
            => new CheckBox { Content = label, IsChecked = value };

        static CheckBox MBCb(string label, MapleBool mb)
            => new CheckBox { Content = label, IsThreeState = true,
                              IsChecked = mb.HasValue ? mb.Value : (bool?)null };

        static (StackPanel row, Func<int?> get) OptInt(string label, int? value, int min, int max)
        {
            var cb  = new CheckBox { IsChecked = value.HasValue };
            var nud = new NumericUpDown
            {
                Value = value ?? 0, Minimum = min, Maximum = max,
                FormatString = "F0", IsEnabled = value.HasValue, Width = 120
            };
            cb.IsCheckedChanged += (_, _) => nud.IsEnabled = cb.IsChecked == true;
            var row = LR(label, Row(cb, nud));
            return (row, () => cb.IsChecked == true ? (int?)nud.Value : null);
        }

        static (StackPanel row, Func<string?> get) OptStr(string label, string? value)
        {
            var cb = new CheckBox { IsChecked = value != null };
            var tb = new TextBox { Text = value ?? "", IsEnabled = value != null, Width = 220 };
            cb.IsCheckedChanged += (_, _) => tb.IsEnabled = cb.IsChecked == true;
            var row = LR(label, Row(cb, tb));
            return (row, () => cb.IsChecked == true ? tb.Text : null);
        }

        static (StackPanel row, Func<float?> get) OptFloat(string label, float? value, float min, float max)
        {
            var cb  = new CheckBox { IsChecked = value.HasValue };
            var nud = new NumericUpDown
            {
                Value = (decimal?)value, Minimum = (decimal)min, Maximum = (decimal)max,
                Increment = 0.01m, FormatString = "F3", IsEnabled = value.HasValue, Width = 120
            };
            cb.IsCheckedChanged += (_, _) => nud.IsEnabled = cb.IsChecked == true;
            var row = LR(label, Row(cb, nud));
            return (row, () => cb.IsChecked == true ? (float?)(float)(nud.Value ?? 0) : null);
        }

        static async Task<string?> ShowInputAsync(string title, string prompt, Window? owner)
        {
            string? result = null;
            var tb      = new TextBox { Width = 200 };
            var btnOK   = new Button { Content = "OK",     IsDefault = true };
            var btnCanc = new Button { Content = "Cancel" };
            var win = new Window
            {
                Title = title, Width = 300, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = prompt }, tb,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 6, Children = { btnOK, btnCanc }
                        }
                    }
                }
            };
            btnOK.Click   += (_, _) => { result = tb.Text; win.Close(); };
            btnCanc.Click += (_, _) => win.Close();
            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
            return result;
        }
    }
}
