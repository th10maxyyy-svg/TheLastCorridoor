using UnityEngine;
using UnityEditor;
using System.IO;

public class JumpscareGenerator
{
    [MenuItem("Tools/Generate Jumpscare Sound")]
    public static void GenerateSound()
    {
        int sampleRate = 44100;
        float duration = 1.5f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        System.Random rand = new System.Random();
        
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            
            // Envelope: Sudden max volume, then fades out quickly
            float envelope = Mathf.Exp(-t * 3.5f);
            
            // 1. Bass Boom (Low frequency that drops)
            float bassFreq = Mathf.Lerp(150f, 20f, t * 5f);
            float boom = Mathf.Sin(2 * Mathf.PI * bassFreq * t) * 0.7f;
            
            // 2. Metallic Screech (High pitch distortion)
            float screech = Mathf.Sin(2 * Mathf.PI * (1200f + Mathf.Sin(2 * Mathf.PI * 40f * t) * 600f) * t) * 0.5f;
            
            // 3. Harsh Noise (Static burst)
            float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.6f;

            // Combine and apply envelope
            samples[i] = (boom + screech + noise) * envelope;
            
            // Add extreme distortion (clipping) to make it sound scary
            if (samples[i] > 1f) samples[i] = 1f;
            if (samples[i] < -1f) samples[i] = -1f;
        }

        string path = "d:/New folder/TheLastCorridoor/My project/Assets/_Recovery/JumpscareSound.wav";
        SaveWav(path, samples, sampleRate);
        
        AssetDatabase.Refresh();
        Debug.Log("JumpscareSound.wav has been created in _Recovery folder!");
    }

    private static void SaveWav(string path, float[] samples, int sampleRate)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        {
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write(new char[4] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + samples.Length * 2);
                bw.Write(new char[4] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[4] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write((short)1); // Channels
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2); // Byte Rate
                bw.Write((short)2); // Block Align
                bw.Write((short)16); // Bits per sample
                bw.Write(new char[4] { 'd', 'a', 't', 'a' });
                bw.Write(samples.Length * 2);

                for (int i = 0; i < samples.Length; i++)
                {
                    short sample = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                    bw.Write(sample);
                }
            }
        }
    }
}
