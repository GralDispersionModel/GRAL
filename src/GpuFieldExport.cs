using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GRAL_2001
{
    // Experimental export only. Does not enable or dispatch a GPU model.
    internal static class GpuFieldExport
    {
        private static Type ElementType(Type type)
        {
            while (type.IsArray) type = type.GetElementType();
            return type;
        }

        private static int Rank(Type type)
        {
            int result = 0;
            while (type.IsArray) { result += type.GetArrayRank(); type = type.GetElementType(); }
            return result;
        }

        private static int Size(Type type)
        {
            if (type == typeof(byte) || type == typeof(bool)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return 4;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
            throw new NotSupportedException("Unsupported field type " + type);
        }

        private static void Measure(object value, List<int> shape, int depth)
        {
            if (value == null) return;
            if (value is Array a)
            {
                for (int i = 0; i < a.Rank; i++)
                {
                    if (shape.Count <= depth+i) shape.Add(0);
                    shape[depth+i] = Math.Max(shape[depth+i], a.GetLength(i));
                }
                if (a.GetType().GetElementType().IsArray)
                    foreach (object child in a) Measure(child, shape, depth+a.Rank);
                return;
            }
        }

        // Conz5d init only: measure the complete jagged envelope even after a nonzero bit.
        private static bool MeasureConz5d(float[][][][] value, List<int> shape)
        {
            bool allPositiveZero = true;
            UpdateExtent(shape, 0, value.Length);
            foreach (float[][][] a in value)
            {
                if (a == null) continue;
                UpdateExtent(shape, 1, a.Length);
                foreach (float[][] b in a)
                {
                    if (b == null) continue;
                    UpdateExtent(shape, 2, b.Length);
                    foreach (float[] c in b)
                    {
                        if (c == null) continue;
                        UpdateExtent(shape, 3, c.Length);
                        if (allPositiveZero)
                            foreach (float element in c)
                                if (BitConverter.SingleToInt32Bits(element) != 0) { allPositiveZero = false; break; }
                    }
                }
            }
            return allPositiveZero;
        }

        private static void UpdateExtent(List<int> shape, int depth, int length)
        {
            if (shape.Count <= depth) shape.Add(0);
            shape[depth] = Math.Max(shape[depth], length);
        }
        private static object At(object node, int[] indices, ref int depth)
        {
            if (node == null) return null;
            if (node is Array a)
            {
                int[] current = new int[a.Rank];
                for (int i=0;i<a.Rank;i++)
                {
                    int index=indices[depth++];
                    if (index>=a.GetLength(i)) return null;
                    current[i]=index;
                }
                return At(a.GetValue(current),indices,ref depth);
            }
            return node;
        }

        private static void WritePrimitive(BinaryWriter w,Type t,object value)
        {
            if(value==null) value=Activator.CreateInstance(t);
            if(t==typeof(byte)) w.Write((byte)value);
            else if(t==typeof(bool)) w.Write((byte)((bool)value?1:0));
            else if(t==typeof(short)) w.Write((short)value);
            else if(t==typeof(ushort)) w.Write((ushort)value);
            else if(t==typeof(int)) w.Write((int)value);
            else if(t==typeof(uint)) w.Write((uint)value);
            else if(t==typeof(float)) w.Write((float)value);
            else if(t==typeof(long)) w.Write((long)value);
            else if(t==typeof(ulong)) w.Write((ulong)value);
            else if(t==typeof(double)) w.Write((double)value);
            else throw new NotSupportedException(t.ToString());
        }

        internal const int ChunkSize = 1024 * 1024;

        internal static bool TryPrimitiveBytes(Array array, out Span<byte> bytes)
        {
            switch (array)
            {
                case byte[] a: bytes = a.AsSpan(); return true;
                case bool[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case short[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case ushort[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case int[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case uint[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case long[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case ulong[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case float[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                case double[] a: bytes = MemoryMarshal.AsBytes(a.AsSpan()); return true;
                default: bytes = default; return false;
            }
        }

        private static void WriteZeros(BinaryWriter writer, long bytes, byte[] zero)
        {
            if(bytes<=0)return;
            Array.Clear(zero,0,(int)Math.Min(bytes,zero.Length));
            while (bytes > 0) { int n = (int)Math.Min(bytes, zero.Length); writer.Write(zero,0,n); bytes-=n; }
        }
        private static void WriteZerosLazy(BinaryWriter writer, long bytes, ref byte[] buffer)
        {
            if(bytes<=0)return;
            WriteZeros(writer,bytes,buffer ??= new byte[ChunkSize]);
        }
        private static long SuffixBytes(List<int> shape, int depth, int elementSize)
        {
            long n=elementSize;
            for (int i=depth;i<shape.Count;i++) n=checked(n*shape[i]);
            return n;
        }
        private static void WriteNested(BinaryWriter w, object node, List<int> shape, int depth, Type elem, ref byte[] buffer)
        {
            if (node==null) { WriteZerosLazy(w,SuffixBytes(shape,depth,Size(elem)),ref buffer); return; }
            Array a=(Array)node; Type et=a.GetType().GetElementType();
            if (et.IsPrimitive && a.Rank==1)
            {
                int total=Buffer.ByteLength(a);
                if (TryPrimitiveBytes(a, out Span<byte> bytes))
                    for(int pos=0;pos<total;) { int n=Math.Min(ChunkSize,total-pos); w.Write(bytes.Slice(pos,n)); pos+=n; }
                else if (total>0)
                {
                    byte[] scratch=buffer ??= new byte[ChunkSize];
                    for(int pos=0;pos<total;) { int n=Math.Min(scratch.Length,total-pos); Buffer.BlockCopy(a,pos,scratch,0,n); w.Write(scratch,0,n); pos+=n; }
                }
                WriteZerosLazy(w,checked((long)(shape[depth]-a.Length)*Size(elem)),ref buffer);return;
            }
            if (et.IsPrimitive && a.Rank>1)
            {
                bool same=true;for(int i=0;i<a.Rank;i++)same &= a.GetLength(i)==shape[depth+i];
                if(!same) throw new NotSupportedException("Ragged multidimensional arrays require explicit layout");
                int total=Buffer.ByteLength(a);
                if (total>0)
                {
                    byte[] scratch=buffer ??= new byte[ChunkSize];
                    for(int pos=0;pos<total;) { int n=Math.Min(scratch.Length,total-pos); Buffer.BlockCopy(a,pos,scratch,0,n); w.Write(scratch,0,n); pos+=n; }
                }
                return;
            }
            if(a.Rank!=1) throw new NotSupportedException("Non-primitive rectangular array");
            for(int i=0;i<shape[depth];i++) WriteNested(w,i<a.Length?(a is object[] children?children[i]:a.GetValue(i)):null,shape,depth+1,elem,ref buffer);
        }

        // Null branches and ragged padding are serialized as positive zero.
        private static bool AllZero(object node, List<int> shape, int depth)
        {
            if (node == null) return true;
            Array a = (Array)node;
            Type et = a.GetType().GetElementType();
            if (et.IsPrimitive)
            {
                if (a.Rank > 1)
                    for (int i = 0; i < a.Rank; i++)
                        if (a.GetLength(i) != shape[depth + i])
                            throw new NotSupportedException("Ragged multidimensional arrays require explicit layout");
                if (TryPrimitiveBytes(a, out Span<byte> bytes))
                {
                    foreach (byte b in bytes) if (b != 0) return false;
                    return true;
                }
                byte[] scratch = new byte[ChunkSize];
                int total = Buffer.ByteLength(a);
                for (int pos = 0; pos < total;)
                {
                    int n = Math.Min(scratch.Length, total - pos);
                    Buffer.BlockCopy(a, pos, scratch, 0, n);
                    for (int i = 0; i < n; i++) if (scratch[i] != 0) return false;
                    pos += n;
                }
                return true;
            }
            if (a.Rank != 1) throw new NotSupportedException("Non-primitive rectangular array");
            foreach (object child in a) if (!AllZero(child, shape, depth + 1)) return false;
            return true;
        }

        private sealed class ArrayRecord
        {
            internal string Name; internal Type Element; internal object Value;
            internal List<int> Shape; internal long Offset, Bytes, Count; internal bool ZeroFill;
        }

        internal static long Export(Stream output, IEnumerable<string> names, string command = "init", string phase = null, int? first = null, int? itemCount = null, IReadOnlyDictionary<string, object> extra = null, int? chunk = null, int? block = null)
        {
            var scalars = new Dictionary<string, object>();
            var records = new List<ArrayRecord>();
            long payloadBytes = 0;
            var allNames = new List<string>(names);
            if (extra != null) foreach (string key in extra.Keys) { if (allNames.Contains(key)) throw new InvalidOperationException("Duplicate field " + key); allNames.Add(key); }
            foreach (string name in allNames)
            {
                object value; Type fieldType;
                if (extra != null && extra.TryGetValue(name, out value)) { if (value==null) throw new InvalidOperationException("Untyped synthetic null " + name); fieldType=value.GetType(); }
                else { FieldInfo f = typeof(Program).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static); if (f == null) throw new InvalidOperationException("Missing field " + name); value=f.GetValue(null); fieldType=f.FieldType; }
                Type element = ElementType(fieldType);
                if (!fieldType.IsArray) { Size(element); scalars.Add(name, value); continue; }
                Size(element); var shape = new List<int>();
                bool typedConz5d = command == "init" && name == "Conz5d" && value is float[][][][];
                bool typedAllZero = false;
                if (typedConz5d) typedAllZero = MeasureConz5d((float[][][][])value, shape);
                else Measure(value, shape, 0);
                if (value != null && shape.Count != Rank(fieldType)) throw new InvalidOperationException("Incomplete shape; explicit dimensions required for " + name);
                long count = value == null ? 0 : 1;
                foreach (int n in shape) count = checked(count * n);
                long bytes = checked(count * Size(element));
                bool zeroFill = command == "init" && name == "Conz5d" && element == typeof(float) && value != null && bytes > 0 && (typedConz5d ? typedAllZero : AllZero(value, shape, 0));
                records.Add(new ArrayRecord { Name = name, Element = element, Value = value, Shape = shape, Offset = payloadBytes, Bytes = bytes, Count = count, ZeroFill = zeroFill });
                payloadBytes = checked(payloadBytes + (zeroFill ? 0 : bytes));
            }
            var arrays = new List<object>();
            foreach (var r in records)
                if (r.ZeroFill) arrays.Add(new { name = r.Name, dtype = r.Element.Name, shape = r.Shape, offset = r.Offset, bytes = r.Bytes, is_null = false, zero_fill = true });
                else arrays.Add(new { name = r.Name, dtype = r.Element.Name, shape = r.Shape, offset = r.Offset, bytes = r.Bytes, is_null = r.Value == null });
            byte[] header = JsonSerializer.SerializeToUtf8Bytes(new { command, phase, first, count = itemCount, chunk, block, scalars, arrays, payload_bytes = payloadBytes });
            var frame = new BinaryWriter(output, System.Text.Encoding.UTF8, true);
            frame.Write((ulong)header.Length); frame.Write(header);
            byte[] buffer = null;
            foreach (var r in records)
                if (r.Value != null && !r.ZeroFill) WriteNested(frame, r.Value, r.Shape, 0, r.Element, ref buffer);
            output.Flush();
            return checked(8L + header.Length + payloadBytes);
        }
    }
}

