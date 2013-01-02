using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using NLog;

namespace WindowsAzure.Storage.Replicate
{
    public delegate void BlobContainerEventHandler(object sender, BlobContainer container);

    public class BlobStorage
    {
        public Replicate Replicate { get; private set;}
        public ConcurrentDictionary<string, BlobContainer> InProgress;
        public ConcurrentDictionary<string, BlobContainer> Finished;

        public event BlobContainerEventHandler BeginReplicateContainer;
        public event BlobContainerEventHandler EndReplicateContainer;

        public BlobStorage(Replicate replicate)
        {
            this.Replicate = replicate;
            this.InProgress = new ConcurrentDictionary<string, BlobContainer>();
            this.Finished = new ConcurrentDictionary<string, BlobContainer>();
        }

        private CloudTable containersTable;
        public CloudTable ContainersTable
        {
            get
            {
                if (containersTable == null)
                {
                    var name = string.Format("{0}Containers", this.Replicate.BackupName);
                    var table = Replicate.Target.TableClient.GetTableReference(name);
                    table.CreateIfNotExists();
                    this.containersTable = table;
                }
                return containersTable;
            }
        }

        public bool AreReplicated
        {
            get
            {
                //Edge case this will say false forever for empty blob storage
                return InProgress.Count == 0 && Finished.Count > 0;
            }
        }

        public void OnTimer()
        {
            foreach (var container in InProgress.Values.ToArray())
            {
                container.OnTimer();
            }
        }

        public int BeginReplicate(int limit = int.MaxValue)
        {
            BlobContinuationToken token = null;

            do
            {
                var result = Replicate.Source.BlobClient.ListContainersSegmented(token);

                var containers = result.Results.ToList();

                var supply = containers.Take(Math.Min(limit, containers.Count));
                
                Parallel.ForEach(supply, new ParallelOptions() { MaxDegreeOfParallelism = 5 }, cloudContainer =>
                {                    
                    var c = new BlobContainer(this, cloudContainer);
                    InProgress[cloudContainer.Name] = c;

                    c.MadeProgress += OnMadeProgress;                                                          

                    if (BeginReplicateContainer != null)
                    {
                        BeginReplicateContainer(this, c);
                    }

                    c.BeginReplicate();

                    Interlocked.Decrement(ref limit);
                });
                token = result.ContinuationToken;
            } while(token != null && limit > 0);

            return limit;
        }

        void OnMadeProgress(object sender, BlobContainer container)
        {
            if (container.IsReplicated)
            {
                BlobContainer ignore;
                if (InProgress.TryRemove(container.Name, out ignore))
                {
                    Finished[container.Name] = container;
                    if (EndReplicateContainer != null)
                    {
                        EndReplicateContainer(this, container);
                    }
                }
            }
        }
    }

    public class BlobContainer
    {
        public BlobStorage Storage;
        public CloudBlobContainer Source;
        public CloudBlobContainer Target;
        public List<string> InProgress;
        public List<string> Finished;
        public List<string> Failed;
        public int Total;

        private DateTime lastChecked = DateTime.MaxValue;

        private static Logger logger = LogManager.GetLogger("BlobContainer");

        public event BlobContainerEventHandler MadeProgress;

        public string Name { get { return Source.Name; } }

        public bool IsReplicated
        {
            get
            {
                return Total == Finished.Count;
            }
        }
        
        public BlobContainer(BlobStorage storage, CloudBlobContainer source)
        {
            this.Storage = storage;
            this.Source = source;
            this.InProgress = new List<string>();
            this.Finished = new List<string>();
            this.Failed = new List<string>();            
        }

        private void RaiseMadeProgress()
        {
            if (MadeProgress != null)
            {
                MadeProgress(this, this);
            }
        }

        public void OnTimer()
        {
            if (InProgress.Count == 0)
            {
                return;
            }

            Task.Factory.StartNew(() =>
            {
                if (TimeSpan.FromMinutes(1) > DateTime.UtcNow - lastChecked)
                {
                    // Avoid re-entrrancy by setting lastCheck to MaxTime
                    lastChecked = DateTime.MaxValue;

                    foreach (var dest in Target.ListBlobs(null, true, BlobListingDetails.Copy))
                    {
                        var blob = dest as ICloudBlob;
                        if (!InProgress.Contains(blob.Name))
                        {
                            // do not repeat status changes for finished blobs
                            continue;
                        }

                        if (blob.CopyState.Status == CopyStatus.Success)
                        {
                            var blobRecord = new BlobReplicateOperation()
                            {
                                PartitionKey = Source.Name,
                                RowKey = blob.Name,
                                Operation = Constants.Copy,
                                Status = Constants.Finished,
                                ETag = "*"
                            };
                            Storage.ContainersTable.Execute(TableOperation.Replace(blobRecord));

                            lock (this)
                            {
                                InProgress.Remove(blob.Name);
                                Finished.Add(blob.Name);
                            }
                            RaiseMadeProgress();
                        }
                        else if (blob.CopyState.Status == CopyStatus.Failed ||
                                blob.CopyState.Status == CopyStatus.Aborted)
                        {
                            var blobRecord = new BlobReplicateOperation()
                            {
                                PartitionKey = Source.Name,
                                RowKey = blob.Name,
                                Operation = Constants.Copy,
                                Status = Constants.Failed,
                                ETag = "*"
                            };
                            Storage.ContainersTable.Execute(TableOperation.Replace(blobRecord));

                            lock (this)
                            {
                                InProgress.Remove(blob.Name);
                                Failed.Add(blob.Name);
                            }
                            RaiseMadeProgress();
                        }
                        else if (blob.CopyState.Status == CopyStatus.Invalid)
                        {
                            Debug.Assert(false, "Invalid state what does this mean");
                        }
                        else
                        {
                            // In progress or pending
                            continue;
                        }
                    }

                    if (InProgress.Count == 0)
                    {
                        if (Failed.Count == 0)
                        {
                            logger.Info("EndReplicate {0}", Source.Name);                            
                        }
                        else
                        {
                            logger.Info("EndReplicate {0} WITH ERRORS", Source.Name);
                        }
                        RaiseMadeProgress();
                    }

                    lastChecked = DateTime.UtcNow;
                }
            });
        }

