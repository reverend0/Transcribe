using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transcribe
{
    class RegistryManager
    {
        public RegistryManager()
        {
        }

        public void setAzureKeyLocation(string azurekey, string azurelocation)
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Transcriber");
            key.SetValue("AzureKey", azurekey);
            key.SetValue("AzureLocation", azurelocation);
            key.Close();
        }

        public string getAzureKey()
        {
            return getValue("AzureKey");
        }

        public string getAzureLocation()
        {
            return getValue("AzureLocation");
        }

        public string getValue(string key)
        {
            RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Transcriber");
            if (reg != null)
            {
                string result = (string) reg.GetValue(key);
                reg.Close();
                return result;
            }
            return null;
        }
    }
}
