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

                    var folder3DField = new TextField("Default 3D Library Folder")
                    {
                        value = settings.Default3DLibraryFolder,
                        style = { marginTop = 8 },
                    };
                    folder3DField.RegisterValueChangedCallback(evt => settings.Default3DLibraryFolder = evt.newValue);
                    rootElement.Add(folder3DField);

                    var browse3DRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
                    browse3DRow.Add(new Button(() => BrowseFolder(folder3DField)) { text = "Browse..." });
                    browse3DRow.Add(new Button(() =>
                    {
                        settings.Default3DLibraryFolder = "Assets/Gradients3D";
                        folder3DField.SetValueWithoutNotify(settings.Default3DLibraryFolder);
                    })
                    { text = "Reset" });
                    rootElement.Add(browse3DRow);

                    var livePreviewToggle = new Toggle("Live Preview In Picker") { value = settings.PickerLivePreview, style = { marginTop = 8 } };
                    livePreviewToggle.RegisterValueChangedCallback(evt => settings.PickerLivePreview = evt.newValue);
                    rootElement.Add(livePreviewToggle);

                    // Exposed now that it is actually read. Previews bake at this width and are stretched
                    // to fit, so it trades preview sharpness on wide inspectors against bake cost.
                    var resolutionField = new IntegerField("Preview Resolution")
                    {
                        value = settings.PreviewResolution,
                        isDelayed = true,
                        tooltip = "Width every gradient preview strip is baked at, independent of how wide it is drawn.",
                        style = { marginTop = 4 },
                    };
                    resolutionField.RegisterValueChangedCallback(evt =>
                    {
                        settings.PreviewResolution = evt.newValue;
                        resolutionField.SetValueWithoutNotify(settings.PreviewResolution);
                    });
                    rootElement.Add(resolutionField);
                },
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "Gradient", "ABCW", "3D" }),
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
