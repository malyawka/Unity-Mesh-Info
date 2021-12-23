using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace MeshExtensions.Editor
{
    public class TableView : TreeView
    {
        protected int freeID = 0;
        
        public TableView(TreeViewState state, MultiColumnHeader multiColumnHeader) : base(state, multiColumnHeader)
        {
            freeID = 0;

            showAlternatingRowBackgrounds = true;
            showBorder = true;
            cellMargin = 1;

            multiColumnHeader.canSort = false;
            multiColumnHeader.ResizeToFit();
        }
        
        public int GetNewID()
        {
            int id = freeID;
            freeID += 1;
            return id;
        }
        
        public void AddElement(object[] properties)
        {
            var rows = GetRows();
            var item = TableViewItem.Create(properties, this);

            rootItem.AddChild(item);
            rows.Add(item);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            GUI.enabled = false;
            var item = (TableViewItem)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                Rect r = args.GetCellRect(i);
                int column = args.GetColumn(i);
                int idx = column;

                switch (item.properties[idx])
                {
                    case int n:
                        EditorGUI.DelayedIntField(r, GUIContent.none, n, new GUIStyle("label"));
                        DrawDivider(r, -1.0f);
                        break;
                    case float f:
                        EditorGUI.DelayedFloatField(r, GUIContent.none, f, new GUIStyle("label"));
                        DrawDivider(r);
                        break;
                    case string s:
                        EditorGUI.DelayedTextField(r, GUIContent.none, s, new GUIStyle("label"));
                        DrawDivider(r);
                        break;
                }
            }

            GUI.enabled = true;
        }
        
        protected void DrawDivider(Rect r, float xMaxOffset = 0.0f)
        {
            Rect dividerRect = new Rect(r.xMax + xMaxOffset, r.y, 1f, r.height);
            EditorGUI.DrawRect(dividerRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        protected override TreeViewItem BuildRoot()
        {
            TreeViewItem root = new TreeViewItem();
            root.depth = -1;
            root.id = -1;
            root.parent = null;
            root.children = new List<TreeViewItem>();
            
            return root;
        }
    }

    public class TableViewItem : TreeViewItem
    {
        public object[] properties;
        
        public static TableViewItem Create(object[] properties, TableView tableView)
        {
            TableViewItem item = new TableViewItem();
            item.children = new List<TreeViewItem>();
            item.depth = 0;
            item.id = tableView.GetNewID();
            item.properties = properties;

            return item;
        }
    }
}
