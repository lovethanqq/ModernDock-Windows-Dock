using System.Collections.Generic;

namespace MyCustomDock
{
    public static class FixedItemOrder
    {
        public static bool Move(IList<DockItem> items, DockItem item, int targetIndex)
        {
            if (items == null || item == null || !item.IsFixed) return false;

            int currentIndex = -1;
            for (int i = 0; i < items.Count; i++)
            {
                if (object.ReferenceEquals(items[i], item))
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0 || items.Count < 2) return false;
            targetIndex = targetIndex < 0 ? 0 : targetIndex;
            targetIndex = targetIndex > items.Count - 1 ? items.Count - 1 : targetIndex;
            if (currentIndex == targetIndex) return false;

            items.RemoveAt(currentIndex);
            if (targetIndex > items.Count) targetIndex = items.Count;
            items.Insert(targetIndex, item);
            return true;
        }
    }
}
