using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;

namespace WindowsAzure.Storage.Replicate
{
    public class Repository
    {
        private CloudStorageAccount account;
        public CloudBlobClient BlobClient { get; private set; }
        public CloudTableClient TableClient { get; private set; }

        public Repository(CloudStorageAccount account)
        {
            this.account = account;

            this.BlobClient = account.CreateCloudBlobClient();

            // Set timeout for all blob operations
            this.BlobClient.MaximumExecutionTime = TimeSpan.FromMinutes(10);
            this.BlobClient.ServerTimeout = TimeSpan.FromMinutes(3);
            
            //TODO 
            //this.BlobClient.RetryPolicy = RetryPolicies.RetryExponential(5, RetryPolicies.DefaultClientBackoff);

            this.TableClient = account.CreateCloudTableClient();
            this.TableClient.MaximumExecutionTime = TimeSpan.FromMinutes(10);
            this.TableClient.ServerTimeout = TimeSpan.FromMinutes(3);
        }       
    }
}
