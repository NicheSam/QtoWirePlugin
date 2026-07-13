using System.Collections.Generic;

namespace QtoWirePlugin
{
    public enum QtoCadChangeKind
    {
        Added,
        Modified,
        Deleted,
        Missing,
        CopiedOrPasted,
        ArrayItem,
        Error
    }

    public static class QtoChangePerformancePolicy
    {
        public static bool RequiresSyncIdScan(IEnumerable<QtoCadChangeKind> changeKinds)
        {
            if (changeKinds == null)
            {
                return false;
            }

            foreach (QtoCadChangeKind kind in changeKinds)
            {
                if (kind == QtoCadChangeKind.Added)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ShouldUseBackgroundDebounce(bool hasBatchSubscriber)
        {
            return hasBatchSubscriber;
        }
    }
}
