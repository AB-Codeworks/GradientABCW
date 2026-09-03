using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Project-wide settings for the Gradient ABCW editor tools.</summary>
    [FilePath("ProjectSettings/GradientABCWSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GradientABCWSettings : ScriptableSingleton<GradientABCWSettings>
    {
        private const string DefaultFolder = "Assets/Gradients";
        private const int DefaultPreviewResolution = 256;

        [SerializeField] private string defaultLibraryFolder = DefaultFolder;
        [SerializeField] private int previewResolution = DefaultPreviewResolution;
        [SerializeField] private bool pickerLivePreview = true;

        public string DefaultLibraryFolder
        {
            get => string.IsNullOrEmpty(defaultLibraryFolder) ? DefaultFolder : defaultLibraryFolder;
            set
            {
                defaultLibraryFolder = string.IsNullOrEmpty(value) ? DefaultFolder : value;
                Persist();
            }
        }

        public int PreviewResolution
        {
            get => previewResolution > 0 ? previewResolution : DefaultPreviewResolution;
            set
            {
                previewResolution = Mathf.Max(2, value);
                Persist();
            }
        }

        public bool PickerLivePreview
        {
            get => pickerLivePreview;
            set
            {
                pickerLivePreview = value;
                Persist();
            }
        }

        private void Persist() => Save(true);
    }
}
