using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace ClipYourself.Desktop.Interop;

/// <summary>
/// An editable text control in some other app, found by UI Automation under a screen
/// point. Bounds are in the same (process-DPI) screen pixels as GetCursorPos/SetWindowPos.
/// </summary>
public sealed class TextFieldTarget
{
    private TextFieldTarget(AutomationElement element, Rect bounds, IntPtr topWindow)
    {
        Element = element;
        Bounds = bounds;
        TopWindow = topWindow;
    }

    public AutomationElement Element { get; }
    public Rect Bounds { get; }
    public IntPtr TopWindow { get; }

    /// <summary>
    /// Hit-tests the screen. Blocking cross-process COM — call off the UI thread (it can
    /// call back into this very process, and would deadlock on the dispatcher).
    /// </summary>
    public static TextFieldTarget? FromPoint(int x, int y)
    {
        try
        {
            var hit = AutomationElement.FromPoint(new Point(x, y));
            if (hit == null || hit.Current.ProcessId == Environment.ProcessId) return null;

            // FromPoint can land on a decoration inside the field (text run, scroll host) —
            // climb a few levels looking for the editable control itself.
            var walker = TreeWalker.ControlViewWalker;
            var node = hit;
            for (var depth = 0; node != null && depth < 4; depth++)
            {
                if (IsEditableText(node))
                {
                    var bounds = node.Current.BoundingRectangle;
                    if (bounds.IsEmpty || bounds.Width < 2 || bounds.Height < 2) return null;
                    return new TextFieldTarget(node, bounds, FindTopWindow(node, x, y));
                }
                node = walker.GetParent(node);
                if (node == null || node == AutomationElement.RootElement) break;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        catch (InvalidOperationException) { }
        return null;
    }

    private static bool IsEditableText(AutomationElement el)
    {
        var info = el.Current;
        if (!info.IsEnabled) return false;

        var type = info.ControlType;
        var isTextish = type == ControlType.Edit || type == ControlType.Document;
        var isCombo = type == ControlType.ComboBox;
        if (!isTextish && !isCombo) return false;

        // A writable ValuePattern is the clearest "you can type here" signal; a read-only
        // one (a web page's root document, a locked field) rules the control out.
        if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp))
            return !((ValuePattern)vp).Current.IsReadOnly;

        // Combos without a writable value are drop-down-only lists.
        if (isCombo) return false;

        // Rich editors (Notepad, Word, RichEdit) often skip ValuePattern but expose text and
        // take keyboard focus.
        return info.IsKeyboardFocusable
               && (type == ControlType.Edit || el.TryGetCurrentPattern(TextPattern.Pattern, out _));
    }

    private static IntPtr FindTopWindow(AutomationElement el, int x, int y)
    {
        var handle = IntPtr.Zero;
        try { handle = new IntPtr(el.Current.NativeWindowHandle); } catch { }
        if (handle == IntPtr.Zero) handle = WindowFromPoint(new POINT { X = x, Y = y });
        return handle == IntPtr.Zero ? IntPtr.Zero : GetAncestor(handle, GA_ROOT);
    }

    /// <summary>Brings the field's window forward, focuses the field, then types <paramref name="text"/>.</summary>
    public TypeResult FocusAndType(string text, CancellationToken cancel)
    {
        var focus = Focus();
        return focus != TypeResult.Typed ? focus : KeystrokeTyper.Type(text, cancel);
    }

    /// <summary>
    /// Brings the field's window forward, focuses the field, then presses Ctrl+V — the
    /// caller has already put the text on the clipboard.
    /// </summary>
    public TypeResult FocusAndPaste()
    {
        var focus = Focus();
        return focus != TypeResult.Typed ? focus : KeystrokeTyper.Paste();
    }

