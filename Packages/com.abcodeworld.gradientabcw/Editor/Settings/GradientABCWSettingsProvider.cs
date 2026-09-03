using UnityEditor;
using UnityEngine.UIElements;

namespace ABCodeworld.Gradients.Editor
{
    internal static class GradientABCWSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/ABCodeworld/Gradient ABCW", SettingsScope.Project)
            {
                label = "Gradient ABCW",
                activateHandler = (searchContext, rootElement) =>
                {
                    var settings = GradientABCWSettings.instance;
                    rootElement.style.paddingLeft = 8;
                    rootElement.style.paddingTop = 8;

                    var folderField = new TextField("Default Library Folder") { value = settings.DefaultLibraryFolder };
                    folderField.RegisterValueChangedCallback(evt => settings.DefaultLibraryFolder = evt.newValue);
                    rootElement.Add(folderField);

                    var browseRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
                    var browseButton = new Button(() => BrowseFolder(folderField)) { text = "Browse..." };
                    var resetButton = new Button(() =>
                    {
                        settings.DefaultLibraryFolder = "Assets/Gradients";
                        folderField.SetValueWithoutNotify(settings.DefaultLibraryFolder);
                    })
                    { text = "Reset" };
                    browseRow.Add(browseButton);
                    browseRow.Add(resetButton);
                    rootElement.Add(browseRow);

                    var livePreviewToggle = new Toggle("Live Preview In Picker") { value = settings.PickerLivePreview, style = { marginTop = 8 } };
                    livePreviewToggle.RegisterValueChangedCallback(evt => settings.PickerLivePreview = evt.newValue);
                    rootElement.Add(livePreviewToggle);
                },
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "Gradient", "ABCW" }),
            };
        }

        private static void BrowseFolder(TextField field)
        {
            string start = UnityEngine.Application.dataPath;
            string abs = EditorUtility.OpenFolderPanel("Select Default Gradient Library Folder", start, "");
            if (string.IsNullOrEmpty(abs) || !abs.StartsWith(start))
                return;

            string rel = "Assets" + abs.Substring(start.Length).Replace("\\", "/");
            field.value = rel;
        }
    }
}
