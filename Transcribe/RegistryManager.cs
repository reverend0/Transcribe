using Microsoft.Win32;

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

        public void setColor(string prefix, System.Drawing.Color color)
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Transcriber");
            key.SetValue(prefix + "A", color.A);
            key.SetValue(prefix + "R", color.R);
            key.SetValue(prefix + "G", color.G);
            key.SetValue(prefix + "B", color.B);
            key.Close();
        }

        public void setColor(string prefix, System.Windows.Media.Color color)
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Transcriber");
            key.SetValue(prefix + "A", color.A);
            key.SetValue(prefix + "R", color.R);
            key.SetValue(prefix + "G", color.G);
            key.SetValue(prefix + "B", color.B);
            key.Close();
        }

        public System.Drawing.Color getColorDrawing(string prefix)
        {
            RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Transcriber");
            if (reg != null && reg.GetValue(prefix + "A") != null)
            {
                byte a = 0, r = 0, g = 0, b = 0;
                if (reg.GetValue(prefix + "A").GetType().Equals(typeof(byte)))
                {
                    a = (byte)reg.GetValue(prefix + "A");
                    r = (byte)reg.GetValue(prefix + "R");
                    g = (byte)reg.GetValue(prefix + "G");
                    b = (byte)reg.GetValue(prefix + "B");
                }
                else if (reg.GetValue(prefix + "A").GetType().Equals(typeof(string)))
                {
                    a = byte.Parse((string)reg.GetValue(prefix + "A"));
                    r = byte.Parse((string)reg.GetValue(prefix + "R"));
                    g = byte.Parse((string)reg.GetValue(prefix + "G"));
                    b = byte.Parse((string)reg.GetValue(prefix + "B"));
                }
                System.Drawing.Color result = System.Drawing.Color.FromArgb(a, r, g, b);
                reg.Close();
                return result;
            }
            return System.Drawing.Color.FromArgb(0, 0, 255, 0);
        }

        public System.Windows.Media.Color getColorMedia(string prefix)
        {
            RegistryKey reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Transcriber");
            if (reg != null && reg.GetValue(prefix + "A") != null)
            {
                byte a = 0, r = 0, g = 0, b = 0;
                if (reg.GetValue(prefix + "A").GetType().Equals(typeof(byte)))
                {
                    a = (byte)reg.GetValue(prefix + "A");
                    r = (byte)reg.GetValue(prefix + "R");
                    g = (byte)reg.GetValue(prefix + "G");
                    b = (byte)reg.GetValue(prefix + "B");
                }
                else if (reg.GetValue(prefix + "A").GetType().Equals(typeof(string)))
                {
                    a = byte.Parse((string)reg.GetValue(prefix + "A"));
                    r = byte.Parse((string)reg.GetValue(prefix + "R"));
                    g = byte.Parse((string)reg.GetValue(prefix + "G"));
                    b = byte.Parse((string)reg.GetValue(prefix + "B"));
                }
                System.Windows.Media.Color result = System.Windows.Media.Color.FromArgb(a, r, g, b);
                reg.Close();
                return result;
            }
            return System.Windows.Media.Color.FromArgb(0, 0, 255, 0);
        }

        public void setValue(string key, string value)
        {
            RegistryKey reg = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Transcriber");
            reg.SetValue(key, value);
            reg.Close();
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
