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
        }

    }
}