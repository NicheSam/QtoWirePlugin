namespace QtoWirePlugin
{
    public static class QtoCatalogSnapshotBuilder
    {
        public static QtoCatalogSnapshot Build(QtoBlockCatalog catalog)
        {
            QtoCatalogSnapshot snapshot = new QtoCatalogSnapshot();
            if (catalog == null)
            {
                return snapshot;
            }

            snapshot.IsLoaded = true;
            if (catalog.Items == null)
            {
                return snapshot;
            }

            foreach (QtoBlockCatalogItem source in catalog.Items)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.BlockName))
                {
                    continue;
                }

                snapshot.Add(new QtoCatalogItem
                {
                    CatalogId = source.CatalogId,
                    BlockName = source.BlockName,
                    SystemCode = source.SystemCode,
                    EquipmentTypeCode = source.EquipmentTypeCode,
                    QuantityBasis = source.QuantityBasis,
                    CatalogVersion = source.Version,
                    Status = QtoBlockCatalogStatus.Normalize(source.Status)
                });
            }

            return snapshot;
        }
    }
}
