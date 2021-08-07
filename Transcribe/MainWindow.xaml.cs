using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Transcribe
{
    /// <summary>
    /// Interaction logic for Transcribe.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Boolean stillWorking;

        DisplayHelper display;

        private AzureService service;
        RegistryManager manager = new RegistryManager();

        private MMDevice selectedDevice;
        private MMDeviceEnumerator enumerator;

        private FontDialog fontDialog = new FontDialog();

        public MainWindow()
        {
            InitializeComponent();
            InitializeFontsAndBackground();
            InitializeDeviceListMenu();
            display = new DisplayHelper();

            string azureKey = manager.getAzureKey();
            string azureLocation = manager.getAzureLocation();
            if (azureKey == null || azureLocation == null)
            {
                KeyManager keyManagerDialog = new KeyManager();
                if (keyManagerDialog.ShowDialog() == true)
                {
                    azureKey = keyManagerDialog.Key;
                    azureLocation = keyManagerDialog.Location;
                    manager.setAzureKeyLocation(azureKey, azureLocation);
                }
            }

            service = new AzureService(azureKey, azureLocation, display);
        }

        private void InitializeDeviceListMenu()
        {
            enumerator = new MMDeviceEnumerator();
            MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var item = new MenuItem();
                item.Header = endpoint.FriendlyName;
                item.Click += DeviceMenu_SelectionChanged;
                item.IsCheckable = true;
                if (endpoint.FriendlyName == defaultDevice.FriendlyName)
                {
                    item.IsChecked = true;
                    selectedDevice = endpoint;
                }
                DeviceMenuList.Items.Add(item);
            }
        }

        private void InitializeFontsAndBackground()
        {
            SolidColorBrush bgBrush = new SolidColorBrush(manager.getColorMedia("bg"));
            TranscriptionDisplay.Background = bgBrush;
            RTAudioTranscriptionDisplay.Background = bgBrush;
            RTMicTranscriptionDisplay.Background = bgBrush;

            FontFamily family = new FontFamily("Arial");
            float size = 10;
            FontStyle style = FontStyles.Normal;
            FontWeight weight = FontWeights.Normal;
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            if (manager.getValue("fontFamily") != null)
            {
                family = new FontFamily(manager.getValue("fontFamily"));
                size = float.Parse(manager.getValue("fontSize"));
                style = manager.getValue("fontStyle") == "italic" ? FontStyles.Italic : FontStyles.Normal;
                weight = manager.getValue("fontWeight") == "bold" ? FontWeights.Bold : FontWeights.Normal;
                brush = new SolidColorBrush(manager.getColorMedia("font"));
            }
            else
            {
                manager.setColor("font", brush.Color);
            }

            TranscriptionDisplay.FontFamily = family;
            TranscriptionDisplay.FontSize = size;
            TranscriptionDisplay.FontStyle = style;
            TranscriptionDisplay.FontWeight = weight;
            TranscriptionDisplay.Foreground = brush;

            RTAudioTranscriptionDisplay.FontFamily = family;
            RTAudioTranscriptionDisplay.FontSize = size;
            RTAudioTranscriptionDisplay.FontStyle = style;
            RTAudioTranscriptionDisplay.FontWeight = weight;
            RTAudioTranscriptionDisplay.Foreground = brush;

            RTMicTranscriptionDisplay.FontFamily = family;
            RTMicTranscriptionDisplay.FontSize = size;
            RTMicTranscriptionDisplay.FontStyle = style;
            RTMicTranscriptionDisplay.FontWeight = weight;
            RTMicTranscriptionDisplay.Foreground = brush;

        }

        private void DeviceMenu_SelectionChanged(object sender, RoutedEventArgs e)
        {
            MenuItem selected = (MenuItem)e.Source;
            ItemCollection collection = DeviceMenuList.Items;
            foreach (MenuItem item in collection)
            {
                if (item.Header == selected.Header)
                {
                    item.IsChecked = true;
                    foreach (var endpoint in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                    {
                        if (endpoint.FriendlyName == (string) item.Header)
                        {
                            selectedDevice = endpoint;
                        }
                    }
                }
                else
                {
                    item.IsChecked = false;
                }
            }
        }

        private async Task TranscribeLoop()
        {
            while (stillWorking)
            {
                RTMicTranscriptionDisplay.Text = display.GetRealtimeMic();
                RTAudioTranscriptionDisplay.Text = display.GetRealtimeAudio();
                TranscriptionDisplay.Text = display.GetTranscription();
                ErrorText.Text = display.GetError();
                await Task.Delay(100);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await service.StopAudioRecognizer();
            await service.StopMicrophoneRecognizer();
            stillWorking = false;
            StartMicButton.IsEnabled = true;
            StartDeskButton.IsEnabled = true;
            MicListening.Text = "";
            AudioListening.Text = "";
        }

        private async void StartMicButton_Click(object sender, RoutedEventArgs e)
        {
            StartMicButton.IsEnabled = false;
            stillWorking = true;
            await service.StartMicrophoneRecognizer();
            await TranscribeLoop();
            MicListening.Text = "Mic Listening";
        }

        private async void StartDeskButton_Click(object sender, RoutedEventArgs e)
        {
            StartDeskButton.IsEnabled = false;
            stillWorking = true;
            await service.StartAudioRecognizer(selectedDevice);
            await TranscribeLoop();
            AudioListening.Text = "Audio Listening";
        }

        private void MenuItemSetKey_Click(object sender, RoutedEventArgs e)
        {
            KeyManager keyManagerDialog = new KeyManager();

            if (keyManagerDialog.ShowDialog() == true)
            {
                string azureKey = keyManagerDialog.Key;
                string azureLocation = keyManagerDialog.Location;
                manager.setAzureKeyLocation(azureKey, azureLocation);
                service = new AzureService(azureKey, azureLocation, display);
            }
        }

        private void MenuItemFont_Click(object sender, RoutedEventArgs e)
        {
            fontDialog.ShowColor = true;
            System.Drawing.Font font = new System.Drawing.Font(
                manager.getValue("fontFamily") != null ? manager.getValue("fontFamily") : "Arial",
                float.Parse(manager.getValue("fontSize") != null ? manager.getValue("fontSize") : "10"),
                manager.getValue("fontStyle") == "italic" ? System.Drawing.FontStyle.Italic :
                    manager.getValue("fontWeight") == "bold" ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular
                );
            fontDialog.Font = font;
            fontDialog.Color = manager.getColorDrawing("font");

            if (fontDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FontFamily family = new FontFamily(fontDialog.Font.FontFamily.Name);
                string style = fontDialog.Font.Style == System.Drawing.FontStyle.Italic ? "italic" : "normal";
                string weight = fontDialog.Font.Style == System.Drawing.FontStyle.Bold ? "bold" : "normal";
                System.Drawing.Color color = fontDialog.Color;
                SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
                manager.setValue("fontFamily", fontDialog.Font.FontFamily.Name);
                manager.setValue("fontStyle", style);
                manager.setValue("fontWeight", weight);
                manager.setValue("fontSize", fontDialog.Font.Size.ToString());
                manager.setColor("font", color);

                InitializeFontsAndBackground();
            }
        }

        private void MenuItemColor_Click(object sender, RoutedEventArgs e)
        {
            ColorDialog dialog = new ColorDialog();
            dialog.Color = manager.getColorDrawing("bg");
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                System.Drawing.Color color = dialog.Color;
                SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
                manager.setColor("bg", brush.Color);
                InitializeFontsAndBackground();
            }
        }
    }
}
