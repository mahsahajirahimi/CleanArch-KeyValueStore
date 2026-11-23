namespace KeyValueStore.Infrastructure.Storage.Persistent;

internal sealed class BPlusTree
{
    private const int MaxKeys = 3;
    private const int MinLeafKeys = 2;
    private const int MinInternalChildren = 2;

    private Node _root = new LeafNode();

    private readonly record struct IndexKey(
        string Keyspace,
        string Key);

    private abstract class Node
    {
        public List<IndexKey> Keys { get; } = new();
    }

    private sealed class LeafNode : Node
    {
        public List<long> Offsets { get; } = new();
        public LeafNode? Next { get; set; }
    }

    private sealed class InternalNode : Node
    {
        public List<Node> Children { get; } = new();
    }

    private readonly record struct SplitResult(
        IndexKey Separator,
        Node Right);

    public long? FindOffset(string keyspace, string key)
    {
        var searchKey = new IndexKey(keyspace, key);
        Node current = _root;
        while (current is InternalNode internalNode)
        {
            int index = FindChildIndex(
                internalNode.Keys, searchKey);
            current = internalNode.Children[index];
        }

        var leaf = (LeafNode)current;
        int position = FindPosition(leaf.Keys, searchKey);
        if (position < leaf.Keys.Count &&
            Compare(leaf.Keys[position], searchKey) == 0)
        {
            return leaf.Offsets[position];
        }
        return null;
    }

    public void Upsert(string keyspace, string key, long offset)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var indexKey = new IndexKey(keyspace, key);
        SplitResult? split = Insert(_root, indexKey, offset);
        if (split is null)
            return;
        var newRoot = new InternalNode();

        newRoot.Keys.Add(split.Value.Separator);
        newRoot.Children.Add(_root);
        newRoot.Children.Add(split.Value.Right);

        _root = newRoot;
    }

    private static SplitResult? Insert(
        Node node,
        IndexKey key,
        long offset)
    {
        if (node is LeafNode leaf)
        {
            int position = FindPosition(leaf.Keys, key);

            if (position < leaf.Keys.Count &&
                Compare(leaf.Keys[position], key) == 0)
            {
                leaf.Offsets[position] = offset;
                return null;
            }

            leaf.Keys.Insert(position, key);
            leaf.Offsets.Insert(position, offset);

            if (leaf.Keys.Count <= MaxKeys)
                return null;

            int middle = leaf.Keys.Count / 2;

            var right = new LeafNode();

            right.Keys.AddRange(
                leaf.Keys.GetRange(
                    middle, leaf.Keys.Count - middle));

            right.Offsets.AddRange(
                leaf.Offsets.GetRange(
                    middle, leaf.Offsets.Count - middle));

            leaf.Keys.RemoveRange(
                middle, leaf.Keys.Count - middle);

            leaf.Offsets.RemoveRange(
                middle, leaf.Offsets.Count - middle);

            right.Next = leaf.Next;
            leaf.Next = right;

            return new SplitResult(right.Keys[0], right);
        }

        var parent = (InternalNode)node;

        int childIndex = FindChildIndex(parent.Keys, key);

        SplitResult? childSplit = Insert(
            parent.Children[childIndex], key, offset);

        if (childSplit is null)
            return null;

        parent.Keys.Insert(
            childIndex, childSplit.Value.Separator);

        parent.Children.Insert(
            childIndex + 1, childSplit.Value.Right);

        if (parent.Keys.Count <= MaxKeys)
            return null;

        int mid = parent.Keys.Count / 2;
        IndexKey separator = parent.Keys[mid];

        var newRight = new InternalNode();

        newRight.Keys.AddRange(
            parent.Keys.GetRange(
                mid + 1, parent.Keys.Count - mid - 1));

        newRight.Children.AddRange(
            parent.Children.GetRange(
                mid + 1, parent.Children.Count - mid - 1));

        parent.Keys.RemoveRange(
            mid, parent.Keys.Count - mid);

        parent.Children.RemoveRange(
            mid + 1, parent.Children.Count - mid - 1);

        return new SplitResult(separator, newRight);
    }

    private static int FindPosition(
        List<IndexKey> keys,
        IndexKey key)
    {
        int index = 0;

        while (index < keys.Count &&
               Compare(keys[index], key) < 0)
        {
            index++;
        }

        return index;
    }

    private static int FindChildIndex(
        List<IndexKey> keys,
        IndexKey key)
    {
        int index = 0;

        while (index < keys.Count &&
               Compare(key, keys[index]) >= 0)
        {
            index++;
        }

        return index;
    }

    private static int Compare(IndexKey left, IndexKey right)
    {
        int result = string.CompareOrdinal(
            left.Keyspace, right.Keyspace);

        return result != 0
            ? result
            : string.CompareOrdinal(left.Key, right.Key);
    }


