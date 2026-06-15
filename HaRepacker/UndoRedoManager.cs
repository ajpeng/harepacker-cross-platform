using HaRepacker.GUI.Panels;
using HaRepacker.Models;
using System.Collections.Generic;

namespace HaRepacker
{
    public class UndoRedoManager
    {
        public List<UndoRedoBatch> UndoList = new List<UndoRedoBatch>();
        public List<UndoRedoBatch> RedoList = new List<UndoRedoBatch>();
        private readonly MainPanel parentPanel;

        public UndoRedoManager(MainPanel parentPanel)
        {
            this.parentPanel = parentPanel;
        }

        public void AddUndoBatch(List<UndoRedoAction> actions)
        {
            UndoList.Add(new UndoRedoBatch { Actions = actions });
            RedoList.Clear();
        }

        public static UndoRedoAction ObjectAdded(WzNode parent, WzNode item)
            => new UndoRedoAction(item, parent, UndoRedoType.ObjectAdded);

        public static UndoRedoAction ObjectRemoved(WzNode parent, WzNode item)
            => new UndoRedoAction(item, parent, UndoRedoType.ObjectRemoved);

        public static UndoRedoAction ObjectRenamed(WzNode parent, WzNode item)
            => new UndoRedoAction(item, parent, UndoRedoType.ObjectRenamed);

        public void Undo()
        {
            if (UndoList.Count == 0) return;
            var batch = UndoList[UndoList.Count - 1];
            batch.UndoRedo();
            batch.SwitchActions();
            UndoList.RemoveAt(UndoList.Count - 1);
            RedoList.Add(batch);
        }

        public void Redo()
        {
            if (RedoList.Count == 0) return;
            var batch = RedoList[RedoList.Count - 1];
            batch.UndoRedo();
            batch.SwitchActions();
            RedoList.RemoveAt(RedoList.Count - 1);
            UndoList.Add(batch);
        }
    }

    public class UndoRedoBatch
    {
        public List<UndoRedoAction> Actions = new List<UndoRedoAction>();

        public void UndoRedo()
        {
            foreach (var action in Actions) action.UndoRedo();
        }

        public void SwitchActions()
        {
            foreach (var action in Actions) action.SwitchAction();
        }
    }

    public class UndoRedoAction
    {
        private readonly WzNode item;
        private readonly WzNode parent;
        private UndoRedoType type;

        public UndoRedoAction(WzNode item, WzNode parent, UndoRedoType type)
        {
            this.item = item;
            this.parent = parent;
            this.type = type;
        }

        public void UndoRedo()
        {
            switch (type)
            {
                case UndoRedoType.ObjectAdded:
                    item.DeleteNode();
                    parent.Nodes.Remove(item);
                    break;
                case UndoRedoType.ObjectRemoved:
                    parent.AddChildNode(item);
                    break;
            }
        }

        public void SwitchAction()
        {
            switch (type)
            {
                case UndoRedoType.ObjectAdded:
                    type = UndoRedoType.ObjectRemoved;
                    break;
                case UndoRedoType.ObjectRemoved:
                    type = UndoRedoType.ObjectAdded;
                    break;
            }
        }
    }

    public enum UndoRedoType
    {
        ObjectAdded,
        ObjectRemoved,
        ObjectRenamed,
        ObjectChanged
    }
}
