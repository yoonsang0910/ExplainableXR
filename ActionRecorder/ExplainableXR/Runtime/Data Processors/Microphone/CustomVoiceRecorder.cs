using System.IO;
using UnityEngine;
using UnityEngine.Assertions;
using Whisper.Utils;

namespace ExplainableXR
{
    public class CustomVoiceRecorder : MonoBehaviour
    {
        [SerializeField] private MicrophoneRecord microphoneRecordManager;
        public delegate void OnVoiceQueryEndDelegate(byte[] voiceWavBytes, float recordedAudioLength);
        public event OnVoiceQueryEndDelegate OnVoiceQueryEnd;
        private void Awake()
        {
            Assert.IsNotNull(microphoneRecordManager, "MicrophoneRecord not linked!");

            this.gameObject.SetActive(true);
            microphoneRecordManager.enabled = true;
            microphoneRecordManager.OnRecordStop += OnRecordEnd;
        }
        public void RecordStart()
        {
            print($"Voice recording start");
            microphoneRecordManager.StartRecord();
        }
        public void RecordStop()
        {
            print($"Voice recording stop");
            microphoneRecordManager.StopRecord();
        }

        //Invoked upon completion of MIC recording (For both (1) VAD stop, and (2) User-invoked stop)
        private async void OnRecordEnd(AudioChunk recordedAudio, string requestedAudioFileName)
        {
            print($"Voice recording end");
            var wavRecording = await AudioClipWavConverter.ConvertAudioChunkToWavAsync(recordedAudio);
            OnVoiceQueryEnd?.Invoke(wavRecording, recordedAudio.Length);
        }
    }
}