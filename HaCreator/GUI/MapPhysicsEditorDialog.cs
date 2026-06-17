/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using MapleLib.WzLib;
using System;
using System.Threading.Tasks;

namespace HaCreator.GUI
{
    public static class MapPhysicsEditorDialog
    {
        private const string WZ_FILE_NAME = "map";
        private const string WZ_FILE_IMAGE = "Physics.img";

        // Defaults (pre-BB)
        private const decimal D_walkForce         = 140000m;
        private const decimal D_walkSpeed         = 125m;
        private const decimal D_walkDrag          = 80000m;
        private const decimal D_slipForce         = 60000m;
        private const decimal D_slipSpeed         = 120m;
        private const decimal D_floatDrag1        = 100000m;
        private const decimal D_floatDrag2        = 10000m;
        private const decimal D_floatCoefficient  = 0.01m;
        private const decimal D_swimForce         = 120000m;
        private const decimal D_swimSpeed         = 140m;
        private const decimal D_flyForce          = 120000m;
        private const decimal D_flySpeed          = 200m;
        private const decimal D_gravityAcc        = 2000m;
        private const decimal D_fallSpeed         = 670m;
        private const decimal D_jumpSpeed         = 555m;
        private const decimal D_maxFriction       = 2m;
        private const decimal D_minFriction       = 0.05m;
        private const decimal D_swimSpeedDec      = 0.9m;
        private const decimal D_flyJumpDec        = 0.35m;

        public static void ShowAsync(Window? owner)
        {
            Dispatcher.UIThread.Post(async () => await ShowImplAsync(owner));
        }