        public static string CalculateHash(CloudBlobContainer container)
        {
            var sb = new StringBuilder();
            
            BlobContinuationToken token = null;
            do
            {
                var result = container.ListBlobsSegmented(token);

                foreach (var blobItem in result.Results.OfType<ICloudBlob>())
                {
                    sb.AppendFormat("{0}{1}{2}", blobItem.Name, blobItem.Properties.Length, blobItem.Properties.LastModified);
                }
                token = result.ContinuationToken;
            } while (token != null);

            return Hash.MD5(sb.ToString());
        }

        public void BeginReplicate()
        {
            logger.Info("BeginReplicate {0}", Source.Name);
    
            var replicate = Storage.Replicate;
            var sb = new StringBuilder();

            string accessSignature = null;
            var permissions = Source.GetPermissions();
            this.Target = replicate.Target.BlobClient.GetContainerReference(Source.Name);

            if (permissions.PublicAccess == BlobContainerPublicAccessType.Off)
            {
                // Must use sharedaccesssignature to copy
                var policy = new SharedAccessBlobPolicy() 
                { 
                    SharedAccessExpiryTime = DateTime.UtcNow.AddHours(12),
                    Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.List
                };
                accessSignature = Source.GetSharedAccessSignature(policy);
            }

            this.Target.CreateIfNotExists();
            this.Target.SetPermissions(permissions);

            BlobContinuationToken token = null;
            do
            {
                var result = Source.ListBlobsSegmented(token);
                
                // Get all records and only call copy on those that don't have records or failed
                var exQuery =
                new TableQuery<BlobReplicateOperation>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal,
                                                                             Source.Name));

                var records = Storage.ContainersTable.ExecuteQuery(exQuery).ToList();

                Parallel.ForEach(result.Results.OfType<ICloudBlob>(), new ParallelOptions() { MaxDegreeOfParallelism = 10 }, blobItem =>
                {
                    var record = records.Where(r => r.RowKey == blobItem.Name).FirstOrDefault();
                    if (record == null || record.Status == Constants.Failed)
                    {
                        StorageUtil.CopyBlobAsync(replicate.Source, replicate.Target, blobItem, accessSignature);

                        InProgress.Add(blobItem.Name);

                        var blobRecord = new BlobReplicateOperation()
                        {
                            PartitionKey = Source.Name,
                            RowKey = blobItem.Name,
                            Operation = Constants.Copy,
                            Status = Constants.InProgress,
                            ETag = "*"
                        };
                        Storage.ContainersTable.Execute(TableOperation.InsertOrReplace(blobRecord));
                    }
                    else if(record.Status == Constants.InProgress)
                    {
                        lock (this)
                        {
                            InProgress.Add(blobItem.Name);
                        }
                    }
                    else if (record.Status == Constants.Finished)
                    {
                        lock (this)
                        {
                            Finished.Add(blobItem.Name);
                        }
                    }                    
                    else
                    {
                        throw new NotSupportedException();
                    }
                    sb.AppendFormat("{0}{1}{2}", blobItem.Name, blobItem.Properties.Length, blobItem.Properties.LastModified);

                    Interlocked.Increment(ref Total);
                });
                token = result.ContinuationToken;
            } while (token != null);

            if (InProgress.Count == 0)
            {
                // Nothing in progress
                logger.Info("{0} already copied with {1}/{2} blobs", Target.Name, Finished.Count, Total);
                var containerRecord = new ContainerReplicateOperation()
                {
                    PartitionKey = "container",
                    RowKey = Source.Name,
                    Status = Constants.Finished,
                    Hash = Hash.MD5(sb.ToString()),
                    ETag = "*",
                };
                Storage.ContainersTable.Execute(TableOperation.InsertOrReplace(containerRecord));
                RaiseMadeProgress();
            }
            else
            {
                logger.Info("{0} - copying {1} blobs",  Target.Name, InProgress.Count);
                var containerRecord = new ContainerReplicateOperation()
                {
                    PartitionKey = "container",
                    RowKey = Source.Name,
                    Status = Constants.InProgress,
                    Hash = Hash.MD5(sb.ToString()),
                    ETag = "*",
                };
                Storage.ContainersTable.Execute(TableOperation.InsertOrReplace(containerRecord));
                RaiseMadeProgress();
            }                                   
        }

        public static bool AreEqual(CloudBlobContainer c1, CloudBlobContainer c2)
        {
            var h1 = CalculateHash(c1);
            var h2 = CalculateHash(c2);

            return h1 == h2;
        }

    }
}
