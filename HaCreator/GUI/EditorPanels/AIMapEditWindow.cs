/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HaCreator.GUI.EditorPanels
{
    /// <summary>
    /// Avalonia code-only window for AI-based map editing with chat interface.
    /// One instance is created per map/board.
    /// </summary>
    public class AIMapEditWindow : Window
    {
        private const string OpenCodeManualStartHint =
            "Hint: Run in terminal: opencode serve --port 4096 --hostname 127.0.0.1";

        private static readonly Dictionary<Board, AIMapEditWindow> _instances = new();
        private static readonly object _startupLock = new();
        private static Task _startupTask = Task.CompletedTask;

        private readonly Board _board;
        private readonly ChatSession _chatSession;

        // UI elements
        private readonly TextBox _txtMapContext;
        private readonly TextBox _txtChatDisplay;
        private readonly TextBox _txtInput;
        private readonly Button _btnSend;
        private readonly Button _btnExecute;
        private bool _isProcessing;

        private AIMapEditWindow(Board board)
        {
            _board = board;
            _chatSession = new ChatSession();

            Title = "AI Map Editor";
            Width = 900;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Map context panel (left)
            _txtMapContext = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 200
            };
            var btnRefresh = new Button { Content = "Refresh Context" };
            btnRefresh.Click += (_, _) => LoadMapContext();

            var contextPanel = new DockPanel { Width = 280 };
            DockPanel.SetDock(new TextBlock { Text = "Map Context", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) }, Dock.Top);
            DockPanel.SetDock(btnRefresh, Dock.Bottom);
            contextPanel.Children.Add(new TextBlock { Text = "Map Context", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 0, 0, 4) });
            contextPanel.Children.Add(btnRefresh);
            contextPanel.Children.Add(new ScrollViewer { Content = _txtMapContext });

            // Chat display (right, top portion)
            _txtChatDisplay = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Courier New,monospace")
            };

            // Chat input + buttons
            _txtInput = new TextBox
            {
                AcceptsReturn = false,
                Watermark = "Type a message... (Enter to send)",
                MinHeight = 36
            };
            _txtInput.KeyDown += TxtInput_KeyDown;
            _txtInput.TextChanged += (_, _) => _btnSend.IsEnabled = !string.IsNullOrWhiteSpace(_txtInput.Text) && !_isProcessing;

            _btnSend = new Button { Content = "Send", IsEnabled = false };
            _btnSend.Click += async (_, _) => await SendMessageAsync();

            _btnExecute = new Button { Content = "Execute", IsEnabled = false };
            _btnExecute.Click += BtnExecute_Click;

            var btnClear = new Button { Content = "Clear Chat" };
            btnClear.Click += BtnClear_Click;

            var btnSettings = new Button { Content = "Settings" };
            btnSettings.Click += async (_, e) =>
            {
                var dlg = new AISettingsDialog();
                await dlg.ShowDialog(this);
            };

            var btnRunTests = new Button { Content = "Run Tests" };
            btnRunTests.Click += async (_, _) => await BtnRunTests_ClickAsync(btnRunTests);

            var toolBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 0, 0, 6),
                Children = { btnClear, btnSettings, btnRunTests }
            };

            var inputRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(_btnSend, Dock.Right);
            DockPanel.SetDock(_btnExecute, Dock.Right);
            inputRow.Children.Add(_btnExecute);
            inputRow.Children.Add(_btnSend);
            inputRow.Children.Add(_txtInput);

            var chatPanel = new DockPanel { Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(toolBar, Dock.Top);
            DockPanel.SetDock(inputRow, Dock.Bottom);
            chatPanel.Children.Add(toolBar);
            chatPanel.Children.Add(inputRow);
            chatPanel.Children.Add(new ScrollViewer { Content = _txtChatDisplay });

            // Main grid: context | chat
            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("280,*"),
                Margin = new Thickness(8)
            };
            Grid.SetColumn(contextPanel, 0);
            Grid.SetColumn(chatPanel, 1);
            mainGrid.Children.Add(contextPanel);
            mainGrid.Children.Add(chatPanel);

            Content = mainGrid;

            // Subscribe to message changes
            _chatSession.Messages.CollectionChanged += Messages_CollectionChanged;

            // Suppress close, hide instead
            Closing += (_, e) => { e.Cancel = true; Hide(); };

            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string mapName = "Unknown";
            int mapId = 0;
            if (_board?.MapInfo != null)
            {
                mapId = _board.MapInfo.id;
                mapName = !string.IsNullOrEmpty(_board.MapInfo.strMapName)
                    ? _board.MapInfo.strMapName
                    : $"Map {mapId}";
            }
            Title = $"AI Map Editor — {mapName} ({mapId})";
        }

        #region Static Instance Management

        public static AIMapEditWindow GetOrCreate(Board board)
        {
            if (board == null) return null!;
            CleanupClosedInstances();
            if (_instances.TryGetValue(board, out var existing))
                return existing;
            var w = new AIMapEditWindow(board);
            _instances[board] = w;
            return w;
        }

        public static void ShowForBoard(Board board, Window? owner = null)
        {
            var window = GetOrCreate(board);
            if (window == null)
            {
                Debug.WriteLine("[AIMapEditWindow] No board loaded.");
                return;
            }

            if (owner != null)
                window.ShowInTaskbar = true;

            if (window.IsVisible)
            {
                window.Activate();
                window.Focus();
            }
            else
            {
                window.Show();
            }

            TryAutoStartOpenCodeServer();
            window.LoadMapContext();
        }

        public static void CloseForBoard(Board board)
        {
            if (board != null && _instances.TryGetValue(board, out var w))
            {
                _instances.Remove(board);
                w.Closing -= null; // detach guard
                w.Close();
            }
        }

        public static void CloseAll()
        {
            foreach (var w in _instances.Values)
                w.Close();
            _instances.Clear();
            AIClientFactory.Cleanup();
        }

        private static void CleanupClosedInstances()
        {
            var toRemove = new List<Board>();
            foreach (var kvp in _instances)
            {
                try { var _ = kvp.Value.IsVisible; }
                catch { toRemove.Add(kvp.Key); }
            }
            foreach (var key in toRemove)
                _instances.Remove(key);
        }

        private static void TryAutoStartOpenCodeServer()
        {
            if (AISettings.Provider != AIProvider.OpenCode || !AISettings.OpenCodeAutoStart) return;
            lock (_startupLock)
            {
                if (!_startupTask.IsCompleted) return;
                _startupTask = Task.Run(async () =>
                {
                    try
                    {
                        var client = new OpenCodeClient(
                            AISettings.OpenCodeHost, AISettings.OpenCodePort,
                            AISettings.OpenCodeModel, AISettings.OpenCodeAutoStart,
                            AISettings.OpenCodeReasoningEffort);
                        await client.EnsureServerAsync();
                        Debug.WriteLine("[AIMapEditWindow] OpenCode server ready.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AIMapEditWindow] OpenCode auto-start failed: {ex.Message}");
                    }
                });
            }
        }

        #endregion

        #region Map Context

        public void LoadMapContext()
        {
            if (_board == null)
            {
                _txtMapContext.Text = "No map loaded";
                return;
            }
            try
            {
                var serializer = new MapAISerializer(_board);
                string text = serializer.GenerateAISummary();
                _txtMapContext.Text = text;
                _chatSession.CurrentMapContext = text;
            }
            catch (Exception ex)
            {
                _txtMapContext.Text = $"Error: {ex.Message}";
            }
        }

        #endregion

        #region Chat

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (ChatMessage msg in e.NewItems)
                    msg.PropertyChanged += (_, _) => RefreshChatDisplay();
            }
            RefreshChatDisplay();
        }

        private void RefreshChatDisplay()
        {
            var sb = new StringBuilder();
            foreach (var msg in _chatSession.Messages)
            {
                string prefix = msg.IsUser ? "You" : "AI";
                string ts     = msg.Timestamp.ToString("HH:mm");
                sb.AppendLine($"[{ts}] {prefix}:");

                if (msg.IsProcessing)
                    sb.AppendLine("  (thinking...)");
                else if (msg.HasError)
                    sb.AppendLine($"  [Error] {msg.ErrorMessage}");
                else
                    sb.AppendLine($"  {msg.Content}");

                if (!string.IsNullOrEmpty(msg.CommandsContent))
                {
                    sb.AppendLine("  [Commands]");
                    foreach (var line in msg.CommandsContent.Split('\n'))
                        sb.AppendLine($"    {line}");
                }

                sb.AppendLine();
            }

            string display = sb.ToString();
            Dispatcher.UIThread.Post(() => _txtChatDisplay.Text = display);
        }

        private void TxtInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter &&
                !e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
            {
                e.Handled = true;
                _ = SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            if (_isProcessing) return;
            string userInput = _txtInput.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(userInput)) return;

            if (!AISettings.IsConfigured)
            {
                var dlg = new AISettingsDialog();
                await dlg.ShowDialog(this);
                if (!AISettings.IsConfigured) return;
            }

            if (_board == null)
            {
                Debug.WriteLine("[AIMapEditWindow] No map loaded.");
                return;
            }

            try
            {
                _isProcessing = true;
                _btnSend.IsEnabled = false;
                _btnExecute.IsEnabled = false;
                _txtInput.Text = string.Empty;

                _chatSession.AddUserMessage(userInput);
                var assistantMsg = _chatSession.AddAssistantMessage();

                var serializer = new MapAISerializer(_board);
                _chatSession.CurrentMapContext = serializer.GenerateAISummary();

                var orchestrator = new AgentOrchestrator();
                string result = await orchestrator.ProcessWithConversationAsync(
                    _chatSession.CurrentMapContext,
                    _chatSession.ToConversationHistory(),
                    userInput);

                var (explanation, commands) = ParseAIResponse(result);
                assistantMsg.IsProcessing = false;
                await StreamAssistantTextAsync(assistantMsg, explanation, CancellationToken.None);
                assistantMsg.CommandsContent = commands;
            }
            catch (Exception ex)
            {
                var last = _chatSession.LastAssistantMessage;
                if (last != null)
                {
                    last.IsProcessing = false;
                    last.HasError     = true;
                    last.ErrorMessage = BuildErrorMessage(ex);
                }
            }
            finally
            {
                _isProcessing = false;
                _btnSend.IsEnabled = true;
                _btnExecute.IsEnabled = _chatSession.HasCommands;
                _txtInput.Focus();
            }
        }

        private static (string explanation, string commands) ParseAIResponse(string response)
        {
            var commandPrefixes = new[] {
                "ADD ", "SET ", "DELETE ", "MOVE ", "TILE ", "CLEAR ", "FLIP ",
                "# QUERY:", "# WARNING:"
            };
            var explanLines = new List<string>();
            var commandLines = new List<string>();

            foreach (var line in response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                bool isCmd = false;
                foreach (var p in commandPrefixes)
                    if (trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { isCmd = true; break; }

                if (isCmd) commandLines.Add(trimmed);
                else if (!string.IsNullOrWhiteSpace(trimmed)) explanLines.Add(trimmed);
            }

            string explanation = explanLines.Count > 0
                ? string.Join(Environment.NewLine, explanLines)
                : "Here are the commands to accomplish your request:";
            string commands = string.Join(Environment.NewLine, commandLines);
            return (explanation, commands);
        }

        private static async Task StreamAssistantTextAsync(ChatMessage msg, string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text)) { msg.Content = text ?? string.Empty; return; }

            int len = text.Length;
            int chunk = Math.Max(1, len / 60);
            int delay = len > 1500 ? 8 : 14;
            msg.Content = string.Empty;

            for (int i = chunk; i < len; i += chunk)
            {
                if (ct.IsCancellationRequested) break;
                msg.Content = text[..i];
                await Task.Delay(delay, ct);
            }
            msg.Content = text;
        }

        private void BtnClear_Click(object? sender, RoutedEventArgs e)
        {
            _chatSession.Clear();
            _btnExecute.IsEnabled = false;
        }

        private void BtnExecute_Click(object? sender, RoutedEventArgs e)
        {
            if (_board == null) { Debug.WriteLine("[AIMapEditWindow] No map loaded."); return; }

            string commandText = _chatSession.GetLatestCommands();
            if (string.IsNullOrWhiteSpace(commandText))
            {
                Debug.WriteLine("[AIMapEditWindow] No commands to execute.");
                return;
            }

            try
            {
                var parser   = new MapAIParser();
                var commands = parser.ParseCommands(commandText);
                if (commands.Count == 0) { Debug.WriteLine("[AIMapEditWindow] No valid commands."); return; }

                var executor = new MapAIExecutor(_board);
                var result   = executor.ExecuteCommands(commands);
                Debug.WriteLine($"[AIMapEditWindow] Execute: {result.SuccessCount} ok, {result.FailCount} failed");

                if (result.SuccessCount > 0)
                {
                    _board.Dirty = true;
                    LoadMapContext();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AIMapEditWindow] Execute error: {ex.Message}");
            }
        }

        private async Task BtnRunTests_ClickAsync(Button btnRunTests)
        {
            if (_isProcessing || _board == null) return;
            if (!AISettings.IsConfigured)
            {
                var dlg = new AISettingsDialog();
                await dlg.ShowDialog(this);
                if (!AISettings.IsConfigured) return;
            }

            try
            {
                _isProcessing = true;
                btnRunTests.IsEnabled = false;
                _btnSend.IsEnabled = false;

                string testPrompt = MapEditorPromptBuilder.LoadPromptFile("ComprehensiveTestPrompt.txt");
                _chatSession.Clear();
                _chatSession.AddUserMessage("=== RUNNING AUTOMATED TESTS ===\n\n" + testPrompt);
                var assistantMsg = _chatSession.AddAssistantMessage();

                var serializer = new MapAISerializer(_board);
                _chatSession.CurrentMapContext = serializer.GenerateAISummary();

                var orchestrator = new AgentOrchestrator();
                string result = await orchestrator.ProcessWithConversationAsync(
                    _chatSession.CurrentMapContext,
                    _chatSession.ToConversationHistory(),
                    testPrompt);

                var (explanation, commands) = ParseAIResponse(result);
                assistantMsg.IsProcessing = false;
                await StreamAssistantTextAsync(assistantMsg, explanation, CancellationToken.None);
                assistantMsg.CommandsContent = commands;

                Debug.WriteLine("[AIMapEditWindow] Tests complete.");
            }
            catch (Exception ex)
            {
                var last = _chatSession.LastAssistantMessage;
                if (last != null) { last.IsProcessing = false; last.HasError = true; last.ErrorMessage = BuildErrorMessage(ex, "Test error"); }
            }
            finally
            {
                _isProcessing = false;
                btnRunTests.IsEnabled = true;
                _btnSend.IsEnabled = true;
                _btnExecute.IsEnabled = _chatSession.HasCommands;
            }
        }

        #endregion

        private static string BuildErrorMessage(Exception ex, string prefix = "Error")
        {
            string msg = $"{prefix}: {ex.Message}";
            if (AISettings.Provider == AIProvider.OpenCode && AISettings.OpenCodeAutoStart)
            {
                string m = ex.Message?.ToLowerInvariant() ?? string.Empty;
                if (m.Contains("failed to auto-start") || m.Contains("opencode server not running")
                    || m.Contains("opencode cli not found") || (m.Contains("opencode") && m.Contains("not running")))
                    msg += Environment.NewLine + OpenCodeManualStartHint;
            }
            return msg;
        }
    }
}
