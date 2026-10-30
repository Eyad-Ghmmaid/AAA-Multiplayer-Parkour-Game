using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    public class FSAudioUtil
    {
        static AudioSource _audioSource;
        public static void PlaySfx(AudioClip clip, bool overridePlayingAudio=false)
        {
            if (clip == null) return;

            if (_audioSource == null)
            {
                GameObject sfx = new GameObject("FS Audio Source");
                _audioSource = sfx.AddComponent<AudioSource>();
            }

            if (_audioSource.isPlaying && !overridePlayingAudio)
                return;

            _audioSource.clip = clip;
            _audioSource.Play();
        }

        public static void PlaySfxNew(AudioClip clip)
        {
            if (clip == null) return;

            GameObject sfx = new GameObject("FS Audio Source");
            var audioSource = sfx.AddComponent<AudioSource>();

            audioSource.clip = clip;
            audioSource.Play();

            Object.Destroy(sfx, clip.length + 1f);
        }

        public static void PlaySfxNew(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;

            GameObject sfx = new GameObject("FS Audio Source");
            sfx.transform.position = position;
            var audioSource = sfx.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.volume = volume;
            audioSource.clip = clip;
            audioSource.Play();

            Object.Destroy(sfx, clip.length + 1f);
        }

        public static AudioSource CreateSource(GameObject gameObject, AudioClip clip, bool loop, float volume)
        {
            if (clip == null) return null;

            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 50f;

            return source;
        }
    }
}
