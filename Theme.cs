using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace DutyDM
{
    /// <summary>
    /// Shared blue-slate theme, kept consistent with the PF Presets plugin so all of
    /// Mayo Botic's plugins look the same. Colors and helpers are lifted from PfPresets.
    /// </summary>
    internal static class Theme
    {
        // ── Palette ───────────────────────────────────────────────
        public static readonly Vector4 BgOuter = Hex("#212c3e");
        public static readonly Vector4 BgCard = Hex("#2a3850");
        public static readonly Vector4 BgCardExpanded = Hex("#1b2536");
        public static readonly Vector4 BgDropdown = Hex("#28344a");
        public static readonly Vector4 BorderDefault = Hex("#3a4860");
        public static readonly Vector4 BorderHover = Hex("#4d5e7e");
        public static readonly Vector4 BorderActiveAccent = Hex("#6fa8dc55");
        public static readonly Vector4 TextPrimary = Hex("#dfe6f1");
        public static readonly Vector4 TextSecondary = Hex("#9aa7bb");
        public static readonly Vector4 TextMuted = Hex("#6e7c91");
        public static readonly Vector4 AccentBlue = Hex("#6fa8dc");
        public static readonly Vector4 AccentGreen = Hex("#3fb56a");
        public static readonly Vector4 AccentRed = Hex("#e06a5a");
        public static readonly Vector4 AccentYellow = Hex("#ffbd2e");

        // Action-button colors (match the PfPresets "Apply"/"Create" green and "kebab" grey).
        public static readonly Vector4 OkBg = Hex("#2f7d4e");
        public static readonly Vector4 OkHover = Hex("#379059");
        public static readonly Vector4 NeutralBg = Hex("#36435a");
        public static readonly Vector4 NeutralHover = Hex("#414f68");
        public static readonly Vector4 TitleBg = Hex("#2b3850");

        // Ko-fi's signature red, with lighter hover / darker active states.
        public static readonly Vector4 KofiRed = Hex("#ff5e5b");
        public static readonly Vector4 KofiRedHover = Hex("#ff7572");
        public static readonly Vector4 KofiRedActive = Hex("#e8504d");
        public static readonly Vector4 KofiText = Hex("#fff4f3");

        /// <summary>Pushes the shared ImGui colors so every window matches the PfPresets look.
        /// Returns the number of colors pushed (pass to PopStyleColor).</summary>
        public static int PushColors()
        {
            (ImGuiCol Col, Vector4 Value)[] theme =
            {
                (ImGuiCol.TitleBg, TitleBg),
                (ImGuiCol.TitleBgActive, TitleBg),
                (ImGuiCol.TitleBgCollapsed, TitleBg),
                (ImGuiCol.FrameBg, BgCard),
                (ImGuiCol.FrameBgHovered, BorderDefault),
                (ImGuiCol.FrameBgActive, BorderHover),
                (ImGuiCol.PopupBg, BgDropdown),
                (ImGuiCol.Header, Hex("#33405a")),
                (ImGuiCol.HeaderHovered, BorderHover),
                (ImGuiCol.HeaderActive, BorderActiveAccent),
                (ImGuiCol.CheckMark, AccentBlue),
                (ImGuiCol.ScrollbarBg, new Vector4(0, 0, 0, 0)),
                (ImGuiCol.ScrollbarGrab, BorderDefault),
                (ImGuiCol.ScrollbarGrabHovered, BorderHover),
                (ImGuiCol.ScrollbarGrabActive, AccentBlue),
                (ImGuiCol.Separator, BorderDefault),
                (ImGuiCol.Text, TextPrimary),
                (ImGuiCol.TextDisabled, TextMuted),
                (ImGuiCol.Button, BgCard),
                (ImGuiCol.ButtonHovered, BorderHover),
                (ImGuiCol.ButtonActive, BorderActiveAccent),
            };
            foreach (var (col, value) in theme)
                ImGui.PushStyleColor(col, value);
            return theme.Length;
        }

        /// <summary>A small blue section heading, matching PfPresets' DrawSectionLabel.</summary>
        public static void SectionLabel(string label)
        {
            ImGui.TextColored(AccentBlue, label);
            ImGui.Dummy(new Vector2(0, 2));
        }

        /// <summary>A themed checkbox. Returns true when toggled.</summary>
        public static bool Checkbox(string label, ref bool value)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, BgCard);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Hex("#1c2230"));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Hex("#243a54"));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, AccentBlue);
            ImGui.PushStyleColor(ImGuiCol.Border, BorderDefault);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3.0f);
            bool changed = ImGui.Checkbox(label, ref value);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor(5);
            return changed;
        }

        /// <summary>A framed text input matching the PfPresets editor fields.</summary>
        public static bool InputField(string id, string hint, ref string value, int maxLength, float width = -1)
        {
            ImGui.PushStyleColor(ImGuiCol.FrameBg, BgCardExpanded);
            ImGui.PushStyleColor(ImGuiCol.Border, BorderDefault);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 7));
            ImGui.SetNextItemWidth(width);
            bool changed = ImGui.InputTextWithHint(id, hint, ref value, maxLength);
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(2);
            return changed;
        }

        /// <summary>Green primary action button (the PfPresets "Apply"/"Save" style).</summary>
        public static bool PrimaryButton(string label, Vector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, OkBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, OkHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, OkHover);
            ImGui.PushStyleColor(ImGuiCol.Text, Hex("#eafff0"));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);
            bool clicked = ImGui.Button(label, size);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
            return clicked;
        }

        /// <summary>Neutral grey button (the PfPresets "Cancel"/secondary style).</summary>
        public static bool SecondaryButton(string label, Vector2 size)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, NeutralBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, NeutralHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, NeutralHover);
            ImGui.PushStyleColor(ImGuiCol.Text, TextPrimary);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6.0f);
            bool clicked = ImGui.Button(label, size);
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
            return clicked;
        }

        // Ko-fi button frame padding; shared so the width estimate matches the draw.
        private static readonly Vector2 KofiPadding = new(12, 7);

        /// <summary>A cute red Ko-fi button with a heart icon. Auto-sizes to its label.</summary>
        public static bool KofiButton(string label)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, KofiText);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, KofiPadding);
            bool clicked = ImGuiComponents.IconButtonWithText(
                FontAwesomeIcon.Heart, label, KofiRed, KofiRedActive, KofiRedHover);
            ImGui.PopStyleVar(2);
            ImGui.PopStyleColor();
            return clicked;
        }

        /// <summary>Approximate width <see cref="KofiButton"/> will occupy, so callers can
        /// right-align or centre it. Slightly over-estimates the heart glyph (~1em) so the
        /// button never clips the window edge.</summary>
        public static float KofiButtonWidth(string label)
        {
            float iconWidth = ImGui.GetFontSize();              // FontAwesome heart ≈ 1em
            float gap = ImGui.GetStyle().ItemInnerSpacing.X;    // icon-to-text spacing
            float textWidth = ImGui.CalcTextSize(label).X;
            return (KofiPadding.X * 2) + iconWidth + gap + textWidth + 4f;
        }

        public static Vector4 Hex(string hex)
        {
            hex = hex.TrimStart('#');
            float r, g, b, a = 1.0f;
            r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
            g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
            b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
            if (hex.Length == 8)
                a = Convert.ToInt32(hex.Substring(6, 2), 16) / 255f;
            return new Vector4(r, g, b, a);
        }
    }
}
