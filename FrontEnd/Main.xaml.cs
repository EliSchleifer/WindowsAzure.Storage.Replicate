using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
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
        private string backupName;
        private Replicate replicate;

        public ObservableCollection<string> Logs { get; private set; }

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

        public string BackupName
        {
            get { return backupName; }
            set
            {
                backupName = value;
                registry.Write("backup_name", value);
            }
        }
         
        private RegistrySettings registry;

        

        public Main()
        {
            Runtime.Initialize();

            this.registry = new RegistrySettings(@"Software\WindowAzureReplicator");
            
            this.backupName = registry.Read("backup_name");
            this.source = registry.Read("source");
            this.target = registry.Read("target");
            this.Logs = new ObservableCollection<string>();

            InitializeComponent();
            this.DataContext = this;            


        }

        private void replicateClick(object sender, RoutedEventArgs e)
        {
            this.replicate = new Replicate(this.Source, this.Target, this.BackupName);

            DispatcherTimer dt = new DispatcherTimer()
            {
               Interval = TimeSpan.FromSeconds(10) 
            };

            dt.Tick += (ds, de) =>
            {
                this.replicate.OnTimer();

                var containersInProgress = this.replicate.Blobs.InProgress.Count;
                var containersFinished = this.replicate.Blobs.Finished.Count;
                var containersTotal = this.replicate.Blobs.Total;

                var containers = string.Format("Containers In:{0} Finished:{1} Total:{2}", containersInProgress, containersFinished, containersTotal);
                this.Status.Content = containers;
            };

            dt.Start();

            var lt = new DispatcherTimer()
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            lt.Tick += (ls, le) =>
            {
                IList<string> logs = Runtime.MemoryTarget.Logs;
                foreach (var l in logs)
                {
                    this.Logs.Add(l);
                }
                Runtime.MemoryTarget.Logs.Clear();
            };
            lt.Start();

            Task.Factory.StartNew(() =>
            {
                this.replicate.BeginReplicate();
            });
        }
    }
}
