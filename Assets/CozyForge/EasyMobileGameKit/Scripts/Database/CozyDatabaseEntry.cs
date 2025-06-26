using UnityEngine;

namespace CozyFramework
{
    public abstract class CozyDatabaseEntry : ScriptableObject
    {
        [SerializeField] private string entryName;
        [SerializeField] private string id;
        [SerializeField] [TextArea] private string description;
        [SerializeField] private Sprite icon;

        public string EntryName => entryName;
        public string Id => id;
        public string Description => description;
        public Sprite Icon => icon;
    }
}