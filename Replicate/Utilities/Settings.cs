using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace WindowsAzure.Storage.Replicate
{
    public abstract class Settings
    {
        public string ConnectionString
        {
            get { return this.Get("DataConnectionString"); }
        }

        public string this[string value]
        {
            get { return Get(value); }
        }

        public abstract string Get(string name);
        public abstract string Get(string name, string defaultValue);
    }

    public class ConfigurationSettings : Settings
    {
        public ConfigurationSettings() { }

        public override string Get(string name)
        {
            //if (Runtime.IsAzureEnvironment)
            //{
            //    return RoleEnvironment.GetConfigurationSettingValue(name);
            //}
            //else
            {
                var settings = ConfigurationManager.AppSettings;
                return settings[name];
            }
        }

        public override string Get(string name, string defaultValue)
        {
            //if (Runtime.IsAzureEnvironment)
            //{
            //    try
            //    {
            //        return RoleEnvironment.GetConfigurationSettingValue(name);
            //    }
            //    catch (RoleEnvironmentException)
            //    {
            //        return defaultValue;
            //    }
            //}
            //else
            {
                var settings = ConfigurationManager.AppSettings;
                var value = settings[name];
                return (value == null) ? defaultValue : value;
            }
        }
    }
}
