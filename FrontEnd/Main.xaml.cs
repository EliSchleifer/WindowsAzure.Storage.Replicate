using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsAzure.Storage.Replicate;

namespace WindowsAzure.Storage.Replicate.FrontEnd
{
    /// <summary>
    /// Interaction logic for Main.xaml
    /// </summary>
    public partial class Main : Window
    {
        private string source;
        private string target;
        private Replicate replicate;

        public string Target 
        {
            get { return target; }
            set
            {
                target = value;
                registry.Write("target", value);
            }
        }

        public string Source
        {
            get { return source; }
            set
            {
                source = value;
                registry.Write("source", value);
            }
        }
         
        private RegistrySettings registry;

        

        public Main()
        {
            this.registry = new RegistrySettings(@"Software\WindowAzureReplicator");

            this.source = registry.Read("source");
            this.target = registry.Read("target");

            InitializeComponent();
            this.DataContext = this;            
        }

        private void replicateClick(object sender, RoutedEventArgs e)
        {
            this.replicate = new Replicate(this.Source, this.Target);
            this.replicate.BeginReplicate();

            DispatcherTimer dt = new DispatcherTimer()
            {
               Interval = TimeSpan.FromSeconds(10) 
            };

            dt.Tick += (ds, de) =>
            {
                var containersInProgress = this.replicate.Blobs.InProgress.Count;
                var containersFinished = this.replicate.Blobs.Finished.Count;
                var containersTotal = this.replicate.Blobs.Total;

                var containers = string.Format("Containers In:{0} Finished:{1} Total:{2}", containersInProgress, containersFinished, containersTotal);
                this.Status.Content = containers;
            };

            dt.Start();
                

        }
    }
}
