/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HaCreator.MapEditor.AI;
using System;
using System.Diagnostics;

namespace HaCreator.GUI.EditorPanels
{
    public class AISettingsDialog : Window
    {
        // Provider selection
        private readonly ComboBox _cboProvider;

        // OpenRouter controls
        private readonly Panel _pnlOpenRouter;
        private readonly TextBox _txtApiKey;
        private readonly ComboBox _cboModel;

        // OpenCode controls
        private readonly Panel _pnlOpenCode;
        private readonly TextBox _txtOpenCodeHost;
        private readonly NumericUpDown _numOpenCodePort;
        private readonly ComboBox _cboOpenCodeModel;
        private readonly ComboBox _cboOpenCodeReasoning;
        private readonly CheckBox _chkAutoStart;

        // Status + buttons
        private readonly TextBlock _lblStatus;
        private readonly Button _btnSave;
        private readonly Button _btnTest;

        private bool _connectionTested = false;

        public AISettingsDialog()
        {
            Title = "AI Settings";
            Width = 540;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Padding = new Thickness(16);

            // Provider row
            _cboProvider = new ComboBox
            {
                ItemsSource = new[] { "OpenRouter (Cloud API)", "OpenCode (Local Server)" },
                SelectedIndex = 0,
                Width = 220
            };
            _cboProvider.SelectionChanged += (_, _) => OnProviderChanged();

            // ── OpenRouter Panel ──────────────────────────────────────
            _txtApiKey = new TextBox { Watermark = "sk-or-v1-...", Width = 460 };
            _txtApiKey.TextChanged += (_, _) => InvalidateTest();

            _cboModel = new ComboBox { ItemsSource = AISettings.AvailableModels, Width = 360 };
            _cboModel.SelectionChanged += (_, _) => InvalidateTest();

            var lnkGetKey = MakeLink("Get your API key from openrouter.ai",
                "https://openrouter.ai/keys");

            _pnlOpenRouter = new StackPanel
            {
                IsVisible = true,
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "OpenRouter API Key:", FontWeight = FontWeight.Bold },
                    _txtApiKey,
                    lnkGetKey,
                    new TextBlock { Text = "AI Model:", FontWeight = FontWeight.Bold },
                    _cboModel
                }
            };

            // ── OpenCode Panel ────────────────────────────────────────
            _txtOpenCodeHost = new TextBox { Text = "127.0.0.1", Width = 220 };
            _txtOpenCodeHost.TextChanged += (_, _) => InvalidateTest();

