/* Copyright (c) 2026, ajpeng https://github.com/ajpeng/harepacker-cross-platform */
using Avalonia.Controls;
using HaCreator.MapEditor;

namespace HaCreator.GUI
{
    public class TabItemContainer
    {
        public string Text { get; set; }
        public MultiBoard MultiBoard { get; }
        public string Tooltip { get; }
        public ContextMenu Menu { get; }
        public Board Board { get; }

        public TabItemContainer(string text, MultiBoard multiBoard, string tooltip, ContextMenu menu, Board board)
        {
            Text = text;
            MultiBoard = multiBoard;
            Tooltip = tooltip;
            Menu = menu;
            Board = board;
        }
    }
}
