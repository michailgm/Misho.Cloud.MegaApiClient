using System.Collections.Generic;

namespace Misho.Cloud.MegaNz
{
    public interface IAccountInformation
    {
        long TotalQuota { get; }

        long UsedQuota { get; }

        IEnumerable<IStorageMetrics> Metrics { get; }
    }

    public interface IStorageMetrics
    {
        string NodeId { get; }

        long BytesUsed { get; }

        long FilesCount { get; }

        long FoldersCount { get; }
    }
}