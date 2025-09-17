using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace PD3AudioModder.util
{
    public class HotkeyManager
    {
        private readonly Dictionary<string, Action> _hotkeys = new Dictionary<string, Action>();
        private readonly Window _window;

        public HotkeyManager(Window window)
        {
            _window = window;
            _window.KeyDown += OnKeyDown;
        }

        /// <summary>
        /// Register a hotkey with the specified key combination and action
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="modifiers">Key modifiers (Ctrl, Shift, Alt)</param>
        /// <param name="action">Action to execute when hotkey is pressed</param>
        public void RegisterHotkey(Key key, KeyModifiers modifiers, Action action)
        {
            string hotkeyString = GetHotkeyString(key, modifiers);
            _hotkeys[hotkeyString] = action;
        }

        /// <summary>
        /// Unregister a hotkey
        /// </summary>
        /// <param name="key">The primary key</param>
        /// <param name="modifiers">Key modifiers</param>
        public void UnregisterHotkey(Key key, KeyModifiers modifiers)
        {
            string hotkeyString = GetHotkeyString(key, modifiers);
            _hotkeys.Remove(hotkeyString);
        }

        /// <summary>
        /// Clear all registered hotkeys
        /// </summary>
        public void ClearHotkeys()
        {
            _hotkeys.Clear();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Don't process if text input is focused
            if (IsTextInputFocused())
                return;

            string hotkeyString = GetHotkeyString(e.Key, e.KeyModifiers);

            if (_hotkeys.TryGetValue(hotkeyString, out Action? action))
            {
                action?.Invoke();
                e.Handled = true;
            }
        }

        private string GetHotkeyString(Key key, KeyModifiers modifiers)
        {
            return $"{modifiers}+{key}";
        }

        private bool IsTextInputFocused()
        {
            var focusedElement = _window.FocusManager?.GetFocusedElement();

            return focusedElement is TextBox
                || focusedElement is NumericUpDown
                || focusedElement is ComboBox
                || (
                    focusedElement is Control control
                    && control.Focusable
                    && (
                        control.GetType().Name.Contains("TextBox")
                        || control.GetType().Name.Contains("ComboBox")
                        || control.GetType().Name.Contains("AutoCompleteBox")
                    )
                );
        }

        /// <summary>
        /// Dispose of the hotkey manager and unsubscribe from events.
        /// </summary>
        public void Dispose()
        {
            _window.KeyDown -= OnKeyDown;
            _hotkeys.Clear();
        }
    }
}
