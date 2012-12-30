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


    public class Replicate
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
            if (backupName == null)
            {
                this.BackupName = string.Format("{0}-{1}-{2}", this.backupPrefix, source.Credentials.AccountName, DateTime.UtcNow.Ticks);
            }
            else
            {
                this.BackupName = backupName;
            }
        }
        
        public void BeginReplicate()
        {
            logger.Trace("{0:yyyy MM dd hh:mm:ss} Replicate started.", DateTime.UtcNow);

            var table = Target.TableClient.GetTableReference(this.BackupName + "-containers");
            table.CreateIfNotExists();

            this.Blobs = new BlobStorage(this);
            Blobs.BeginReplicate(1);

            replicateTimer = new Timer((state) =>
            {
                // On each timer hit we ping the Cached indices to check if they want to do some work
                Blobs.OnTimer();

            }, null, 0, (long)TimeSpan.FromSeconds(30).TotalMilliseconds);

            //// Build the backup list
            //var tables = GetTables();
            //var blobStorage = GetContainers(blobClient);
            
            
            ////var backupList = StorageUtil.CreateRunList(tables, containers, this.priorityTables, this.priorityContainers, BackupTable, BackupContainer);
            
            //var threadRunner = new ThreadRunner();
            //threadRunner.Run(backupList, this.maxThreads);

            //WriteBackupData(backupToContainer, tables, blobStorage);

            //Trace.TraceInformation("{0:yyyy MM dd hh:mm:ss} Storage backup completed.", DateTime.UtcNow);
        }


        //private void WriteBackupData(CloudBlobContainer backupToContainer, IEnumerable<string> tables, IEnumerable<string> containers)
        //{
        //    var stream = new MemoryStream();
        //    var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true});

        //    Trace.TraceInformation("Writing backup data xml.");

        //    // Add tables to manifest
        //    writer.WriteStartElement("BackupData");
        //    writer.WriteStartElement("Tables");

        //    foreach (var table in tables)
        //    {
        //        writer.WriteElementString("Table", table);
        //    }

        //    writer.WriteEndElement();

        //    // Add containers to manifest
        //    writer.WriteStartElement("Containers");

        //    foreach (var container in containers)
        //    {
        //        writer.WriteElementString("Container", container);
        //    }

        //    writer.WriteEndElement();
        //    writer.WriteEndElement();
        //    writer.Close();

        //    stream.SetLength(stream.Position);
        //    stream.Seek(0, SeekOrigin.Begin);

        //    var manifestBlob = backupToContainer.GetBlobReference("BackupData.xml");
        //    manifestBlob.UploadFromStream(stream);
        //}


        //private void BackupContainer(string containerName)
        //{
        //    var blobClient = StorageUtil.GetBlobClient(this.Source);
        //    var backupToBlobClient = StorageUtil.GetBlobClient(this.Target);

        //    var container = blobClient.GetContainerReference(containerName);
        //    var backupToContainer = backupToBlobClient.GetContainerReference(this.BackupName); 
        //    var blobs = container.ListBlobs();
            
        //    string backupFileName;
        //    CloudBlob sourceBlob;
        //    CloudBlob destBlob;
        //    string blobName;

        //    if (container.Name.StartsWith(this.backupPrefix, StringComparison.CurrentCultureIgnoreCase))
        //    {
        //        // Do not backup any backup containers
        //        Trace.TraceInformation("{0:yyyy MM dd hh:mm:ss} Container {1} excluded as it is a backup container.", DateTime.UtcNow, container.Name);
        //        return;
        //    }

        //    Trace.TraceInformation("{0:yyyy MM dd hh:mm:ss} Backing up container {1} in thread {2}.", DateTime.UtcNow, container.Name, Thread.CurrentThread.ManagedThreadId);

        //    foreach (var blob in blobs)
        //    {
        //        // Get the full blob name including container
        //        blobName = blob.Uri.AbsolutePath.Substring(blob.Uri.AbsolutePath.IndexOf(container.Name));
               
        //        try
        //        {
        //            backupFileName = string.Format("BlobData/{0}", blobName);
        //            sourceBlob = container.GetBlobReference(blob.Uri.ToString());
        //            destBlob = backupToContainer.GetBlobReference(backupFileName);

        //            StorageUtil.CopyBlob(sourceBlob, destBlob);
        //        }
        //        catch (Exception e)
        //        {
        //            // The blob may have been deleted since retrieving the blob list so ignore
        //            Trace.TraceWarning("{0:yyyy MM dd hh:mm:ss} Backup of blob {1} has failed with error {2}", DateTime.UtcNow, blobName, e.Message);
        //        }
        //    }
                
        //    Trace.TraceInformation("{0:yyyy MM dd hh:mm:ss} Finished backing up container {1}", DateTime.UtcNow, container.Name);
        //}
       
        //private IEnumerable<string> GetTables()
        //{
        //    // Get all tables for the specified account
        //    var tableClient = StorageUtil.GetTableClient(this.Source);
        //    var tableNames = tableClient.ListTables().ToList();
            
        //    return tableNames;
        //}
    }
}