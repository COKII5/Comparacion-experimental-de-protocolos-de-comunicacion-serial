using UnityEngine;

namespace PhysicalDigital.Game
{
    public sealed class ToneBank : MonoBehaviour
    {
        private static readonly float[] PadFrequencies = { 209f, 252f, 310f, 415f };
        private const int SampleRate = 44100;
        private const float PadToneSeconds = 0.42f;
        private const float ErrorFrequency = 90f;
        private const float ErrorToneSeconds = 0.7f;
        private const float ErrorVolume = 0.8f;
        private const float AttackSeconds = 0.006f;
        private const float ReleaseSeconds = 0.15f;
        private const float MasterGain = 0.3f;
        private const float SquareAmplitude = 0.45f;
        private const float OvertoneLevel = 0.25f;
        private const int OvertoneMultiple = 2;
        private const int MonoChannels = 1;

        private AudioSource source;
        private AudioClip[] padClips;
        private AudioClip errorClip;

        public void PlayPad(int index)
        {
            source.PlayOneShot(padClips[index]);
        }

        public void PlayError()
        {
            source.PlayOneShot(errorClip, ErrorVolume);
        }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            padClips = new AudioClip[PadFrequencies.Length];
            for (int i = 0; i < PadFrequencies.Length; i++)
            {
                padClips[i] = MakeTone($"Pad{i + 1}", PadFrequencies[i], PadToneSeconds, false);
            }
            errorClip = MakeTone("Error", ErrorFrequency, ErrorToneSeconds, true);
        }

        private static AudioClip MakeTone(string name, float frequency, float duration, bool square)
        {
            int sampleCount = Mathf.RoundToInt(SampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float seconds = i / (float)SampleRate;
                float envelope = Mathf.Min(1f, seconds / AttackSeconds) * Mathf.Clamp01((duration - seconds) / ReleaseSeconds);
                float phase = 2f * Mathf.PI * frequency * seconds;
                float wave = square
                    ? Mathf.Sign(Mathf.Sin(phase)) * SquareAmplitude
                    : Mathf.Sin(phase) + OvertoneLevel * Mathf.Sin(OvertoneMultiple * phase);
                samples[i] = MasterGain * envelope * wave;
            }
            AudioClip clip = AudioClip.Create(name, sampleCount, MonoChannels, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