    /// <summary>Activates and focuses the field. <see cref="TypeResult.Typed"/> means "ready for input".</summary>
    private TypeResult Focus()
    {
        try
        {
            if (TopWindow != IntPtr.Zero)
            {
                if (IsIconic(TopWindow)) ShowWindow(TopWindow, SW_RESTORE);
                SetForegroundWindow(TopWindow);
            }
            Element.SetFocus();
        }
        catch (ElementNotAvailableException) { return TypeResult.TargetGone; }
        catch (InvalidOperationException) { /* not focusable via UIA — the foreground switch may still suffice */ }
        catch (COMException) { }

        // Let the target process actually process the activation/focus change before
        // keystrokes start arriving, or the first few land in the old focus.
        Thread.Sleep(120);

        // SendInput goes to whatever is foreground. If Windows refused the switch, sending
        // keys now would spray them into some other app — bail instead.
        if (TopWindow != IntPtr.Zero && GetAncestor(GetForegroundWindow(), GA_ROOT) != TopWindow)
            return TypeResult.FocusFailed;
        return TypeResult.Typed;
    }

    private const uint GA_ROOT = 2;
    private const int SW_RESTORE = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
}

public enum TypeResult { Typed, Cancelled, Blocked, TargetGone, FocusFailed }

/// <summary>
/// Types text as synthesized keystrokes (never a paste) — the clipboard is left alone,
/// and fields that block paste still accept it.
/// </summary>
public static class KeystrokeTyper
{
    private const int ChunkChars = 48;

    public static TypeResult Type(string text, CancellationToken cancel)
    {
        // A modifier still held from the gesture (Shift-drag) would turn Enter into
        // Shift+Enter or chars into shortcuts — give the user a moment to let go.
        WaitForModifiersReleased(TimeSpan.FromSeconds(2), cancel);

        // Normalize newlines so CRLF becomes a single Enter press.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        var batch = new List<INPUT>(ChunkChars * 2);
        var sent = 0;
        foreach (var c in text)
        {
            // Newlines need a real Enter press (a bare '\r' char is ignored by many editors).
            // Tabs stay a character: a Tab *key* would hop focus to the next control and
            // spill the rest of the text into a field the user never picked.
            if (c == '\n') AddKey(batch, VK_RETURN);
            else AddUnicode(batch, c);

            if (++sent % ChunkChars == 0)
            {
                if (!Flush(batch)) return TypeResult.Blocked;
                // Pace large texts so slow targets (browsers, Electron) don't drop input,
                // and give Esc a chance to abort a long run.
                Thread.Sleep(8);
                if (cancel.IsCancellationRequested || (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
                    return TypeResult.Cancelled;
            }
        }
        return Flush(batch) ? TypeResult.Typed : TypeResult.Blocked;
    }

    /// <summary>Presses Ctrl+V in whatever has keyboard focus.</summary>
    public static TypeResult Paste()
    {
        var batch = new List<INPUT>(4)
        {
            Key(VK_CONTROL, 0, 0),
            Key(VK_V, 0, 0),
            Key(VK_V, 0, KEYEVENTF_KEYUP),
            Key(VK_CONTROL, 0, KEYEVENTF_KEYUP)
        };
        return Flush(batch) ? TypeResult.Typed : TypeResult.Blocked;
    }

    private static void WaitForModifiersReleased(TimeSpan timeout, CancellationToken cancel)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !cancel.IsCancellationRequested &&
               ((GetAsyncKeyState(VK_SHIFT) | GetAsyncKeyState(VK_CONTROL) | GetAsyncKeyState(VK_MENU)) & 0x8000) != 0)
        {
            Thread.Sleep(15);
        }
    }

    private static bool Flush(List<INPUT> batch)
    {
        if (batch.Count == 0) return true;
        var arr = batch.ToArray();
        batch.Clear();
        // UIPI silently drops input aimed at an elevated window — SendInput reports 0.
        return SendInput((uint)arr.Length, arr, Marshal.SizeOf<INPUT>()) == arr.Length;
    }

    private static void AddUnicode(List<INPUT> batch, char c)
    {
        batch.Add(Key(0, c, KEYEVENTF_UNICODE));
        batch.Add(Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
    }

    private static void AddKey(List<INPUT> batch, ushort vk)
    {
        batch.Add(Key(vk, 0, 0));
        batch.Add(Key(vk, 0, KEYEVENTF_KEYUP));
    }

    private static INPUT Key(ushort vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags } }
    };

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SHIFT = 0x10;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_MENU = 0x12;
    private const ushort VK_V = 0x56;
    private const int VK_ESCAPE = 0x1B;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // The union must include MOUSEINPUT (the largest member) so sizeof(INPUT) matches Win32.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);
}
