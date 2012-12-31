using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WindowsAzure.Storage.Replicate
{
    internal static class StorageUtil
    {
        //public static void CopyBlob(CloudBlob sourceBlob, CloudBlob destBlob)
        //{
        //    // If the accounts are the same then use CopyFromBlob for effiency
        //    if (sourceBlob.Uri.Host == destBlob.Uri.Host)
        //    {
        //        destBlob.CopyFromBlob(sourceBlob);
        //    }
        //    else
        //    {
        //        // CopyFromBlob does not work across storage accounts, so we must open and re-upload the blob stream
        //        using (BlobStream stream = sourceBlob.OpenRead())
        //        {
        //            destBlob.OpenWrite();
        //            destBlob.UploadFromStream(stream);
        //        }
        //    }
        //}

        public static void CopyBlobAsync(Repository source, Repository target, ICloudBlob item, string accessSignature)
        {            
            var tc = target.BlobClient.GetContainerReference(item.Container.Name);            
            
            ICloudBlob tb = null;
            if(item.BlobType == BlobType.BlockBlob)
            {
                tb = tc.GetBlockBlobReference(item.Name);
            }
            else
            {
                tb = tc.GetPageBlobReference(item.Name);
            }
            if (!string.IsNullOrEmpty(accessSignature))
            {
                tb.StartCopyFromBlob(new Uri(item.Uri.AbsoluteUri + accessSignature));
            }
            else
            {
                tb.StartCopyFromBlob(item.Uri);
            }

                // Write to table storage this backup operation

            //return Task.Factory.StartNew(() =>
            //{
            //    while (true)
            //    {
            //        Thread.Sleep(TimeSpan.FromMilliseconds(100));

            //        var blob = target.GetBlobReferenceFromServer(destName);
            //        if (blob.CopyState.Status == CopyStatus.Success)
            //        {
            //            return true;
            //        }
            //        else if (blob.CopyState.Status == CopyStatus.Pending)
            //        {
            //            continue;
            //        }
            //        else
            //        {
            //            return false;
            //        }
            //    }
            //});
        }

        public static IEnumerable<ThreadRunner.ThreadRunnerThread> CreateRunList(IEnumerable<string> tables,
            IEnumerable<string> containers,
            IEnumerable<string> priorityTables,
            IEnumerable<string> priorityContainers,
            ThreadRunner.TaskDelegate tableDelegate,
            ThreadRunner.TaskDelegate containerDelegate)
        {
            // Build consolidated list
            return (from t in tables
                    join h in priorityTables on t equals h into outer
                    from h in outer.DefaultIfEmpty()
                    select new ThreadRunner.ThreadRunnerThread(t, tableDelegate, h != null ? 1 : 2)).Union(
                    from c in containers
                    join j in priorityContainers on c equals j into outer
                    from j in outer.DefaultIfEmpty()
                    select new ThreadRunner.ThreadRunnerThread(c, containerDelegate, j != null ? 1 : 3)).OrderBy(p => p.Priority).ToList();
        }
    }
}