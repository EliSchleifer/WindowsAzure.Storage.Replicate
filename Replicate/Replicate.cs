using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using NLog;

namespace WindowsAzure.Storage.Replicate
{


    public class Replicate : IDisposable
    {
        public Repository Source;
        public Repository Target;
        public string BackupName;
        public BlobStorage Blobs { get; private set; }

        private Timer replicateTimer;
        private readonly string backupPrefix = "backup";
        
        private static Logger logger = LogManager.GetLogger("Replicate");

        public Replicate(string source, string target, string backupName)
            : this(CloudStorageAccount.Parse(source), CloudStorageAccount.Parse(target), backupName)
        {
        }
        
        public Replicate(CloudStorageAccount source, CloudStorageAccount target, string backupName)
        {
            this.Source = new Repository(source);
            this.Target = new Repository(target);
            this.Blobs = new BlobStorage(this);

            if (backupName == null)
            {
                this.BackupName = string.Format("{0}{1}{2}", this.backupPrefix, source.Credentials.AccountName, DateTime.UtcNow.Ticks);
            }
            else
            {
                this.BackupName = backupName;
            }
        }

        public void OnTimer()
        {
            this.Blobs.OnTimer();
        }

        public void BeginReplicate(int maxContainers = int.MaxValue)
        {
            logger.Trace("{0:yyyy MM dd hh:mm:ss} Replicate started.", DateTime.UtcNow);

            
            Blobs.BeginReplicate(maxContainers);

            replicateTimer = new Timer((state) =>
            {
                // On each timer hit we ping the Cached indices to check if they want to do some work
                Blobs.OnTimer();

                if (Blobs.AreReplicated)
                {
                    replicateTimer.Dispose();
                }
            }, null, 0, (long)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.replicateTimer != null)
                {
                    replicateTimer.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}