            _numOpenCodePort = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 65535,
                Value = 4096,
                Width = 100
            };
            _numOpenCodePort.ValueChanged += (_, _) => InvalidateTest();

            _cboOpenCodeModel = new ComboBox
            {
                ItemsSource = AISettings.AvailableOpenCodeModels,
                Width = 360
            };
            _cboOpenCodeModel.SelectionChanged += (_, _) => InvalidateTest();

            _cboOpenCodeReasoning = new ComboBox
            {
                ItemsSource = AISettings.AvailableOpenCodeReasoningEfforts,
                Width = 140
            };
            _cboOpenCodeReasoning.SelectionChanged += (_, _) => InvalidateTest();

            _chkAutoStart = new CheckBox { Content = "Auto-start server if not running", IsChecked = true };

            var btnRegenTools = new Button { Content = "Regenerate Tools" };
            btnRegenTools.Click += BtnRegenTools_Click;

            var lnkOpenCodeHelp = MakeLink(
                "OpenCode requires 'opencode serve' running locally",
                "https://opencode.ai/docs/server/");

            _pnlOpenCode = new StackPanel
            {
                IsVisible = false,
                Spacing = 6,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 12,
                        Children =
                        {
                            new StackPanel { Spacing = 4, Children =
                                { new TextBlock { Text = "Host:" }, _txtOpenCodeHost } },
                            new StackPanel { Spacing = 4, Children =
                                { new TextBlock { Text = "Port:" }, _numOpenCodePort } }
                        }
                    },
                    lnkOpenCodeHelp,
                    new TextBlock { Text = "AI Model:", FontWeight = FontWeight.Bold },
                    _cboOpenCodeModel,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 16,
                        Children =
                        {
                            new StackPanel { Spacing = 4, Children =
                                { new TextBlock { Text = "Reasoning Effort:" }, _cboOpenCodeReasoning } },
                            _chkAutoStart
                        }
                    },
                    new TextBlock
                    {
                        Text = "Note: OpenCode uses OAuth. Run 'opencode auth' first.",
                        Foreground = Brushes.Gray, FontSize = 11
                    },
                    btnRegenTools
                }
            };

            // ── Status + buttons ──────────────────────────────────────
            _lblStatus = new TextBlock
            {
                Text = string.Empty,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            };

            _btnTest = new Button { Content = "Test Connection" };
            _btnTest.Click += BtnTest_Click;

            _btnSave = new Button { Content = "Save", IsEnabled = false };
            _btnSave.Click += BtnSave_Click;

            var btnCancel = new Button { Content = "Cancel" };
            btnCancel.Click += (_, _) => Close();

            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 12,
                        Children =
                        {
                            new TextBlock { Text = "AI Provider:", VerticalAlignment = VerticalAlignment.Center },
                            _cboProvider
                        }
                    },
                    _pnlOpenRouter,
                    _pnlOpenCode,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal, Spacing = 12,
                        Children = { _btnTest, _lblStatus }
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { btnCancel, _btnSave }
                    }
                }
            };

            Opened += (_, _) => LoadSettings();
        }

        private static Button MakeLink(string text, string url)
        {
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.DodgerBlue,
                    TextDecorations = TextDecorations.Underline
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            btn.Click += (_, _) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AISettingsDialog] Failed to open URL {url}: {ex.Message}");
                }
            };
            return btn;
        }

        private void LoadSettings()
        {
            _cboProvider.SelectedIndex = AISettings.Provider == AIProvider.OpenCode ? 1 : 0;

            _txtApiKey.Text = AISettings.ApiKey ?? string.Empty;
            _cboModel.SelectedItem = AISettings.Model;
            if (_cboModel.SelectedIndex < 0 && _cboModel.ItemCount > 0)
                _cboModel.SelectedIndex = 0;

            _txtOpenCodeHost.Text = AISettings.OpenCodeHost ?? "127.0.0.1";
            _numOpenCodePort.Value = AISettings.OpenCodePort;
            _cboOpenCodeModel.SelectedItem = AISettings.OpenCodeModel;
            if (_cboOpenCodeModel.SelectedIndex < 0 && _cboOpenCodeModel.ItemCount > 0)
                _cboOpenCodeModel.SelectedIndex = 0;

            _cboOpenCodeReasoning.SelectedItem = AISettings.OpenCodeReasoningEffort;
            if (_cboOpenCodeReasoning.SelectedIndex < 0 && _cboOpenCodeReasoning.ItemCount > 1)
                _cboOpenCodeReasoning.SelectedIndex = 1; // medium

            _chkAutoStart.IsChecked = AISettings.OpenCodeAutoStart;

            UpdatePanelVisibility();

            if (AISettings.IsConfigured)
            {
                _lblStatus.Text = $"Configured: {AISettings.GetStatusDescription()}";
                _lblStatus.Foreground = Brushes.Green;
            }
            else
            {
                _lblStatus.Text = "Not configured — test connection to save.";
                _lblStatus.Foreground = Brushes.Gray;
            }
        }

        private void OnProviderChanged()
        {
            UpdatePanelVisibility();
            InvalidateTest();
        }

        private void UpdatePanelVisibility()
        {
            bool isOpenCode = _cboProvider.SelectedIndex == 1;
            _pnlOpenRouter.IsVisible = !isOpenCode;
            _pnlOpenCode.IsVisible   = isOpenCode;
        }

        private void InvalidateTest()
        {
            _connectionTested = false;
            _btnSave.IsEnabled = false;
            _lblStatus.Text = "Test connection required before saving.";
            _lblStatus.Foreground = Brushes.Gray;
        }

        private async void BtnTest_Click(object? sender, RoutedEventArgs e)
        {
            _btnTest.IsEnabled = false;
            _lblStatus.Text = "Testing connection...";
            _lblStatus.Foreground = Brushes.Gray;

            try
            {
                bool isOpenCode = _cboProvider.SelectedIndex == 1;

                if (isOpenCode)
                {
                    string host = _txtOpenCodeHost.Text?.Trim() ?? string.Empty;
                    int port    = (int)(_numOpenCodePort.Value ?? 4096);
                    bool auto   = _chkAutoStart.IsChecked == true;
                    string model    = _cboOpenCodeModel.SelectedItem?.ToString() ?? string.Empty;
                    string effort   = _cboOpenCodeReasoning.SelectedItem?.ToString();

                    if (string.IsNullOrWhiteSpace(host))
                    {
                        _lblStatus.Text = "Enter the OpenCode server host.";
                        _lblStatus.Foreground = Brushes.Red;
                        return;
                    }

                    if (auto)
                    {
                        _lblStatus.Text = "Starting OpenCode server...";
                        _lblStatus.Foreground = Brushes.Gray;
                    }

                    var client = new OpenCodeClient(host, port, model, auto, effort);
                    bool success = await client.TestConnectionAsync();

                    if (success)
                    {
                        string msg = OpenCodeClient.IsManagedServerRunning
                            ? "OpenCode connection successful! (server auto-started)"
                            : "OpenCode connection successful!";
                        _lblStatus.Text = msg;
                        _lblStatus.Foreground = Brushes.Green;
                        _connectionTested = true;
                        _btnSave.IsEnabled = true;
                    }
                    else
                    {
                        _lblStatus.Text = auto
                            ? "Failed to start/connect. Is 'opencode' installed?"
                            : $"Cannot connect to OpenCode at {host}:{port}.";
                        _lblStatus.Foreground = Brushes.Red;
                    }
                }
                else
                {
                    string key = _txtApiKey.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        _lblStatus.Text = "Enter an API key first.";
                        _lblStatus.Foreground = Brushes.Red;
                        return;
                    }

                    string model = _cboModel.SelectedItem?.ToString() ?? AISettings.Model;
                    var client = new OpenRouterClient(key, model);
                    bool success = await client.TestConnectionAsync();

                    if (success)
                    {
                        _lblStatus.Text = "OpenRouter connection successful!";
                        _lblStatus.Foreground = Brushes.Green;
                        _connectionTested = true;
                        _btnSave.IsEnabled = true;
                    }
                    else
                    {
                        _lblStatus.Text = "Connection failed — check your API key.";
                        _lblStatus.Foreground = Brushes.Red;
                    }
                }
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error: {ex.Message}";
                _lblStatus.Foreground = Brushes.Red;
            }
            finally
            {
                _btnTest.IsEnabled = true;
            }
        }

        private void BtnRegenTools_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                string? dir = AppDomain.CurrentDomain.BaseDirectory;
                while (!string.IsNullOrEmpty(dir))
                {
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".opencode")))
                        break;
                    var parent = System.IO.Directory.GetParent(dir);
                    if (parent == null) { dir = null; break; }
                    dir = parent.FullName;
                }

                if (string.IsNullOrEmpty(dir))
                {
                    _lblStatus.Text = "Cannot find .opencode folder — run from the project root.";
                    _lblStatus.Foreground = Brushes.Red;
                    return;
                }

                string toolDir = System.IO.Path.Combine(dir, ".opencode", "tool");
                int count = OpenCodeToolGenerator.GenerateAllTools(toolDir);
                _lblStatus.Text = $"Generated {count} tool files in: {toolDir}";
                _lblStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error regenerating tools: {ex.Message}";
                _lblStatus.Foreground = Brushes.Red;
            }
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            bool isOpenCode = _cboProvider.SelectedIndex == 1;
            AISettings.Provider = isOpenCode ? AIProvider.OpenCode : AIProvider.OpenRouter;

            if (isOpenCode)
            {
                AISettings.OpenCodeHost            = _txtOpenCodeHost.Text?.Trim() ?? string.Empty;
                AISettings.OpenCodePort            = (int)(_numOpenCodePort.Value ?? 4096);
                AISettings.OpenCodeModel           = _cboOpenCodeModel.SelectedItem?.ToString() ?? string.Empty;
                AISettings.OpenCodeReasoningEffort = _cboOpenCodeReasoning.SelectedItem?.ToString();
                AISettings.OpenCodeAutoStart       = _chkAutoStart.IsChecked == true;
            }
            else
            {
                AISettings.ApiKey = _txtApiKey.Text?.Trim() ?? string.Empty;
                AISettings.Model  = _cboModel.SelectedItem?.ToString() ?? AISettings.AvailableModels[0];
            }

            Close();
        }
    }
}
