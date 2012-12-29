using System;
using Microsoft.Win32;
using System.Configuration;
using Microsoft.WindowsAzure;
using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using NLog.Config;
using System.Data.Services.Client;
using Microsoft.WindowsAzure.Storage;
using System.Security.Cryptography;

namespace WindowsAzure.Storage.Replicate
{
    public enum RuntimeMode
    {
        Invalid,
        Production,
        Test
    }

    public static class Runtime
    {
        private static Logger logger = LogManager.GetLogger("Runtime");
        private static bool initialized = false;

        static Runtime()
        {
            TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>(TaskScheduler_UnobservedTaskException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(OnUnhandledException);

            // Set the maximum number of concurrent connections 
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.DefaultConnectionLimit = 100;
        }

        private static void InitializeNLog(RuntimeMode mode = RuntimeMode.Test)
        {
            var fileTarget = new FileTarget();
            fileTarget.ArchiveEvery = FileArchivePeriod.Day;
            fileTarget.CreateDirs = true;
            fileTarget.KeepFileOpen = true;
            fileTarget.MaxArchiveFiles = 10;
            fileTarget.Layout = fileTarget.Layout = NLog.Layouts.Layout.FromString(@"${date:format=MM-dd HH\:mm\:ss} ${logger} | ${message} ${onexception:EXCEPTION\:${exception:format=tostring}}");

            //var layout = NLog.Layouts.Layout.FromString(@"${message}${onexception:EXCEPTION OCCURRED:${message} (${callsite:includeSourcePath=true}) ($stacktrace:topFrames=10}) ${exception:format=ToString}");

            //TODO does not run in Azure right now
            //if (Runtime.IsAzureEnvironment)
            //{
            //    var logs = RoleEnvironment.GetLocalResource("Logs");
            //    fileTarget.ArchiveFileName = logs.RootPath + "${shortdate}.Archive.NLog.txt";
            //    fileTarget.FileName = logs.RootPath + "${shortdate}.NLog.txt";
            //}
            //else
            {
                fileTarget.FileName = "${basedir}/${shortdate}.NLog.txt";
                fileTarget.ArchiveFileName = "${shortdate}.Archive.NLog.txt";
            }

            var split = new SplitGroupTarget();

            split.Targets.Add(fileTarget);

            if (mode == RuntimeMode.Test)
            {
                split.Targets.Add(new ConsoleTarget());
            }

            var asyncTarget = new AsyncTargetWrapper();
            asyncTarget.WrappedTarget = split;
            asyncTarget.QueueLimit = 5000;
            asyncTarget.OverflowAction = AsyncTargetWrapperOverflowAction.Block;

            var config = new LoggingConfiguration();
            var rule = new LoggingRule("*", Runtime.LogLevel, asyncTarget);
            config.LoggingRules.Add(rule);
            LogManager.Configuration = config;

            logger.Info("InitalizeNLog");
        }

        /// <summary>
        /// Retry the provided function retryCount times. If failure is caused by stale cache data and we have the cache key we
        /// can clean the cache out 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="cacheId"></param>
        /// <param name="retryCount"></param>
        /// <param name="tcs"></param>
        /// <returns></returns>
        public static Task<T> Retry<T>(Func<Task<T>> func, string cacheId, int retryCount = 3, TaskCompletionSource<T> tcs = null)
        {
            if (tcs == null)
            {
                tcs = new TaskCompletionSource<T>();
            }

            func().ContinueWith(f =>
            {
                if (f.IsFaulted)
                {
                    if (retryCount == 0)
                    {
                        tcs.SetException(f.Exception.InnerExceptions);
                    }
                    else
                    {
                        Retry(func, cacheId, retryCount - 1, tcs);
                    }
                }
                else
                {
                    tcs.SetResult(f.Result);
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            logger.ErrorException("Unhandled Task caught...this is not good", e.Exception);
            e.SetObserved();
        }    

        public static Settings Settings { get; private set; }

        public static NLog.LogLevel LogLevel
        {
            get
            {
                string value = Settings.Get("LogLevel", "debug").ToLowerInvariant();

                switch (value)
                {
                    case "debug":
                        return NLog.LogLevel.Debug;

                    case "info":
                        return NLog.LogLevel.Info;

                    case "trace":
                        return NLog.LogLevel.Trace;

                    case "error":
                        return NLog.LogLevel.Error;

                    case "fatal":
                        return NLog.LogLevel.Fatal;

                    default:
                        return NLog.LogLevel.Debug;
                }
            }
        }     

        public static void Initialize()
        {
            InitializeNLog();

            logger.Info("Initalized");
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // On an unhandled exception the process is going to be killed. We will want to trace this out
            // the WebRole will then stop and when it starts back up the transfer will occur within the standard window
            logger.FatalException("Unhandled Exception", e.ExceptionObject as Exception);
        }
    }

   
}
