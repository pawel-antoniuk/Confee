using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Speech.AudioFormat;

namespace ConfeeDemoWPF
{
    class SpeechRecognizer
    {
        private SpeechRecognitionEngine _speechEngine;

        public event EventHandler<string> SpeechRecognized;

        private static RecognizerInfo GetKinectRecognizer()
        {
            foreach (var recognizer in SpeechRecognitionEngine.InstalledRecognizers())
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }
            return null;
        }

        public SpeechRecognizer(Stream audioStream)
        {
            var recognizerInfo = GetKinectRecognizer();
            _speechEngine = new SpeechRecognitionEngine(recognizerInfo);
            _speechEngine.SpeechRecognized += Engine_SpeechRecognized;
            //_speechEngine.SpeechRecognitionRejected += Engine_SpeechRecognitionRejected;
            //_speechEngine.SpeechDetected += SpeechEngine_SpeechDetected;

            using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
            {
                _speechEngine.LoadGrammar(new Grammar(memoryStream));
            }

            var convertStream = new KinectStreamConverter(audioStream);
            convertStream.SpeechActive = true;
            _speechEngine.SetInputToAudioStream(
                convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            _speechEngine.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void Engine_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            SpeechRecognized?.Invoke(sender, e.Result.Text);
        }
    }
}
