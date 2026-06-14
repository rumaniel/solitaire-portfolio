using System;
using UnityEngine;

namespace Data.Audio
{
    [Serializable]
    public struct AudioClipReference
    {
        [SerializeField] private string key;
        [SerializeField] private AudioClip clip;

        public string Key => key;
        public AudioClip Clip => clip;
        public bool HasClip => clip != null;
    }
}
