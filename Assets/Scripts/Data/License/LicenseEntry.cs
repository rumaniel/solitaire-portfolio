using System;
using UnityEngine;

namespace Data.License
{
    /// <summary>Single entry in the Open Source Licenses list.</summary>
    [Serializable]
    public class LicenseEntry
    {
        [SerializeField] private string name;
        [SerializeField] private string author;
        [SerializeField] private string licenseName;
        [SerializeField] private string sourceUrl;
        [TextArea(4, 12)]
        [SerializeField] private string fullText;

        public string Name => name;
        public string Author => author;
        public string LicenseName => licenseName;
        public string SourceUrl => sourceUrl;
        public string FullText => fullText;
    }
}