public bool Delete(string keyspace, string key)
    {
        var indexKey = new IndexKey(keyspace, key);

        if (!DeleteFromNode(_root, indexKey))
            return false;

        if (_root is InternalNode root &&
            root.Children.Count == 1)
        {
            _root = root.Children[0];
        }

        return true;
    }

    private static bool DeleteFromNode(Node node, IndexKey key)
    {
        if (node is LeafNode leaf)
        {
            int position = FindPosition(leaf.Keys, key);

            if (position >= leaf.Keys.Count ||
                Compare(leaf.Keys[position], key) != 0)
            {
                return false;
            }

            leaf.Keys.RemoveAt(position);
            leaf.Offsets.RemoveAt(position);

            return true;
        }

        var parent = (InternalNode)node;
        int childIndex = FindChildIndex(parent.Keys, key);

        if (!DeleteFromNode(parent.Children[childIndex], key))
            return false;

        Rebalance(parent, childIndex);
        RefreshSeparators(parent);

        return true;
    }

    private static void Rebalance(
        InternalNode parent,
        int childIndex)
    {
        Node child = parent.Children[childIndex];

        if (child is LeafNode leaf)
        {
            if (leaf.Keys.Count < MinLeafKeys)
                RebalanceLeaf(parent, childIndex, leaf);

            return;
        }

        var internalChild = (InternalNode)child;

        if (internalChild.Children.Count < MinInternalChildren)
            RebalanceInternal(parent, childIndex, internalChild);
    }

    private static void RebalanceLeaf(
        InternalNode parent,
        int index,
        LeafNode leaf)
    {
        if (index > 0)
        {
            var left = (LeafNode)parent.Children[index - 1];

            if (left.Keys.Count > MinLeafKeys)
            {
                int last = left.Keys.Count - 1;

                leaf.Keys.Insert(0, left.Keys[last]);
                leaf.Offsets.Insert(0, left.Offsets[last]);

                left.Keys.RemoveAt(last);
                left.Offsets.RemoveAt(last);

                return;
            }
        }

        if (index + 1 < parent.Children.Count)
        {
            var right = (LeafNode)parent.Children[index + 1];

            if (right.Keys.Count > MinLeafKeys)
            {
                leaf.Keys.Add(right.Keys[0]);
                leaf.Offsets.Add(right.Offsets[0]);

                right.Keys.RemoveAt(0);
                right.Offsets.RemoveAt(0);

                return;
            }
        }

        if (index > 0)
        {
            var left = (LeafNode)parent.Children[index - 1];

            left.Keys.AddRange(leaf.Keys);
            left.Offsets.AddRange(leaf.Offsets);
            left.Next = leaf.Next;

            parent.Children.RemoveAt(index);
        }
        else
        {
            var right = (LeafNode)parent.Children[index + 1];

            leaf.Keys.AddRange(right.Keys);
            leaf.Offsets.AddRange(right.Offsets);
            leaf.Next = right.Next;

            parent.Children.RemoveAt(index + 1);
        }
    }

    private static void RebalanceInternal(
        InternalNode parent,
        int index,
        InternalNode child)
    {
        if (index > 0)
        {
            var left = (InternalNode)parent.Children[index - 1];

            if (left.Children.Count > MinInternalChildren)
            {
                int last = left.Children.Count - 1;

                child.Children.Insert(0, left.Children[last]);
                left.Children.RemoveAt(last);

                RefreshSeparators(left);
                RefreshSeparators(child);

                return;
            }
        }

        if (index + 1 < parent.Children.Count)
        {
            var right = (InternalNode)parent.Children[index + 1];

            if (right.Children.Count > MinInternalChildren)
            {
                child.Children.Add(right.Children[0]);
                right.Children.RemoveAt(0);

                RefreshSeparators(child);
                RefreshSeparators(right);

                return;
            }
        }

        if (index > 0)
        {
            var left = (InternalNode)parent.Children[index - 1];

            left.Children.AddRange(child.Children);
            RefreshSeparators(left);

            parent.Children.RemoveAt(index);
        }
        else
        {
            var right = (InternalNode)parent.Children[index + 1];

            child.Children.AddRange(right.Children);
            RefreshSeparators(child);

            parent.Children.RemoveAt(index + 1);
        }
    }

    private static void RefreshSeparators(InternalNode node)
    {
        node.Keys.Clear();

        for (int i = 1; i < node.Children.Count; i++)
        {
            node.Keys.Add(FindMinimumKey(node.Children[i]));
        }
    }

    private static IndexKey FindMinimumKey(Node node)
    {
        while (node is InternalNode internalNode)
            node = internalNode.Children[0];

        return ((LeafNode)node).Keys[0];
    }
}
