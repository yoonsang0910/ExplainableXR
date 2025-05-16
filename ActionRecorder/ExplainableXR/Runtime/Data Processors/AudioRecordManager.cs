using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using Whisper.Utils;

namespace ExplainableXR
{
    using static ExplainableXR.Utility;
    public class AudioRecordManager
    {
        private int recordingIndex = 1;
        private string dirName = null;
        public string DataSaveDirPath { get; private set; } = null;
        private MicrophoneRecord microphoneRecordManager = null;
        public AudioRecordManager(MicrophoneRecord microphoneManager, string rootDirName)
        {
            if (microphoneManager != null)
            {
                microphoneRecordManager = microphoneManager;
                microphoneRecordManager.gameObject.SetActive(true);
                microphoneRecordManager.useVad = false;
                microphoneRecordManager.vadStop = false;
                microphoneRecordManager.dropVadPart = false;
                microphoneRecordManager.enabled = true;
                microphoneRecordManager.OnRecordStop += OnRecordEnd;
            }

            dirName = Path.Combine(rootDirName, "Audio");
            Directory.CreateDirectory(dirName);
        }
        public void RecordStart()
        {
            MsgPrint($"Voice recording start");
            microphoneRecordManager.StartRecord();
        }
        public string RecordStop()
        {
            MsgPrint($"Voice recording stop");
            if (!microphoneRecordManager.IsRecording)
                return null;

            var fileName = $"{recordingIndex++}.wav";
            var dataSaveFilePath = Path.Combine(dirName, fileName);

            //RequestedAudioFileSavePath: Full file path of the audio WAV bytes
            microphoneRecordManager.StopRecord(requestedAudioFileSavePath: dataSaveFilePath);
            return fileName;
        }
        private async void OnRecordEnd(AudioChunk recordedAudio, string requestedAudioFileSavePath)
        {
            MsgPrint($"Voice recording end");
            _ = await AudioClipWavConverter.ConvertAudioChunkToWavAsync(recordedAudio, fullSavePath: requestedAudioFileSavePath);
            MsgPrint($"Audio file saved at :{requestedAudioFileSavePath}");
        }
        public string SaveWavBytesToPath(byte[] wavBytes)
        {
            var fileName = $"{recordingIndex++}.wav";
            var dataSaveFilePath = Path.Combine(dirName, fileName);
            File.WriteAllBytes(dataSaveFilePath, wavBytes);
            return fileName;
        }
    }
}