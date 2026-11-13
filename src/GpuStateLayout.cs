using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GRAL_2001
{
    /// <summary>
    /// Host copy of the kernel's global state struct <c>P</c>. The layout follows C rules: the non-constant manifest fields
    /// in manifest order, then the host-only fields; scalars use their natural size and alignment, and an
    /// <c>Arr&lt;T,n&gt;</c> is <c>{ uint64 p; int d[n]; }</c> aligned to 8.
    /// </summary>
    internal sealed class GpuStateLayout
    {
        internal sealed class Field
        {
            internal string Name;
            internal string Cpp;        // C type, e.g. "int" or "Arr<float,2>"
            internal string Element;    // element C type for arrays
            internal int Rank;          // 0 for scalars
            internal int Offset;
            internal int Size;
            internal int Alignment;
        }

        private static readonly Dictionary<string, (int Size, string Dtype)> Scalars = new Dictionary<string, (int, string)>
        {
            {"bool",(1,"Boolean")},{"unsigned char",(1,"Byte")},{"short",(2,"Int16")},{"int",(4,"Int32")},{"unsigned int",(4,"UInt32")},
            {"long long",(8,"Int64")},{"unsigned long long",(8,"UInt64")},{"float",(4,"Single")},{"double",(8,"Double")}
        };
        private static readonly Regex ArrayType = new Regex(@"^Arr<(.+),(\d+)>$");

        private static readonly (string Name, string Cpp)[] HostFields =
        {
            ("RnGSeed1","unsigned int"),("RnGSeed2","unsigned int"),
            ("GridRecStart","Arr<int,1>"),("GridRecList","Arr<int,1>"),("GridRecRow","int"),
            ("TraceEnabled","bool"),("Trace","Arr<double,2>"),("TraceIds","Arr<int,1>"),("StepTrace","Arr<double,3>"),
            ("ResidualTrace","Arr<double,2>"),("ParticleReceptorScratch","Arr<double,2>"),("ParticleSliceScratch","Arr<int,2>"),
            ("ParticleWindowFirst","int"),("DeviceError","int"),("DeviceErrorPhase","int"),("DeviceErrorParticle","int")
        };

        internal readonly List<Field> Fields = new List<Field>();
        internal readonly Dictionary<string, Field> ByName = new Dictionary<string, Field>(StringComparer.Ordinal);
        internal int Size { get; }
        internal byte[] Data { get; }

        internal static string DtypeOf(string cpp) => Scalars[cpp].Dtype;
        internal static int SizeOfDtype(string dtype) => dtype switch
        {
            "Boolean" => 1, "Byte" => 1, "Int16" => 2, "Int32" => 4, "UInt32" => 4, "Int64" => 8, "UInt64" => 8, "Single" => 4, "Double" => 8,
            _ => throw new ArgumentException("Unknown dtype " + dtype)
        };

        /// <summary>Rank and element C type of an <c>Arr&lt;T,n&gt;</c> type, or null for a scalar.</summary>
        internal static (string Element, int Rank)? ArrayInfo(string cpp)
        {
            var match = ArrayType.Match(cpp);
            if (!match.Success) return null;
            return (match.Groups[1].Value, int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }

        internal GpuStateLayout(JsonElement manifest)
        {
            int offset = 0, maxAlignment = 1;
            void Add(string name, string cpp)
            {
                var field = new Field { Name = name, Cpp = cpp };
                var array = ArrayInfo(cpp);
                if (array != null)
                {
                    if (!Scalars.ContainsKey(array.Value.Element)) throw new InvalidOperationException("Unsupported state type: " + cpp);
                    field.Element = array.Value.Element;
                    field.Rank = array.Value.Rank;
                    field.Alignment = 8;
                    field.Size = Align(8 + 4 * field.Rank, 8);
                }
                else
                {
                    if (!Scalars.TryGetValue(cpp, out var scalar)) throw new InvalidOperationException("Unsupported state type: " + cpp);
                    field.Size = scalar.Size;
                    field.Alignment = scalar.Size;
                }
                offset = Align(offset, field.Alignment);
                field.Offset = offset;
                offset += field.Size;
                maxAlignment = Math.Max(maxAlignment, field.Alignment);
                Fields.Add(field);
                ByName.Add(name, field);
            }
            foreach (JsonElement f in manifest.GetProperty("fields").EnumerateArray())
                if (!f.GetProperty("constant").GetBoolean()) Add(f.GetProperty("name").GetString(), f.GetProperty("cpp").GetString());
            foreach (var (name, cpp) in HostFields) Add(name, cpp);
            Size = Align(offset, maxAlignment);
            Data = new byte[Size];
        }

        private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;

        internal bool Has(string name) => ByName.ContainsKey(name);
        internal Field Get(string name) => ByName.TryGetValue(name, out var f) ? f : throw new KeyNotFoundException("State field " + name);

        internal ulong GetPointer(string name) => BinaryPrimitives.ReadUInt64LittleEndian(Data.AsSpan(ArrayField(name).Offset));
        internal void SetPointer(string name, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(Data.AsSpan(ArrayField(name).Offset), value);

        internal int GetDim(string name, int index)
        {
            var f = ArrayField(name);
            if ((uint)index >= (uint)f.Rank) throw new ArgumentOutOfRangeException(nameof(index));
            return BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(f.Offset + 8 + 4 * index));
        }

        internal int[] GetDims(string name)
        {
            var f = ArrayField(name);
            var dims = new int[f.Rank];
            for (int i = 0; i < f.Rank; i++) dims[i] = GetDim(name, i);
            return dims;
        }

        internal void SetDims(string name, IReadOnlyList<int> dims)
        {
            var f = ArrayField(name);
            if (dims.Count != f.Rank) throw new ArgumentException("Array rank mismatch: " + name);
            for (int i = 0; i < f.Rank; i++) BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(f.Offset + 8 + 4 * i), dims[i]);
        }

        private Field ArrayField(string name)
        {
            var f = Get(name);
            if (f.Rank == 0) throw new InvalidOperationException("State field is not an array: " + name);
            return f;
        }

        private Field ScalarField(string name)
        {
            var f = Get(name);
            if (f.Rank != 0) throw new InvalidOperationException("State field is not a scalar: " + name);
            return f;
        }

        internal int GetInt(string name)
        {
            var f = ScalarField(name);
            return f.Cpp switch
            {
                "int" => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(f.Offset)),
                "short" => BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan(f.Offset)),
                "unsigned char" => Data[f.Offset],
                "bool" => Data[f.Offset],
                _ => throw new InvalidOperationException("Not an int field: " + name)
            };
        }

        internal uint GetUInt(string name) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(ScalarField(name).Offset));
        internal bool GetBool(string name) => Data[ScalarField(name).Offset] != 0;
        internal double GetReal(string name)
        {
            var f = ScalarField(name);
            return f.Cpp switch
            {
                "float" => BinaryPrimitives.ReadSingleLittleEndian(Data.AsSpan(f.Offset)),
                "double" => BinaryPrimitives.ReadDoubleLittleEndian(Data.AsSpan(f.Offset)),
                _ => throw new InvalidOperationException("Not a real field: " + name)
            };
        }

        internal void SetInt(string name, int value)
        {
            var f = ScalarField(name);
            if (f.Cpp != "int") throw new InvalidOperationException("Not an int field: " + name);
            BinaryPrimitives.WriteInt32LittleEndian(Data.AsSpan(f.Offset), value);
        }

        /// <summary>
        /// Checks a JSON scalar: bool fields take JSON true/false only, real fields take a
        /// finite number that stays finite in the field type, integer fields take an integer literal that fits exactly.
        /// </summary>
        internal bool ValidScalar(string name, JsonElement value)
        {
            var f = ScalarField(name);
            if (f.Cpp == "bool") return value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
            if (value.ValueKind != JsonValueKind.Number) return false;
            if (f.Cpp == "float" || f.Cpp == "double")
            {
                if (!value.TryGetDouble(out double d) || !double.IsFinite(d)) return false;
                return f.Cpp == "double" || float.IsFinite((float)d);
            }
            string raw = value.GetRawText();
            if (raw.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0) return false;
            if (!System.Numerics.BigInteger.TryParse(raw, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var big)) return false;
            return f.Cpp switch
            {
                "unsigned char" => big >= byte.MinValue && big <= byte.MaxValue,
                "short" => big >= short.MinValue && big <= short.MaxValue,
                "int" => big >= int.MinValue && big <= int.MaxValue,
                "unsigned int" => big >= uint.MinValue && big <= uint.MaxValue,
                "long long" => big >= long.MinValue && big <= long.MaxValue,
                "unsigned long long" => big >= ulong.MinValue && big <= ulong.MaxValue,
                _ => false
            };
        }

        internal void SetScalar(string name, JsonElement value)
        {
            var f = ScalarField(name);
            var span = Data.AsSpan(f.Offset);
            switch (f.Cpp)
            {
                case "bool": span[0] = value.GetBoolean() ? (byte)1 : (byte)0; break;
                case "unsigned char": span[0] = value.GetByte(); break;
                case "short": BinaryPrimitives.WriteInt16LittleEndian(span, value.GetInt16()); break;
                case "int": BinaryPrimitives.WriteInt32LittleEndian(span, value.GetInt32()); break;
                case "unsigned int": BinaryPrimitives.WriteUInt32LittleEndian(span, value.GetUInt32()); break;
                case "long long": BinaryPrimitives.WriteInt64LittleEndian(span, value.GetInt64()); break;
                case "unsigned long long": BinaryPrimitives.WriteUInt64LittleEndian(span, value.GetUInt64()); break;
                case "float": BinaryPrimitives.WriteSingleLittleEndian(span, (float)value.GetDouble()); break;
                case "double": BinaryPrimitives.WriteDoubleLittleEndian(span, value.GetDouble()); break;
                default: throw new InvalidOperationException("Unsupported scalar type " + f.Cpp);
            }
        }

        /// <summary>The state_layout.json diagnostic: total size, and offset and size of every State field.</summary>
        internal string LayoutJson()
        {
            var fields = new Dictionary<string, object>();
            foreach (var f in Fields) fields[f.Name] = new Dictionary<string, int> { { "offset", f.Offset }, { "bytes", f.Size } };
            return GpuJson.Text(new Dictionary<string, object> { { "bytes", Size }, { "fields", fields } }, new JsonWriterOptions { Indented = true });
        }
    }
}