        private static async Task ShowImplAsync(Window? owner)
        {
            if (Program.IsPreBBDataWzFormat)
            {
                await MsgBox($"Editing of {WZ_FILE_NAME}/{WZ_FILE_IMAGE} is not available in beta MapleStory.", owner);
                return;
            }

            WzImage? img = Program.FindImage(WZ_FILE_NAME, WZ_FILE_IMAGE);
            if (img == null)
            {
                await MsgBox($"Map.wz is not loaded, or Map.wz/{WZ_FILE_IMAGE} does not exist.", owner);
                return;
            }

            // Read current values (with defaults as fallback)
            decimal Val(string key, decimal def)
            {
                try { return (decimal)img[key].GetDouble(); } catch { return def; }
            }

            var nudWalkForce        = Nud(Val("walkForce",        D_walkForce),        0, 9999999, 1000m, "F0");
            var nudWalkSpeed        = Nud(Val("walkSpeed",        D_walkSpeed),        0, 9999,    1m,    "F0");
            var nudWalkDrag         = Nud(Val("walkDrag",         D_walkDrag),         0, 9999999, 1000m, "F0");
            var nudSlipForce        = Nud(Val("slipForce",        D_slipForce),        0, 9999999, 1000m, "F0");
            var nudSlipSpeed        = Nud(Val("slipSpeed",        D_slipSpeed),        0, 9999,    1m,    "F0");
            var nudFloatDrag1       = Nud(Val("floatDrag1",       D_floatDrag1),       0, 9999999, 1000m, "F0");
            var nudFloatDrag2       = Nud(Val("floatDrag2",       D_floatDrag2),       0, 9999999, 1000m, "F0");
            var nudFloatCoeff       = Nud(Val("floatCoefficient", D_floatCoefficient), 0, 10m,     0.001m,"F4");
            var nudSwimForce        = Nud(Val("swimForce",        D_swimForce),        0, 9999999, 1000m, "F0");
            var nudSwimSpeed        = Nud(Val("swimSpeed",        D_swimSpeed),        0, 9999,    1m,    "F0");
            var nudFlyForce         = Nud(Val("flyForce",         D_flyForce),         0, 9999999, 1000m, "F0");
            var nudFlySpeed         = Nud(Val("flySpeed",         D_flySpeed),         0, 9999,    1m,    "F0");
            var nudGravityAcc       = Nud(Val("gravityAcc",       D_gravityAcc),       0, 9999999, 100m,  "F0");
            var nudFallSpeed        = Nud(Val("fallSpeed",        D_fallSpeed),        0, 9999,    1m,    "F0");
            var nudJumpSpeed        = Nud(Val("jumpSpeed",        D_jumpSpeed),        0, 9999,    1m,    "F0");
            var nudMaxFriction      = Nud(Val("maxFriction",      D_maxFriction),      0, 100m,    0.01m, "F3");
            var nudMinFriction      = Nud(Val("minFriction",      D_minFriction),      0, 100m,    0.001m,"F4");
            var nudSwimSpeedDec     = Nud(Val("swimSpeedDec",     D_swimSpeedDec),     0, 10m,     0.01m, "F3");
            var nudFlyJumpDec       = Nud(Val("flyJumpDec",       D_flyJumpDec),       0, 10m,     0.01m, "F3");

            var lblError = new TextBlock { Foreground = Avalonia.Media.Brushes.Red, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

            var btnSave   = new Button { Content = "Save to Physics.img", IsDefault = true };
            var btnReset  = new Button { Content = "Reset to Defaults" };
            var btnCancel = new Button { Content = "Close" };

            void ResetDefaults()
            {
                nudWalkForce.Value    = D_walkForce;
                nudWalkSpeed.Value    = D_walkSpeed;
                nudWalkDrag.Value     = D_walkDrag;
                nudSlipForce.Value    = D_slipForce;
                nudSlipSpeed.Value    = D_slipSpeed;
                nudFloatDrag1.Value   = D_floatDrag1;
                nudFloatDrag2.Value   = D_floatDrag2;
                nudFloatCoeff.Value   = D_floatCoefficient;
                nudSwimForce.Value    = D_swimForce;
                nudSwimSpeed.Value    = D_swimSpeed;
                nudFlyForce.Value     = D_flyForce;
                nudFlySpeed.Value     = D_flySpeed;
                nudGravityAcc.Value   = D_gravityAcc;
                nudFallSpeed.Value    = D_fallSpeed;
                nudJumpSpeed.Value    = D_jumpSpeed;
                nudMaxFriction.Value  = D_maxFriction;
                nudMinFriction.Value  = D_minFriction;
                nudSwimSpeedDec.Value = D_swimSpeedDec;
                nudFlyJumpDec.Value   = D_flyJumpDec;
            }

            var win = new Window
            {
                Title = "Map Physics Editor",
                Width = 420, Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Grid
                {
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Star),
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto),
                    },
                    Children =
                    {
                        Scroll(new StackPanel
                        {
                            Margin = new Avalonia.Thickness(10), Spacing = 6,
                            Children =
                            {
                                LR("Walk Force:",        nudWalkForce),
                                LR("Walk Speed:",        nudWalkSpeed),
                                LR("Walk Drag:",         nudWalkDrag),
                                LR("Slip Force:",        nudSlipForce),
                                LR("Slip Speed:",        nudSlipSpeed),
                                LR("Float Drag 1:",      nudFloatDrag1),
                                LR("Float Drag 2:",      nudFloatDrag2),
                                LR("Float Coefficient:", nudFloatCoeff),
                                LR("Swim Force:",        nudSwimForce),
                                LR("Swim Speed:",        nudSwimSpeed),
                                LR("Fly Force:",         nudFlyForce),
                                LR("Fly Speed:",         nudFlySpeed),
                                LR("Gravity Acc:",       nudGravityAcc),
                                LR("Fall Speed:",        nudFallSpeed),
                                LR("Jump Speed:",        nudJumpSpeed),
                                LR("Max Friction:",      nudMaxFriction),
                                LR("Min Friction:",      nudMinFriction),
                                LR("Swim Speed Dec:",    nudSwimSpeedDec),
                                LR("Fly Jump Dec:",      nudFlyJumpDec),
                            }
                        }, row: 0),
                        SetRow(lblError, 1),
                        SetRow(new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Avalonia.Thickness(6), Spacing = 6,
                            Children = { btnSave, btnReset, btnCancel }
                        }, 2),
                    }
                }
            };

            btnReset.Click  += (_, _) => ResetDefaults();
            btnCancel.Click += (_, _) => win.Close();

            btnSave.Click += (_, _) =>
            {
                btnSave.IsEnabled = false;
                lblError.Text = "";
                try
                {
                    img["walkForce"].SetValue(nudWalkForce.Value);
                    img["walkSpeed"].SetValue(nudWalkSpeed.Value);
                    img["walkDrag"].SetValue(nudWalkDrag.Value);
                    img["slipForce"].SetValue(nudSlipForce.Value);
                    img["slipSpeed"].SetValue(nudSlipSpeed.Value);
                    img["floatDrag1"].SetValue(nudFloatDrag1.Value);
                    img["floatDrag2"].SetValue(nudFloatDrag2.Value);
                    img["floatCoefficient"].SetValue(nudFloatCoeff.Value);
                    img["swimForce"].SetValue(nudSwimForce.Value);
                    img["swimSpeed"].SetValue(nudSwimSpeed.Value);
                    img["flyForce"].SetValue(nudFlyForce.Value);
                    img["flySpeed"].SetValue(nudFlySpeed.Value);
                    img["gravityAcc"].SetValue(nudGravityAcc.Value);
                    img["fallSpeed"].SetValue(nudFallSpeed.Value);
                    img["jumpSpeed"].SetValue(nudJumpSpeed.Value);
                    img["maxFriction"].SetValue(nudMaxFriction.Value);
                    img["minFriction"].SetValue(nudMinFriction.Value);
                    img["swimSpeedDec"].SetValue(nudSwimSpeedDec.Value);
                    img["flyJumpDec"].SetValue(nudFlyJumpDec.Value);
                    Program.MarkImageUpdated(WZ_FILE_NAME, img);
                    win.Close();
                }
                catch (Exception ex)
                {
                    lblError.Text = ex.Message;
                    btnSave.IsEnabled = true;
                }
            };

            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
        }

        // ── Helpers ───────────────────────────────────────────────────

        static NumericUpDown Nud(decimal val, decimal min, decimal max, decimal inc, string fmt)
            => new NumericUpDown { Value = val, Minimum = min, Maximum = max, Increment = inc, FormatString = fmt, Width = 130 };

        static StackPanel LR(string label, Control ctrl)
            => new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children =
                {
                    new TextBlock { Text = label, Width = 155, VerticalAlignment = VerticalAlignment.Center },
                    ctrl
                }
            };

        static ScrollViewer Scroll(Control content, int row = 0)
        {
            var sv = new ScrollViewer { Content = content };
            Grid.SetRow(sv, row);
            return sv;
        }

        static Control SetRow(Control c, int row) { Grid.SetRow(c, row); return c; }

        static async Task MsgBox(string text, Window? owner)
        {
            var win = new Window
            {
                Title = "Map Physics Editor", Width = 380, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(12), Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }
                    }
                }
            };
            ((Button)((StackPanel)win.Content!).Children[1]).Click += (_, _) => win.Close();
            await win.ShowDialog(owner ?? (Window?)Program.HaEditorWindow);
        }
    }
}
