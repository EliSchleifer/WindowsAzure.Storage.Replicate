using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsAzure.Storage.Replicate.FrontEnd
{
    public class BlobContainerViewModel : INotifyPropertyChanged
    {
        public BlobContainer Container { get; set; }
        public BlobContainerViewModel(BlobContainer container)
        {
            this.Container = container;
            this.Container.MadeProgress += OnMadeProgress;
        }

        void OnMadeProgress(object sender, BlobContainer container)
        {
            RaisePropertyChanged("Status");
            RaisePropertyChanged("PercentageComplete");
            RaisePropertyChanged("Errors");
        }

        public string Name { get { return Container.Source.Name; } }

        public int Errors
        {
            get { return Container.Failed.Count; }
        }

        public float PercentageComplete
        {
            get
            {
                if (Container.IsReplicated)
                {
                    return 100;
                }
                else if (Container.Total == 0)
                {
                    return 0;
                }
                else
                {
                    return (float)Container.Finished.Count / (float)Container.Total;
                }
            }
        }

        public string Status
        {
            get
            {
                if (Container.IsReplicated) 
                {
                    return string.Format("Finished {0}/{1}", Container.Finished.Count, Container.Total);
                }
                return string.Format("Copied {0}/{1}", Container.Finished.Count, Container.Total);
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
