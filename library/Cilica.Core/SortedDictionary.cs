// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Bounded adaptation of pinned SortedDictionary/SortedSet; see ../provenance.json.
namespace System.Collections.Generic
{
    public class SortedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, Collections.IDictionary, IReadOnlyDictionary<TKey, TValue>
    {
        private readonly SortedSet<KeyValuePair<TKey, TValue>> tree;
        private readonly IComparer<TKey> comparer;
        [Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.InternalCall)]
        public extern SortedDictionary(IComparer<TKey> comparer);
        private SortedDictionary(IComparer<TKey> comparer, bool admitted)
        { this.comparer = comparer; tree = new SortedSet<KeyValuePair<TKey, TValue>>(new PairComparer(comparer), true); }
        public static SortedDictionary<TKey, TValue> CreateOrdinal(IComparer<TKey> comparer)
        { return new SortedDictionary<TKey, TValue>(comparer, true); }
        internal sealed class PairComparer : IComparer<KeyValuePair<TKey, TValue>>
        {
            private readonly IComparer<TKey> comparer;
            internal PairComparer(IComparer<TKey> comparer) { this.comparer = comparer; }
            public int Compare(KeyValuePair<TKey, TValue> first, KeyValuePair<TKey, TValue> second)
            { return comparer.Compare(first.Key, second.Key); }
        }
        public int Count => tree.Count;
        public IComparer<TKey> Comparer => comparer;
        public void Add(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            var item = new KeyValuePair<TKey, TValue>(key, value);
            if (!tree.AddItem(item))
            {
                string valueText = value == null ? "" : Cilica.Runtime.DictionaryKeyText<TValue>.Render(value);
                string pairText = string.Concat(string.Concat("[", Cilica.Runtime.DictionaryKeyText<TKey>.Render(key)), string.Concat(", ", string.Concat(valueText, "]")));
                throw new ArgumentException(string.Concat("An item with the same key has already been added. Key: ", pairText));
            }
        }
        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException("key");
            return tree.Find(new KeyValuePair<TKey, TValue>(key, default)) != null;
        }
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            var node = tree.Find(new KeyValuePair<TKey, TValue>(key, default));
            if (node == null) { value = default; return false; }
            value = node.Item.Value; return true;
        }
        public TValue this[TKey key]
        {
            get
            {
                if (key == null) throw new ArgumentNullException("key");
                var node = tree.Find(new KeyValuePair<TKey, TValue>(key, default));
                if (node == null) throw new KeyNotFoundException(string.Concat("The given key '", string.Concat(Cilica.Runtime.DictionaryKeyText<TKey>.Render(key), "' was not present in the dictionary.")));
                return node.Item.Value;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");
                var node = tree.Find(new KeyValuePair<TKey, TValue>(key, default));
                if (node == null) Add(key, value);
                else { node.Item = new KeyValuePair<TKey, TValue>(node.Item.Key, value); tree.UpdateVersion(); }
            }
        }
        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Cilica.Runtime.UnsupportedLibraryMember<ICollection<TKey>>.Invoke();
        ICollection<TValue> IDictionary<TKey, TValue>.Values => Cilica.Runtime.UnsupportedLibraryMember<ICollection<TValue>>.Invoke();
        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Cilica.Runtime.UnsupportedLibraryMember<IEnumerable<TKey>>.Invoke();
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Cilica.Runtime.UnsupportedLibraryMember<IEnumerable<TValue>>.Invoke();
        public bool Remove(TKey key) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        public void Clear() { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        bool Collections.ICollection.IsSynchronized => false;
        object Collections.ICollection.SyncRoot => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
        void Collections.ICollection.CopyTo(Array array, int index) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.IDictionary.IsFixedSize => false;
        bool Collections.IDictionary.IsReadOnly => false;
        Collections.ICollection Collections.IDictionary.Keys => Cilica.Runtime.UnsupportedLibraryMember<Collections.ICollection>.Invoke();
        Collections.ICollection Collections.IDictionary.Values => Cilica.Runtime.UnsupportedLibraryMember<Collections.ICollection>.Invoke();
        object Collections.IDictionary.this[object key] {
            get => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            set { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        }
        void Collections.IDictionary.Add(object key, object value) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        bool Collections.IDictionary.Contains(object key) => Cilica.Runtime.UnsupportedLibraryMember<bool>.Invoke();
        Collections.IDictionaryEnumerator Collections.IDictionary.GetEnumerator() => Cilica.Runtime.UnsupportedLibraryMember<Collections.IDictionaryEnumerator>.Invoke();
        void Collections.IDictionary.Remove(object key) { Cilica.Runtime.UnsupportedLibraryMember<int>.Invoke(); }
        public Enumerator GetEnumerator() => new Enumerator(tree);
        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => Count == 0
            ? SZGenericArrayEnumerator<KeyValuePair<TKey, TValue>>.Create(null, 0) : tree.GetEnumerator();
        Collections.IEnumerator Collections.IEnumerable.GetEnumerator() => ((IEnumerable<KeyValuePair<TKey, TValue>>)this).GetEnumerator();
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, Collections.IDictionaryEnumerator
        {
            private SortedSet<KeyValuePair<TKey, TValue>>.Enumerator cursor;
            internal Enumerator(SortedSet<KeyValuePair<TKey, TValue>> tree) { cursor = tree.GetEnumerator(); }
            public bool MoveNext() => cursor.MoveNext();
            public KeyValuePair<TKey, TValue> Current => cursor.Current;
            public void Dispose() { }
            void Collections.IEnumerator.Reset() { cursor.Reset(); }
            object Collections.IEnumerator.Current { get { cursor.CheckCurrent(); return cursor.Current; } }
            Collections.DictionaryEntry Collections.IDictionaryEnumerator.Entry => Cilica.Runtime.UnsupportedLibraryMember<Collections.DictionaryEntry>.Invoke();
            object Collections.IDictionaryEnumerator.Key => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
            object Collections.IDictionaryEnumerator.Value => Cilica.Runtime.UnsupportedLibraryMember<object>.Invoke();
        }
    }

    // Only the internal ordered-dictionary tree and its public cursor identity
    // are admitted here. Public SortedSet construction/mutation is not supplied.
    public class SortedSet<T>
    {
        private Cilica.Runtime.OrderedTreeNode<T> root;
        private int count;
        private int version;
        private readonly IComparer<T> comparer;
        internal SortedSet(IComparer<T> comparer, bool admitted) { this.comparer = comparer; }
        internal int Count => count;
        internal int EnumerationVersion => version;
        internal Cilica.Runtime.OrderedTreeNode<T> EnumerationRoot => root;
        internal void UpdateVersion() { version = unchecked(version + 1); }
        internal Cilica.Runtime.OrderedTreeNode<T> Find(T item)
        {
            Cilica.Runtime.OrderedTreeNode<T> current = root;
            while (current != null)
            {
                int order = comparer.Compare(item, current.Item);
                if (order == 0) return current;
                current = order < 0 ? current.Left : current.Right;
            }
            return null;
        }
        internal bool AddItem(T item)
        {
            if (root == null) { root = new Cilica.Runtime.OrderedTreeNode<T>(item, false); count = 1; UpdateVersion(); return true; }
            Cilica.Runtime.OrderedTreeNode<T> current = root, parent = null, grandParent = null, greatGrandParent = null;
            // A failed duplicate or allocation may still follow tree rotations.
            UpdateVersion();
            int order = 0;
            while (current != null)
            {
                order = comparer.Compare(item, current.Item);
                if (order == 0) { root.Red = false; return false; }
                if (current.Left != null && current.Left.Red && current.Right != null && current.Right.Red)
                {
                    current.Red = true; current.Left.Red = false; current.Right.Red = false;
                    if (parent != null && parent.Red) Balance(current, ref parent, grandParent, greatGrandParent);
                }
                greatGrandParent = grandParent; grandParent = parent; parent = current;
                current = order < 0 ? current.Left : current.Right;
            }
            Cilica.Runtime.OrderedTreeNode<T> node = new Cilica.Runtime.OrderedTreeNode<T>(item, true);
            if (order > 0) parent.Right = node; else parent.Left = node;
            if (parent.Red) Balance(node, ref parent, grandParent, greatGrandParent);
            root.Red = false; count++; return true;
        }
        private void Balance(Cilica.Runtime.OrderedTreeNode<T> current, ref Cilica.Runtime.OrderedTreeNode<T> parent, Cilica.Runtime.OrderedTreeNode<T> grandParent, Cilica.Runtime.OrderedTreeNode<T> greatGrandParent)
        {
            bool parentRight = grandParent.Right == parent, currentRight = parent.Right == current;
            Cilica.Runtime.OrderedTreeNode<T> replacement;
            if (parentRight == currentRight) replacement = currentRight ? grandParent.RotateLeft() : grandParent.RotateRight();
            else { replacement = currentRight ? grandParent.RotateLeftRight() : grandParent.RotateRightLeft(); parent = greatGrandParent; }
            grandParent.Red = true; replacement.Red = false;
            if (greatGrandParent == null) root = replacement;
            else if (greatGrandParent.Left == grandParent) greatGrandParent.Left = replacement;
            else greatGrandParent.Right = replacement;
        }
        internal Enumerator GetEnumerator() => new Enumerator(this);
        // Cursor copies intentionally share their pending-node stack, as .NET does.
        internal sealed class Path
        {
            private readonly Cilica.Runtime.OrderedTreeNode<T>[] nodes;
            private int count;
            internal Path(int capacity) { nodes = new Cilica.Runtime.OrderedTreeNode<T>[capacity]; }
            internal int Count => count;

            internal void Push(Cilica.Runtime.OrderedTreeNode<T> node) { nodes[count++] = node; }
            internal Cilica.Runtime.OrderedTreeNode<T> Pop() { int index = --count; Cilica.Runtime.OrderedTreeNode<T> node = nodes[index]; nodes[index] = null; return node; }
            internal void Clear() { while (count != 0) { nodes[--count] = null; } }
        }
        public struct Enumerator : IEnumerator<T>, Collections.IEnumerator
        {
            private readonly SortedSet<T> tree;
            private readonly int version;
            private readonly Path path;
            private Cilica.Runtime.OrderedTreeNode<T> current;
            internal Enumerator(SortedSet<T> tree)
            {
                this.tree = tree; version = tree.EnumerationVersion; current = null;
                int height = 0; uint total = unchecked((uint)(tree.Count + 1));
                while (total > 1) { total >>= 1; height++; }
                path = new Path(2 * height); Initialize();
            }
            private void Initialize()
            {
                current = null; Cilica.Runtime.OrderedTreeNode<T> node = tree.EnumerationRoot;
                while (node != null) { path.Push(node); node = node.Left; }
            }
            public bool MoveNext()
            {
                if (version != tree.EnumerationVersion) throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                if (path.Count == 0) { current = null; return false; }
                current = path.Pop(); Cilica.Runtime.OrderedTreeNode<T> node = current.Right;
                while (node != null) { path.Push(node); node = node.Left; }
                return true;
            }
            public T Current => current == null ? default : current.Item;
            public void Dispose() { }
            internal void CheckCurrent()
            { if (current == null) throw new InvalidOperationException("Enumeration has either not started or has already finished."); }
            object Collections.IEnumerator.Current { get { CheckCurrent(); return current.Item; } }
            internal void Reset()
            {
                if (version != tree.EnumerationVersion) throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                path.Clear(); Initialize();
            }
            void Collections.IEnumerator.Reset() { Reset(); }
        }
    }
}

