﻿using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using ImGuiNET;
using System;
using System.Numerics;

namespace OofPluginFixed
{
    public partial class PluginUI
    {

        private class XBounds
        {
            public float Start;
            public float End;

        }
        /// <summary>
        /// calculate x bounds of an element inside window
        /// </summary>
        /// <param name="padding"></param>
        /// <returns></returns>
        private static XBounds calculateSectionBoundsX(float padding)
        {
            float windowStart = ImGui.GetWindowPos().X;
            var start = windowStart + ImGui.GetWindowContentRegionMin().X + padding;
            var end = windowStart + ImGui.GetWindowWidth() - ImGui.GetWindowContentRegionMin().X - padding;
            return new XBounds { Start = start, End = end };
        }
        /// <summary>
        /// create section with a filled background
        /// 
        /// https://github.com/ocornut/imgui/issues/1496#issuecomment-1200143122
        /// </summary>
        public static void SectionStart(float height = 0f)
        {
            ImGui.GetWindowDrawList().ChannelsSplit(2);
            // Draw content above the rectangle
            ImGui.GetWindowDrawList().ChannelsSetCurrent(1);
            var WindowPos = ImGui.GetWindowPos();
            var padding = ImGui.GetStyle().WindowPadding;
            var boundsX = calculateSectionBoundsX(padding.X);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + padding.Y);
            ImGui.BeginGroup();
            if (padding.Y > 0)
            {
                ImGui.Indent(padding.Y);
            }
            var panelMin = new Vector2(boundsX.Start, ImGui.GetItemRectMin().Y);
            var panelMax = new Vector2(boundsX.End, height);
            ImGui.GetWindowDrawList().PushClipRect(panelMin, panelMax, false);

        }
        /// <summary>
        /// end section with filled bg
        /// </summary>
        /// <param name="color"></param>
        public static void SectionEnd(ref float height, ImGuiCol bg = ImGuiCol.MenuBarBg, ImGuiCol border = ImGuiCol.TableBorderLight)
        {
            var padding = ImGui.GetStyle().WindowPadding;
            ImGui.PopClipRect();

            // ImGui.PopClipRect();
            if (padding.X > 0)
            {
                ImGui.Unindent(padding.X);
            }
            ImGui.EndGroup();
            // Essentially, the content is drawn with padding
            // while the rectangle is drawn without padding
            var boundsX = calculateSectionBoundsX(0.0f);

            // GetItemRectMin is going to include the padding
            // as well; so, remove it

            var panelMin = new Vector2(boundsX.Start, ImGui.GetItemRectMin().Y - padding.Y);
            var panelMax = new Vector2(boundsX.End, ImGui.GetItemRectMax().Y + padding.Y);
            height = ImGui.GetItemRectMax().Y;
            // Draw rectangle below
            ImGui.GetWindowDrawList().ChannelsSetCurrent(0);
            ImGui.GetWindowDrawList().AddRectFilled(
                panelMin, panelMax,
                ImGui.GetColorU32(bg),
                ImGui.GetStyle().FrameRounding);
            //ImGui.GetWindowDrawList().ChannelsMerge();
            ImGui.GetWindowDrawList().AddRect(
                panelMin, panelMax,
                ImGui.GetColorU32(border),
                ImGui.GetStyle().FrameRounding, ImDrawFlags.None, 1.0f);

            ImGui.GetWindowDrawList().ChannelsMerge();
            // Since rectangle is bigger than the box, move the cursor;
            // so, it starts outside the box
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + padding.Y);


        }
        public void SectionHeader(string title, ref bool toggle, Action action1)
        {
            var padding = ImGui.GetStyle().WindowPadding;
            var text = toggle ? "Enabled" : "Disabled";
            // var color = toggle ? ImGuiColors.DalamudWhite2 : ImGuiColors.DalamudGrey;

            var textSize = ImGui.CalcTextSize(text);
            ImGui.AlignTextToFramePadding();

            if (ToggleButton($"{title}###${title}", ref toggle))
            {
                action1();
                configuration.Save();
            }
            ImGui.SameLine();
            ImGuiHelpers.SafeTextColoredWrapped(ImGuiColors.DalamudWhite2, title);

            ImGui.SameLine(ImGui.GetWindowWidth() - textSize.X - ImGui.GetFontSize() * 1.8f - padding.X * 2);
            ImGuiHelpers.SafeTextColoredWrapped(ImGuiColors.DalamudWhite2, $"{text}");
            ImGui.SameLine();
            var iconString = toggle ? FontAwesomeIcon.CheckSquare.ToIconString() : FontAwesomeIcon.SquareXmark.ToIconString();
            IconTextColor(iconString, ImGuiColors.DalamudWhite2);
            ImGui.Spacing();
        }

        private static void IconTextColor(string text, Vector4 color = new Vector4())
        {
            if (color == Vector4.Zero) color = ImGuiColors.DalamudWhite;
            ImGui.PushFont(UiBuilder.IconFont);
            ImGuiHelpers.SafeTextColoredWrapped(color, text);
            ImGui.PopFont();
        }

        private static float CalcButtonSize(string text)
        {
            return ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2;
        }
        private static float CalcButtonSize(float value)
        {
            return value + ImGui.GetStyle().FramePadding.X * 2;
        }
        private void LoadAudioUI()
        {
            var WindowPos = ImGui.GetWindowPos();
            var windowPadding = ImGui.GetStyle().WindowPadding;
            var em = ImGui.GetFontSize();
            var draw = ImGui.GetWindowDrawList();

            ImGui.AlignTextToFramePadding();

            ImGuiHelpers.SafeTextColoredWrapped(ImGuiColors.DalamudGrey, "Loaded SoundFile");
            ImGuiComponents.HelpMarker(
               "You can upload a custom audio file from computer. ");
            ImGui.SameLine(ImGui.GetWindowWidth() - CalcButtonSize(em) - windowPadding.X);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.UndoAlt))
            {
                configuration.DefaultSoundImportPath = string.Empty;
                configuration.Save();
                plugin.LoadSoundFile();

            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Reset audio file to oof");

            ImGui.PushFont(UiBuilder.IconFont);
            if (CornerButton(FontAwesomeIcon.PlayCircle.ToIconString(), ImDrawFlags.RoundCornersLeft)) plugin.PlaySound(plugin.CancelToken.Token);
            ImGui.PopFont();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Play");

            ImGui.SameLine(0, 0);

            ImGui.PushFont(UiBuilder.IconFont);
            if (CornerButton(FontAwesomeIcon.StopCircle.ToIconString(), ImDrawFlags.RoundCornersRight)) plugin.StopSound();
            ImGui.PopFont();

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stop");

            ImGui.SameLine();
            var soundFileName = "Original Oof.wav";

            if (configuration.DefaultSoundImportPath.Length > 0)
            {
                var formatString = getFileName().Match(configuration.DefaultSoundImportPath);
                if (formatString.Success) soundFileName = formatString.Value;

            }
            customDraggableText(soundFileName);

            var browseText = "Upload Audio";

            ImGui.SameLine(ImGui.GetWindowWidth() - CalcButtonSize(browseText) - windowPadding.X);

            if (ImGui.Button(browseText))
            {
                void UpdatePath(bool success, string path)
                {
                    if (!success || path.Length == 0) return;

                    configuration.DefaultSoundImportPath = path;
                    configuration.Save();
                    plugin.LoadSoundFile();
                }

                manager.OpenFileDialog("Open Audio File...", "Audio{.wav,.mp3,.aac,.wma}", UpdatePath);
            }
            ImGui.Spacing();
        }
        /// <summary>
        /// theres no reason to use this over an input box but it was fun to make
        /// makes textbox draggable if text overflows
        /// </summary>
        private static void customDraggableText(string text)
        {
            var WindowPos = ImGui.GetWindowPos();
            var draw = ImGui.GetWindowDrawList();
            var em = ImGui.GetFontSize();

            var cursorPos = ImGui.GetCursorPos();
            var panelMin = new Vector2(cursorPos.X + WindowPos.X, ImGui.GetItemRectMin().Y);
            var panelMax = new Vector2(WindowPos.X + ImGui.GetContentRegionAvail().X - CalcButtonSize(em) - ImGui.GetStyle().ItemSpacing.X, ImGui.GetItemRectMax().Y);
            var boxSize = panelMax - panelMin;
            var framePadding = ImGui.GetStyle().FramePadding;

            var shouldScroll = false;
            if (ImGui.CalcTextSize(text).X > boxSize.X) shouldScroll = true;


            ImGui.GetWindowDrawList().PushClipRect(panelMin, panelMax, true);

            ImGui.SetCursorPos(cursorPos);
            ImGui.InvisibleButton("###customDraggableText", panelMax - panelMin);
            if (ImGui.IsItemHovered() && shouldScroll) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
            var dist = panelMin.X;

            if (ImGui.IsItemActive() && shouldScroll)
            {
                var io = ImGui.GetIO();
                var newDist = io.MousePos.X - io.MouseClickedPos[0].X;
                if (panelMin.X + newDist > panelMin.X) newDist = 0;
                else if (panelMin.X + newDist + ImGui.CalcTextSize(text).X < panelMax.X) newDist = boxSize.X - ImGui.CalcTextSize(text).X;

                dist = panelMin.X + newDist;
                //draw.AddLine(io.MouseClickedPos[0], io.MousePos, ImGui.GetColorU32(ImGuiCol.Button), 4.0f);
            }
            draw.AddText(new Vector2(dist, panelMin.Y + framePadding.Y), ImGui.ColorConvertFloat4ToU32(ImGuiColors.DalamudWhite), text);
            ImGui.GetWindowDrawList().PopClipRect();


            draw.AddRect(new Vector2(panelMin.X - framePadding.X, panelMin.Y), new Vector2(panelMax.X + framePadding.X, panelMax.Y), ImGui.GetColorU32(ImGuiCol.TableBorderLight), ImGui.GetStyle().FrameRounding, ImDrawFlags.None, 1.0f);
        }
        /// <summary>
        /// Draw a toggle button.
        /// originally from https://github.com/goatcorp/Dalamud/blob/6b185156ab566203378159b8d6fa209434b23494/Dalamud/Interface/Components/ImGuiComponents.ToggleSwitch.cs
        /// </summary>
        /// <param name="id">The id of the button.</param>
        /// <param name="v">The state of the switch.</param>
        /// <returns>If the button has been interacted with this frame.</returns>
        public static bool ToggleButton(string id, ref bool v)
        {
            var colors = ImGui.GetStyle().Colors;
            var p = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();

            var height = ImGui.GetFrameHeight();
            var width = height * 1.55f;
            var radius = height * 0.50f;

            // TODO: animate

            var changed = false;
            ImGui.InvisibleButton(id, new Vector2(width, height));
            if (ImGui.IsItemClicked())
            {
                v = !v;
                changed = true;
            }

            if (ImGui.IsItemHovered())
                drawList.AddRectFilled(p, new Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(!v ? colors[(int)ImGuiCol.ButtonHovered] : colors[(int)ImGuiCol.HeaderHovered]), height * 0.5f);
            else
                drawList.AddRectFilled(p, new Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(!v ? colors[(int)ImGuiCol.Button] * 0.6f : colors[(int)ImGuiCol.Header]), height * 0.50f);
            drawList.AddRect(p, new Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(!v ? ImGuiCol.TableBorderLight : ImGuiCol.TableBorderStrong), height * 0.5f, ImDrawFlags.None, 1.0f);

            drawList.AddCircleFilled(new Vector2(p.X + radius + ((v ? 1 : 0) * (width - (radius * 2.0f))), p.Y + radius), radius - 1.5f, ImGui.GetColorU32(ImGuiColors.DalamudWhite2));
            return changed;
        }
        public static bool CornerButton(string text, ImDrawFlags flags = ImDrawFlags.None)
        {

            var p = ImGui.GetCursorScreenPos();
            var drawList = ImGui.GetWindowDrawList();
            var framePadding = ImGui.GetStyle().FramePadding;
            var frameRounding = ImGui.GetStyle().FrameRounding;

            var textsize = ImGui.CalcTextSize(text);
            var boxsize = new Vector2(textsize.X + framePadding.X * 2, textsize.Y + framePadding.Y * 2);
            // TODO: animate
            // Draw content above the rectangle
            var changed = false;
            ImGui.InvisibleButton(text, boxsize);
            if (ImGui.IsItemClicked())
            {

                changed = true;
            }

            if (ImGui.IsItemActive())
                drawList.AddRectFilled(p, p + boxsize, ImGui.GetColorU32(ImGuiCol.ButtonActive), frameRounding, flags);
            else if (ImGui.IsItemHovered())
                drawList.AddRectFilled(p, p + boxsize, ImGui.GetColorU32(ImGuiCol.ButtonHovered), frameRounding, flags);

            else
                drawList.AddRectFilled(p, p + boxsize, ImGui.GetColorU32(ImGuiCol.Button), frameRounding, flags);

            drawList.AddText(p + framePadding, ImGui.GetColorU32(ImGuiCol.Text), text);


            return changed;
        }

    }
}
