using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace WindowsAzure.Storage.Replicate
{
    public class RegistrySettings : IDisposable
    {
        private char delimiter = '|';
        private RegistryKey RootKey;
        private String KeyPath = "";

        public string Path
        {
            get
            {
                return KeyPath;
            }
        }

        public RegistrySettings(String RegistryPath)
        {
            KeyPath = RegistryPath;
            RootKey = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            if (RootKey == null)
            { // Key does not exist must make it
                Registry.CurrentUser.CreateSubKey(KeyPath);
                RootKey = Registry.CurrentUser.OpenSubKey(KeyPath, true);
            }
        }

        public void Dispose()
        {
            RootKey.Close();
            RootKey.Dispose();
        }

        public bool ReadListFromRegistry(String KeyName, out string[] s)
        {
            lock (this)
            {
                object o;
                o = RootKey.GetValue(KeyName);
                if (o == null)
                {
                    s = null;
                    return false;
                }
                s = o.ToString().Split(delimiter);
                return true;
            }
        }

        public bool WriteListToRegistry(String KeyName, string[] s)
        {
            lock (this)
            {
                String val = "";

                if (s.Length < 0) return false;

                for (int i = 0; i < s.Length; i++)
                {
                    val += s[i] + delimiter;
                }
                val = val.Substring(0, val.Length - 1); // Remove trailing delimiter
                RootKey.SetValue(KeyName, val);
                return true;
            }
        }

        public void Clear()
        {
            lock (this)
            {
                foreach (var value in RootKey.GetValueNames())
                {
                    RootKey.DeleteValue(value);
                }
                foreach (var subKey in RootKey.GetSubKeyNames())
                {
                    RootKey.DeleteSubKeyTree(subKey, false);
                }
            }
        }

        public string Read(string KeyName, string defaultValue = null)
        {
            lock (this)
            {
                object o;
                o = RootKey.GetValue(KeyName);
                if (o == null)
                {
                    return defaultValue;
                }
                else
                {
                    if (o.GetType() == typeof(string))
                    {
                        return (string)o;
                    }
                    else
                    {
                        throw new InvalidProgramException("Registry key does not contain string");
                    }
                }
            }
        }


        public int Read(string KeyName, int defaultValue)
        {
            lock (this)
            {
                object o;
                o = RootKey.GetValue(KeyName);
                if (o == null)
                {
                    return defaultValue;
                }
                else
                {
                    if (o.GetType() == typeof(int))
                    {
                        return (int)o;
                    }
                    else
                    {
                        throw new InvalidProgramException("Registry key does not contain int");
                    }
                }
            }
        }

        public void Write(string KeyName, string value)
        {
            lock (this)
            {
                if (value == null || KeyName == null || RootKey == null)
                {
                    throw new ArgumentNullException("One or more arguements is null");
                }
                RootKey.SetValue(KeyName, value);
            }
        }

        public void Write(string KeyName, int value)
        {
            lock (this)
            {
                if (KeyName == null || RootKey == null)
                {
                    throw new ArgumentNullException("One or more arguements is null");
                }
                RootKey.SetValue(KeyName, value);
            }
        }
    }
}
