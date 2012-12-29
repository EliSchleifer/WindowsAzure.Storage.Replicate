using System.Xml.Linq;
using Microsoft.WindowsAzure.Storage.Table;

namespace WindowsAzure.Storage.Replicate
{
    internal class BackupEntity : TableEntity
    {
        internal XElement EntryElement { get; set; }
    }

    public class Constants
    {
        public const string Copy = "blob_copy";
        public const string InProgress = "in_progress";
        public const string Finished = "finished";
        public const string Failed = "failed";
    }

    public class ContainerReplicateOperation : TableEntity
    {
        public string Container { get; set; }
        public string Operation { get; set; }
        public string Status { get; set; }
        public string Hash { get; set; }
    }

    public class BlobReplicateOperation : TableEntity
    {
        public string Container { get; set; }
        public string Blob { get; set; }
        public string Operation { get; set; }
        public string Status { get; set; }
    }
}
