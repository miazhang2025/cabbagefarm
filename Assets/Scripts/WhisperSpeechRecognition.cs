using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;
using System.IO;
using System.Text;

public class WhisperSpeechRecognition : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;
    [SerializeField] private ModelImporter modelImporter;

    private AudioClip recordedClip;
    private bool isRecording = false;
    private string microphoneName;

    private int recordingFrequency = 41000;
    private int maxRecordingLength = 30;

    private string whisperApiUrl = "http://localhost:9000/v1/audio/transcriptions";
    private string whisperModelName = "whisper-1";
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startButton.onClick.AddListener(StartRecording);
        stopButton.onClick.AddListener(StopRecording);
        stopButton.interactable = false;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
   
   private void StartRecording(){
    if (isRecording) return;
    
    
    isRecording = true;
    recordedClip = Microphone.Start(microphoneName, false, maxRecordingLength, recordingFrequency);
    displayText.text = "Listening...";
    startButton.interactable = false;
    stopButton.interactable = true;

   }

    private void StopRecording(){
        if (!isRecording) return;

        int lastSample = Microphone.GetPosition(microphoneName);
        Microphone.End(microphoneName);
        isRecording = false;

        displayText.text = "Processing...";

        startButton.interactable = true;
        stopButton.interactable = false;

        if (lastSample > 0)
        {
            float[] samples = new float[lastSample * recordedClip.channels];
            recordedClip.GetData(samples, 0);
            
            AudioClip trimmedClip = AudioClip.Create("TrimmedRecording", lastSample, recordedClip.channels, recordedClip.frequency, false);
            trimmedClip.SetData(samples, 0);
            recordedClip = trimmedClip;
        }

        StartCoroutine(SendAudioToWhisper(recordedClip));



    }


   
    
    private IEnumerator SendAudioToWhisper(AudioClip clip)
    {
        // Convert AudioClip to WAV format
        byte[] wavData = ConvertAudioClipToWav(clip);

        // Create form data
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
        form.AddField("model", whisperModelName);

        // Send request
        using (UnityWebRequest www = UnityWebRequest.Post(whisperApiUrl, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    // Parse JSON response
                    string jsonResponse = www.downloadHandler.text;
                    WhisperResponse response = JsonUtility.FromJson<WhisperResponse>(jsonResponse);
                    
                    if (displayText != null)
                    {
                        displayText.text = $"{response.text}";
                        modelImporter.promptText = response.text;
                    }
                    
                    Debug.Log($"Transcription: {response.text}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing response: {e.Message}");
                    if (displayText != null)
                    {
                        displayText.text = "Error parsing transcription";
                    }
                }
            }
            else
            {
                Debug.LogError($"Error: {www.error}");
                Debug.LogError($"Response: {www.downloadHandler.text}");
                
                if (displayText != null)
                {
                    displayText.text = $"Error: {www.error}";
                }
            }
        }
    }

    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        Int16[] intData = new Int16[samples.Length];
        Byte[] bytesData = new Byte[samples.Length * 2];

        int rescaleFactor = 32767; // to convert float to Int16

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (Int16)(samples[i] * rescaleFactor);
            Byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        // Create WAV header
        int fileSize = bytesData.Length + 44;
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int bitsPerSample = 16;

        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                // RIFF header
                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(fileSize - 8);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));

                // fmt chunk
                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16); // chunk size
                writer.Write((Int16)1); // audio format (1 = PCM)
                writer.Write((Int16)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * bitsPerSample / 8); // byte rate
                writer.Write((Int16)(channels * bitsPerSample / 8)); // block align
                writer.Write((Int16)bitsPerSample);

                // data chunk
                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(bytesData.Length);
                writer.Write(bytesData);
            }

            return stream.ToArray();
        }
    }

    
    //cleanup
    void OnDestroy(){
        if (isRecording)
        {
            Microphone.End(microphoneName);
        }
    }


    [Serializable]
    private class WhisperResponse
    {
        public string text;
    }



}
