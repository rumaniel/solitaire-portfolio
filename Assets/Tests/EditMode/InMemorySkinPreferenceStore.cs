using Model.Skin;
using Service.SkinService;

namespace Tests.EditMode
{
    internal class InMemorySkinPreferenceStore : ISkinPreferenceStore
    {
        private bool hasValue;
        private SkinId value;
        public int SaveCalls { get; private set; }

        public InMemorySkinPreferenceStore() { }
        public InMemorySkinPreferenceStore(SkinId initial) { hasValue = true; value = initial; }

        public bool TryLoad(out SkinId id) { id = value; return hasValue; }
        public void Save(SkinId id) { hasValue = true; value = id; SaveCalls++; }
    }
}
