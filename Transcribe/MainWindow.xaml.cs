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
        
        private SpeechConfig speechConfig;
        RegistryManager manager = new RegistryManager();

        // Temporary Storage for Transcription that is fed to the display elements
        private Transcriber transcriber;
        private Transcriber rtAudioTranscriber;
        private Transcriber rtMicTranscriber;
        private Transcriber error;

        private MMDevice selectedDevice;
        private MMDeviceEnumerator enumerator;

        private FontDialog fontDialog = new FontDialog();

        public MainWindow()
        {
            InitializeComponent();
            transcriber = new Transcriber();
            rtAudioTranscriber = new Transcriber();
            rtMicTranscriber = new Transcriber();
            error = new Transcriber();

            string azureKey = manager.getAzureKey();
            string azureLocation = manager.getAzureLocation();
            if (azureKey == null || azureLocation == null)
            {
                KeyManager keyManagerDialog = new KeyManager();
                if (keyManagerDialog.ShowDialog() == true)
                {
                    azureKey = keyManagerDialog.Key;
                    azureLocation= keyManagerDialog.Location;
                    manager.setAzureKeyLocation(azureKey, azureLocation);
                }
            }

            speechConfig = SpeechConfig.FromSubscription(azureKey, azureLocation);

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

        public void PrependText(string value)
        {
            transcriber.Prepend(value);
        }

        public void DisplayError(string value)
        {
            error.Set(value);
        }

        private async Task FromMic()
        {
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var stopRecognition = new TaskCompletionSource<int>();

            recognizer.Recognizing += (s, e) =>
            {
                rtMicTranscriber.Set(e.Result.Text);
            };
            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    PrependText(e.Result.Text);
                    rtMicTranscriber.Set("");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    DisplayError("ERROR: No Match. Speech not recognized.");
                }
            };
            recognizer.Canceled += (s, e) =>
            {
                DisplayError($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    DisplayError($"CANCELED: ErrorCode={e.ErrorCode}; ErrorDetails={e.ErrorDetails}; Reason={e.Reason}");
                }

                stopRecognition.TrySetResult(0);
            };
            recognizer.SessionStopped += (s, e) =>
            {
                stopRecognition.TrySetResult(0);
            };

            MicListening.Text = "Mic Listening";
            await recognizer.StartContinuousRecognitionAsync();
            while (stillWorking)
            {
                RTMicTranscriptionDisplay.Text = rtMicTranscriber.Get();
                RTAudioTranscriptionDisplay.Text = rtAudioTranscriber.Get();
                TranscriptionDisplay.Text = transcriber.Get();
                ErrorText.Text = error.Get();
                await Task.Delay(100);
            }
            await recognizer.StopContinuousRecognitionAsync();
            MicListening.Text = "";
        }

        private async Task FromOutput()
        {
            using var capture = new WasapiLoopbackCapture(selectedDevice);
            using var audioInputStream = AudioInputStream.CreatePushStream();
            capture.DataAvailable += (s, a) =>
            {
                audioInputStream.Write(ToPCM16(a.Buffer, a.BytesRecorded, capture.WaveFormat));
            };

            using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var stopRecognition = new TaskCompletionSource<int>();

            recognizer.Recognizing += (s, e) =>
            {
                rtAudioTranscriber.Set(e.Result.Text);
            };
            recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    PrependText(e.Result.Text);
                    rtAudioTranscriber.Set("");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    DisplayError("ERROR: No Match. Speech not recognized.");
                }
            };
            recognizer.Canceled += (s, e) =>
            {
                DisplayError($"CANCELED: Reason={e.Reason}");

                if (e.Reason == CancellationReason.Error)
                {
                    DisplayError($"CANCELED: ErrorCode={e.ErrorCode}; ErrorDetails={e.ErrorDetails}; Reason={e.Reason}");
                }

                stopRecognition.TrySetResult(0);
            };
            recognizer.SessionStopped += (s, e) =>
            {
                stopRecognition.TrySetResult(0);
            };

            AudioListening.Text = "Audio Listening";
            await recognizer.StartContinuousRecognitionAsync();
            capture.StartRecording();
            while (stillWorking)
            {
                RTMicTranscriptionDisplay.Text = rtMicTranscriber.Get();
                RTAudioTranscriptionDisplay.Text = rtAudioTranscriber.Get();
                TranscriptionDisplay.Text = transcriber.Get();
                ErrorText.Text = error.Get();
                await Task.Delay(100);
            }
            await recognizer.StopContinuousRecognitionAsync();
            capture.StopRecording();
            capture.Dispose();
            AudioListening.Text = "";
        }

        public byte[] ToPCM16(byte[] inputBuffer, int length, WaveFormat format)
        {
            if (length == 0)
                return new byte[0]; // No bytes recorded, return empty array.

            // Create a WaveStream from the input buffer.
            using var memStream = new MemoryStream(inputBuffer, 0, length);
            using var inputStream = new RawSourceWaveStream(memStream, format);

            // Convert the input stream to a WaveProvider in 16bit PCM format with sample rate of 16000 Hz.
            //var convertedPCM = new WaveFormatConversionStream(new WaveFormat(16000, 16, 1), inputStream);
            var convertedPCM = new StereoToMonoProvider16(
                new SampleToWaveProvider16(
                    new WdlResamplingSampleProvider(
                        new WaveToSampleProvider(inputStream),
                        16000)
                    )
                );

            byte[] convertedBuffer = new byte[length];

            using var stream = new MemoryStream();
            int read;

            // Read the converted WaveProvider into a buffer and turn it into a Stream.
            while ((read = convertedPCM.Read(convertedBuffer, 0, length)) > 0)
                stream.Write(convertedBuffer, 0, read);

            // Return the converted Stream as a byte array.
            return stream.ToArray();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            stillWorking = false;
            StartMicButton.IsEnabled = true;
            StartDeskButton.IsEnabled = true;
        }

        private async void StartMicButton_Click(object sender, RoutedEventArgs e)
        {
            StartMicButton.IsEnabled = false;
            stillWorking = true;
            await FromMic();
        }

        private async void StartDeskButton_Click(object sender, RoutedEventArgs e)
        {
            StartDeskButton.IsEnabled = false;
            stillWorking = true;
            await FromOutput();
        }

        private void MenuItemSetKey_Click(object sender, RoutedEventArgs e)
        {
            KeyManager keyManagerDialog = new KeyManager();

            if (keyManagerDialog.ShowDialog() == true)
            {
                string azureKey = keyManagerDialog.Key;
                string azureLocation = keyManagerDialog.Location;
                manager.setAzureKeyLocation(azureKey, azureLocation);
                speechConfig = SpeechConfig.FromSubscription(azureKey, azureLocation);
            }
        }

        private void MenuItemFont_Click(object sender, RoutedEventArgs e)
        {
            fontDialog.ShowColor = true;
            if (fontDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FontFamily family = new FontFamily(fontDialog.Font.FontFamily.Name);
                FontStyle style = fontDialog.Font.Style == System.Drawing.FontStyle.Italic ? FontStyles.Italic : FontStyles.Normal;
                FontWeight weight = fontDialog.Font.Style == System.Drawing.FontStyle.Bold ? FontWeights.Bold : FontWeights.Normal;
                System.Drawing.Color color = fontDialog.Color;
                SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));

                TranscriptionDisplay.FontFamily = family;
                TranscriptionDisplay.FontSize = fontDialog.Font.Size;
                TranscriptionDisplay.FontStyle = style;
                TranscriptionDisplay.FontWeight = weight;
                TranscriptionDisplay.Foreground = brush;

                RTAudioTranscriptionDisplay.FontFamily = family;
                RTAudioTranscriptionDisplay.FontSize = fontDialog.Font.Size;
                RTAudioTranscriptionDisplay.FontStyle = style;
                RTAudioTranscriptionDisplay.FontWeight = weight;
                RTAudioTranscriptionDisplay.Foreground = brush;

                RTMicTranscriptionDisplay.FontFamily = family;
                RTMicTranscriptionDisplay.FontSize = fontDialog.Font.Size;
                RTMicTranscriptionDisplay.FontStyle = style;
                RTMicTranscriptionDisplay.FontWeight = weight;
                RTMicTranscriptionDisplay.Foreground = brush;
            }
        }
    }
}
