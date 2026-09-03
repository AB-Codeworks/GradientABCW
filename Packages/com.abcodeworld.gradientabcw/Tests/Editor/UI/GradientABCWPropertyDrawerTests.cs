using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;
using ABCodeworld.Gradients.Editor;
using ABCodeworld.Gradients.Tests.Editor.Support;
using Object = UnityEngine.Object;

namespace ABCodeworld.Gradients.Tests.Editor.UI
{
    [TestFixture]
    [Category("UI")]
    internal sealed class GradientABCWPropertyDrawerTests : UITestFixture
    {
        private TestHostObject host;
        private InspectorElement inspector;

        [SetUp]
        public void SetUpInspector()
        {
            panelSize = new Vector2(400, 500);
            host = ScriptableObject.CreateInstance<TestHostObject>();
            host.gradient = TestGradients.Rainbow7();
            inspector = InspectorTestUtility.CreateInspector(host);
            rootVisualElement.Add(inspector);
            simulate.FrameUpdate();
        }

        [TearDown]
        public void TearDownInspector()
        {
            Object.DestroyImmediate(host);
        }

        [Test]
        public void Inspector_RendersGradientField()
        {
            var field = inspector.Q<GradientABCWField>();
            Assert.That(field, Is.Not.Null);
        }

        [Test]
        public void TogglingBypass_WritesThroughToSerializedObject()
        {
            var field = inspector.Q<GradientABCWField>();
            field.Q<Foldout>().value = true; // expand the collapsed-by-default modulation section
            simulate.FrameUpdate();

            var bypassToggle = field.Q<Toggle>("bypass");
            Assert.That(bypassToggle, Is.Not.Null);

            simulate.Click(bypassToggle);
            simulate.FrameUpdate();

            Assert.That(host.gradient.Modulation.bypass, Is.True);
        }

        [Test]
        public void Undo_RevertsWrittenValueAndFieldFollows()
        {
            Undo.IncrementCurrentGroup();

            var field = inspector.Q<GradientABCWField>();
            field.Q<Foldout>().value = true; // expand the collapsed-by-default modulation section
            simulate.FrameUpdate();

            var bypassToggle = field.Q<Toggle>("bypass");

            simulate.Click(bypassToggle);
            simulate.FrameUpdate();
            Assert.That(host.gradient.Modulation.bypass, Is.True);

            Undo.PerformUndo();
            simulate.FrameUpdate();

            Assert.That(host.gradient.Modulation.bypass, Is.False);
            Assert.That(field.value.Modulation.bypass, Is.False);
        }

        [Test]
        public void MultipleDifferentValues_ShowsDisabledElement()
        {
            var host2 = ScriptableObject.CreateInstance<TestHostObject>();
            host2.gradient = TestGradients.Default();

            var so = new SerializedObject(new Object[] { host, host2 });
            var property = so.FindProperty(nameof(TestHostObject.gradient));

            var drawer = new GradientABCWPropertyDrawer();
            var element = drawer.CreatePropertyGUI(property);

            Assert.That(element.enabledSelf, Is.False);

            Object.DestroyImmediate(host2);
        }
    }
}
