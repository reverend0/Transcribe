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
using System.Windows.Shapes;

namespace Transcribe
{
    /// <summary>
    /// Interaction logic for KeyManager.xaml
    /// </summary>
    public partial class KeyManager : Window
    {
        public string Key;
        public string Location;
        private RegistryManager manager = new RegistryManager();

        public KeyManager()
        {
            InitializeComponent();
            string azureKey = manager.getAzureKey();
            string azureLocation = manager.getAzureLocation();
            KeyBox.Text = azureKey;
            LocationBox.Text = azureLocation;
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            Key = KeyBox.Text;
            Location = LocationBox.Text;
            this.DialogResult = true;
        }
    }
}
