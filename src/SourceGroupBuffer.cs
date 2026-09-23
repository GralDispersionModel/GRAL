// Lazily allocated source-group concentration blocks. GPL-3.0, same license as GRAL.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GRAL_2001
{
    // Writers must hold the cell lock, as with the former float[]/double[] cells.
    // Reads, output, clearing and checkpoint I/O keep the existing phase barriers.
    public readonly struct SourceGroupBuffer<T> where T : unmanaged
    {
        private readonly object storage;
        public object SyncRoot => storage;
        public int Length => storage is T[] values ? values.Length : ((SparseCell)storage).Length;

        public SourceGroupBuffer(int length)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            storage = length <= 100 ? new T[length] : new SparseCell(length);
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => storage is T[] values ? values[index] : ((SparseCell)storage)[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (storage is T[] values) values[index] = value;
                else ((SparseCell)storage)[index] = value;
            }
        }

        public void Clear()
        {
            if (storage is T[] values) Array.Clear(values);
            else ((SparseCell)storage).Clear();
        }

        private sealed class SparseCell
        {
            private const int BlockShift = 5;
            private const int BlockSize = 1 << BlockShift;
            // With no block directory, values holds one block (state = its slot)
            // or a dense array (state = -1). With a directory, state is its block count.
            // Reuse the existing fields so untouched cells do not gain an extra object or reference.
            private T[] values;
            private T[][] blocks;
            private int state = -1;
            public int Length { get; }

            public SparseCell(int length) { Length = length; }

            public T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if ((uint)index >= (uint)Length) throw new IndexOutOfRangeException();
                    if (values != null)
                        return state < 0 ? values[index] :
                            (state == index >> BlockShift ? values[index & (BlockSize - 1)] : default);
                    T[] block = blocks == null ? null : blocks[index >> BlockShift];
                    return block == null ? default : block[index & (BlockSize - 1)];
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    if ((uint)index >= (uint)Length) throw new IndexOutOfRangeException();
                    if (values != null && state < 0) { values[index] = value; return; }
                    int slot = index >> BlockShift;
                    if (blocks == null)
                    {
                        if (values == null)
                        {
                            if (EqualityComparer<T>.Default.Equals(value, default)) return;
                            values = new T[Math.Min(BlockSize, Length - slot * BlockSize)];
                            state = slot;
                        }
                        else if (state != slot)
                        {
                            if (EqualityComparer<T>.Default.Equals(value, default)) return;
                            blocks = new T[(Length + BlockSize - 1) >> BlockShift][];
                            blocks[state] = values;
                            values = null;
                            state = 1;
                        }
                        if (blocks == null) { values[index & (BlockSize - 1)] = value; return; }
                    }
                    T[] block = blocks[slot];
                    if (block == null)
                    {
                        if (EqualityComparer<T>.Default.Equals(value, default)) return;
                        block = new T[Math.Min(BlockSize, Length - slot * BlockSize)];
                        blocks[slot] = block;
                        state++;
                    }
                    block[index & (BlockSize - 1)] = value;
                    if (state * BlockSize >= Length * 3 / 4) MakeDense();
                }
            }

            private void MakeDense()
            {
                var dense = new T[Length];
                for (int i = 0; i < blocks.Length; i++)
                    if (blocks[i] != null) Array.Copy(blocks[i], 0, dense, i * BlockSize, blocks[i].Length);
                values = dense;
                blocks = null;
                state = -1;
            }

            public void Clear() { values = null; blocks = null; state = -1; }
        }
    }
}