namespace Cilica.Runtime
{
        public sealed class OrderedTreeNode<T>
        {
            internal T Item { get; set; }
            internal OrderedTreeNode<T> Left { get; set; }
            internal OrderedTreeNode<T> Right { get; set; }
            internal bool Red { get; set; }
            internal OrderedTreeNode(T item, bool red) { Item = item; Red = red; }
            internal OrderedTreeNode<T> RotateLeft() { OrderedTreeNode<T> child = Right; Right = child.Left; child.Left = this; return child; }
            internal OrderedTreeNode<T> RotateRight() { OrderedTreeNode<T> child = Left; Left = child.Right; child.Right = this; return child; }
            internal OrderedTreeNode<T> RotateLeftRight()
            {
                OrderedTreeNode<T> child = Left; OrderedTreeNode<T> grandChild = child.Right;
                Left = grandChild.Right; grandChild.Right = this;
                child.Right = grandChild.Left; grandChild.Left = child; return grandChild;
            }
            internal OrderedTreeNode<T> RotateRightLeft()
            {
                OrderedTreeNode<T> child = Right; OrderedTreeNode<T> grandChild = child.Left;
                Right = grandChild.Left; grandChild.Left = this;
                child.Left = grandChild.Right; grandChild.Right = child; return grandChild;
            }
        }
}
