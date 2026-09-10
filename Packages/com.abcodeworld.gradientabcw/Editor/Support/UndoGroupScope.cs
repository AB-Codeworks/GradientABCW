using UnityEditor;

namespace ABCodeworld.Gradients.Editor
{
    /// <summary>
    /// A named undo group opened at construction, which the caller later either collapses into one step
    /// or reverts entirely.
    /// </summary>
    /// <remarks>
    /// A picker session writes through to the serialized object on every live-preview push, so it leaves
    /// one undo record per edit. Collapsing them on accept turns a whole editing session into a single
    /// Ctrl+Z, and reverting them on cancel undoes every push the session made. Both gradient property
    /// drawers need exactly that, so the three <see cref="Undo"/> calls live here rather than being
    /// written out per drawer.
    /// <para>
    /// Deliberately not <c>IDisposable</c>: which of <see cref="Collapse"/> and
    /// <see cref="RevertDownTo"/> runs is decided asynchronously, when the picker window closes, so there
    /// is no scope for a <c>using</c> statement to end at.
    /// </para>
    /// </remarks>
    internal readonly struct UndoGroupScope
    {
        private readonly int group;

        private UndoGroupScope(int group) => this.group = group;

        /// <summary>The undo group index this scope opened.</summary>
        public int Group => group;

        /// <summary>Starts a new undo group named <paramref name="name"/> and returns a scope for it.</summary>
        public static UndoGroupScope Begin(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return new UndoGroupScope(Undo.GetCurrentGroup());
        }

        /// <summary>Merges everything recorded since <see cref="Begin"/> into one undo step.</summary>
        public void Collapse() => Undo.CollapseUndoOperations(group);

        /// <summary>Undoes everything recorded since <see cref="Begin"/>.</summary>
        public void RevertDownTo() => Undo.RevertAllDownToGroup(group);
    }
}
