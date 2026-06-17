/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using HaCreator.Collections;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.Instance.Shapes;
using MapleLib.WzLib.WzStructure.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using XNA = Microsoft.Xna.Framework;

namespace HaCreator.MapEditor
{
    public class BoardItemContextMenu
    {
        private MultiBoard multiboard;
        private Board board;
        private BoardItem target;
        private ContextMenu? _menu;

        public BoardItemContextMenu(MultiBoard multiboard, Board board, BoardItem target)
        {
            this.multiboard = multiboard;
            this.board = board;
            this.target = target;
        }

        public ContextMenu Menu
        {
            get { return _menu ??= BuildMenu(); }
        }

        private ContextMenu BuildMenu()
        {
            ContextMenu menu = new ContextMenu();
            List<MenuItem> generalCategory = new();
            List<MenuItem> zCategory = new();
            List<MenuItem> platformCategory = new();

            MenuItem editInstance = new MenuItem { Header = "Edit this instance..." };
            editInstance.Click += (s, e) => editInstance_Click(s, e);
            generalCategory.Add(editInstance);

            if (target is PortalInstance portal && portal.tm != MapConstants.MaxMap)
            {
                MenuItem loadTargetMap = new MenuItem { Header = "Load target map in a new tab" };
                loadTargetMap.Click += (s, e) => LoadPortalTargetMap_Click(s, e);
                generalCategory.Add(loadTargetMap);
            }

            if (target is ToolTipInstance tt && tt.CharacterToolTip == null)
            {
                MenuItem addChar = new MenuItem { Header = "Add Character Tooltip" };
                addChar.Click += (s, e) => addChar_Click(s, e);
                generalCategory.Add(addChar);
            }

            if (target is BackgroundInstance || target is LayeredItem)
            {
                MenuItem bringToFront = new MenuItem { Header = "Bring to Front" };
                bringToFront.Click += (s, e) => bringToFront_Click(s, e);
                zCategory.Add(bringToFront);

                MenuItem sendToBack = new MenuItem { Header = "Send to Back" };
                sendToBack.Click += (s, e) => sendToBack_Click(s, e);
                zCategory.Add(sendToBack);
            }

            if (target is FootholdAnchor)
            {
                MenuItem selectPlat = new MenuItem { Header = "Select Connected" };
                selectPlat.Click += (s, e) => selectPlat_Click(s, e);
                platformCategory.Add(selectPlat);
            }

            if (target is IContainsLayerInfo)
            {
                MenuItem moveLayer = new MenuItem { Header = "Change Layer/Platform..." };
                moveLayer.Click += (s, e) => moveLayer_Click(s, e);
                platformCategory.Add(moveLayer);
            }

            if (target is IContainsLayerInfo || (target is FootholdAnchor && GetZmOfSelectedFoothold() != -1))
            {
                MenuItem selectZm = new MenuItem { Header = "Select Platform" };
                selectZm.Click += (s, e) => selectZm_Click(s, e);
                platformCategory.Add(selectZm);
            }

            bool hasItems = false;
            foreach (List<MenuItem> currList in new[] { generalCategory, zCategory, platformCategory })
            {
                if (currList.Count > 0)
                {
                    if (hasItems) menu.Items.Add(new Separator());
                    foreach (MenuItem item in currList) menu.Items.Add(item);
                    hasItems = true;
                }
            }
            return menu;
        }

        private void LoadPortalTargetMap_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (target is PortalInstance portal && portal.tm != MapConstants.MaxMap)
                multiboard.HaCreatorStateManager?.LoadMap(portal.tm);
        }

        private void addChar_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToolTipInstance tt = (ToolTipInstance)target;
            tt.CreateCharacterTooltip(new XNA.Rectangle(tt.Left - 50, tt.Top - 50, tt.Width + 100, tt.Height + 100));
        }

        private void moveLayer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            List<BoardItem> items;
            lock (multiboard)
            {
                // Expand foothold anchors to include connected peers
                for (int i = 0; i < board.SelectedItems.Count; i++)
                    if (board.SelectedItems[i] is FootholdAnchor fa)
                        foreach (FootholdAnchor x in new AnchorEnumerator(fa))
                            x.Selected = true;
                items = board.SelectedItems.ToList();
            }
            HaCreator.GUI.LayerChangeDialog.DispatchAsync(
                items, board,
                multiboard.HaCreatorStateManager?.OwnerWindow);
        }

        private int GetZmOfSelectedFoothold()
        {
            if (target is FootholdAnchor anchor)
                foreach (FootholdLine line in anchor.connectedLines)
                    if (line.Selected) return line.PlatformNumber;
            return -1;
        }

        private void selectPlat_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            lock (multiboard)
            {
                if (target is FootholdAnchor fa)
                    foreach (FootholdAnchor x in new AnchorEnumerator(fa))
                        x.Selected = true;
            }
        }

        private void selectZm_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            lock (multiboard)
            {
                int zm = target is IContainsLayerInfo li ? li.PlatformNumber : GetZmOfSelectedFoothold();
                foreach (BoardItem item in target.Board.BoardItems.Items)
                    if (item is IContainsLayerInfo cli && cli.PlatformNumber == zm)
                        item.Selected = true;
                foreach (FootholdLine line in target.Board.BoardItems.FootholdLines)
                    if (line.PlatformNumber == zm)
                    {
                        line.FirstDot.Selected = true;
                        line.SecondDot.Selected = true;
                    }
            }
        }

        private void sendToBack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => multiboard.SendToBackClicked(target);

        private void bringToFront_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => multiboard.BringToFrontClicked(target);

        private void editInstance_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => multiboard.EditInstanceClicked(target);
    }
}
