using OcrAutomation.Models;
using OcrAutomation.Services.Interfaces;
using OcrAutomation.Utilities;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OcrAutomation.Services;

public class AutomationService : IAutomationService
{
    private readonly Stack<AutomationAction> _actionHistory = new();
    public bool CanUndo => _actionHistory.Count > 0;

    public async Task ExecuteActionsAsync(List<AutomationAction> actions, IntPtr? targetWindow = null)
    {
        if (targetWindow is { } hwnd && Win32Api.IsWindow(hwnd))
            Win32Api.SetForegroundWindow(hwnd);

        foreach (var action in actions)
        {
            ValidateAction(action);
            await ExecuteActionAsync(action);
            _actionHistory.Push(action);
            await Task.Delay(100);
        }
    }

    private static void ValidateAction(AutomationAction action)
    {
        if (action.Type is not (ActionType.Keystroke or ActionType.SetText or ActionType.Click))
            throw new InvalidEnumArgumentException(nameof(action.Type), (int)action.Type, typeof(ActionType));
        if (action.Type != ActionType.Click && action.Value == null)
            throw new ArgumentException("A keyboard or text action requires a value.");
        if (action.Type == ActionType.Click && (!action.Parameters.TryGetValue("x", out var xs) || !action.Parameters.TryGetValue("y", out var ys) ||
            !int.TryParse(xs, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) || !int.TryParse(ys, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("A click action requires integer x and y parameters.");
    }

    private static Task ExecuteActionAsync(AutomationAction action) => Task.Run(() =>
    {
        if (action.Type is ActionType.Keystroke or ActionType.SetText) SendKeystrokes(action.Value ?? string.Empty);
        else PerformClick(action);
    });

    private static void SendKeystrokes(string text)
    {
        var inputs = new List<Win32Api.INPUT>();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                var end = text.IndexOf('}', i + 1);
                if (end < 0) throw new FormatException("Unclosed key token.");
                var token = text[(i + 1)..end].Trim();
                if (token.Length == 0) throw new FormatException("Empty key token.");
                var keys = token.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => KeySequenceParser.TryResolve(part, out var key)
                        ? key
                        : throw new FormatException($"Unsupported key token: {{{part}}}."))
                    .ToList();
                foreach (var key in keys) AddVirtualKeyDown(inputs, key);
                foreach (var key in keys.AsEnumerable().Reverse()) AddVirtualKeyUp(inputs, key);
                i = end;
                continue;
            }
            AddUnicode(inputs, text[i]);
        }
        Send(inputs);
    }

    private static void AddUnicode(List<Win32Api.INPUT> inputs, char value)
    {
        inputs.Add(new() { Type = Win32Api.INPUT_KEYBOARD, Union = new() { Keyboard = new() { ScanCode = value, Flags = Win32Api.KEYEVENTF_UNICODE } } });
        inputs.Add(new() { Type = Win32Api.INPUT_KEYBOARD, Union = new() { Keyboard = new() { ScanCode = value, Flags = Win32Api.KEYEVENTF_UNICODE | Win32Api.KEYEVENTF_KEYUP } } });
    }

    private static void AddVirtualKeyDown(List<Win32Api.INPUT> inputs, KeyStroke key)
    {
        var flags = key.Extended ? Win32Api.KEYEVENTF_EXTENDEDKEY : 0u;
        inputs.Add(new() { Type = Win32Api.INPUT_KEYBOARD, Union = new() { Keyboard = new() { VirtualKeyCode = key.VirtualKey, Flags = flags } } });
    }

    private static void AddVirtualKeyUp(List<Win32Api.INPUT> inputs, KeyStroke key)
    {
        var flags = (key.Extended ? Win32Api.KEYEVENTF_EXTENDEDKEY : 0u) | Win32Api.KEYEVENTF_KEYUP;
        inputs.Add(new() { Type = Win32Api.INPUT_KEYBOARD, Union = new() { Keyboard = new() { VirtualKeyCode = key.VirtualKey, Flags = flags } } });
    }

    private static void Send(List<Win32Api.INPUT> inputs)
    {
        if (inputs.Count == 0) return;
        var sent = Win32Api.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Win32Api.INPUT>());
        if (sent != inputs.Count) throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput sent {sent} of {inputs.Count} events.");
    }

    private static void PerformClick(AutomationAction action)
    {
        int x = int.Parse(action.Parameters["x"], CultureInfo.InvariantCulture);
        int y = int.Parse(action.Parameters["y"], CultureInfo.InvariantCulture);
        if (!Win32Api.GetCursorPos(out var current)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read cursor position.");
        try
        {
            if (!Win32Api.SetCursorPos(x, y)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not move cursor.");
            Send(new List<Win32Api.INPUT> {
                new() { Type = Win32Api.INPUT_MOUSE, Union = new() { Mouse = new() { Flags = Win32Api.MOUSEEVENTF_LEFTDOWN } } },
                new() { Type = Win32Api.INPUT_MOUSE, Union = new() { Mouse = new() { Flags = Win32Api.MOUSEEVENTF_LEFTUP } } }
            });
        }
        finally { Win32Api.SetCursorPos(current.X, current.Y); }
    }

    public async Task UndoLastActionAsync()
    {
        if (!CanUndo) return;
        var action = _actionHistory.Pop();
        if (action.Type is ActionType.Keystroke or ActionType.SetText)
            await ExecuteActionAsync(new AutomationAction { Type = ActionType.Keystroke, Value = "{CTRL+Z}" });
    }

    public void ClearHistory() => _actionHistory.Clear();
}
