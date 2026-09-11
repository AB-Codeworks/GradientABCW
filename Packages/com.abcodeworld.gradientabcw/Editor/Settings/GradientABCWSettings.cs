using UnityEditor;
using UnityEngine;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>Project-wide settings for the Gradient ABCW editor tools.</summary>
    [FilePath("ProjectSettings/GradientABCWSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class GradientABCWSettings : ScriptableSingleton<GradientABCWSettings>
    {
        private const string DefaultFolder = "Assets/Gradients";
        private const string Default3DFolder = "Assets/Gradients3D";

        /// <summary>
        /// Width every preview strip is baked at, independent of how wide it is drawn.
        /// </summary>
        /// <remarks>
        /// Previews used to be baked at their exact pixel width, which meant destroying and recreating the
        /// texture on every inspector resize. A fixed width removes that entirely and makes bake cost
        /// constant; the strip is one pixel tall, so the extra resolution is nearly free and keeps wide
        /// inspectors sharp.
        /// </remarks>
        private const int DefaultPreviewResolution = 512;

        [SerializeField] private string defaultLibraryFolder = DefaultFolder;
        [SerializeField] private string default3DLibraryFolder = Default3DFolder;
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

        /// <summary>Where the 3D picker's library panel looks. Separate from the 1D folder, so the two
        /// kinds of gradient asset do not have to share a directory.</summary>
        public string Default3DLibraryFolder
        {
            get => string.IsNullOrEmpty(default3DLibraryFolder) ? Default3DFolder : default3DLibraryFolder;
            set
            {
                default3DLibraryFolder = string.IsNullOrEmpty(value) ? Default3DFolder : value;
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
