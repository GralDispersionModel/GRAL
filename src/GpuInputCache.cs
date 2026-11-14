#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2026]  [Young-il Park]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace GRAL_2001
{
    // The cache belongs to one server and is advanced only after its init acknowledgement.
    internal sealed class GpuInputCache
    {
        private readonly Dictionary<string,string> committed=new Dictionary<string,string>();
        private readonly bool cacheCoordinates;
        private static readonly HashSet<string> cacheable=new HashSet<string>
        {"ParticleMass","ParticleSG","ParticleSource","SourceType","ParticleMode","ParticleVdep","ParticleVsed",
         "DDX","DDY","HorSlices"};
        private static readonly HashSet<string> coordinates=new HashSet<string>{"Xcoord","YCoord","ZCoord"};

        internal GpuInputCache()
        {
            string setting=Environment.GetEnvironmentVariable("INU_GPU_CACHE_COORDINATES");
            if(setting!=null && setting!="0" && setting!="1")
                throw new InvalidOperationException("Invalid INU_GPU_CACHE_COORDINATES");
            // Situation-dependent RNG usually changes coordinates. Avoid hashing them by default.
            cacheCoordinates=setting=="1";
        }

        private static (string fingerprint,long bytes) Fingerprint(string name)
        {
            var field=typeof(Program).GetField(name);
            object value=field.GetValue(null);
            string descriptor=field.FieldType.FullName+":";
            if(value==null)return (descriptor+"null",0);
            ReadOnlySpan<byte> bytes=value switch
            {
                byte[] a => a,
                int[] a => MemoryMarshal.AsBytes(a.AsSpan()),
                float[] a => MemoryMarshal.AsBytes(a.AsSpan()),
                double[] a => MemoryMarshal.AsBytes(a.AsSpan()),
                _ => throw new InvalidOperationException("Unexpected cache field type " + name)
            };
            return (descriptor+((Array)value).Length+":"+Convert.ToHexString(SHA256.HashData(bytes)),bytes.Length);
        }

        internal Plan Prepare(IEnumerable<string> names)
        {
            var fields=new List<string>();var omitted=new List<string>();var snapshot=new Dictionary<string,string>();long saved=0;
            foreach(string name in names)
            {
                if(!cacheable.Contains(name) && !(cacheCoordinates && coordinates.Contains(name))) { fields.Add(name);continue; }
                var (fingerprint,bytes)=Fingerprint(name);snapshot.Add(name,fingerprint);
                if(committed.TryGetValue(name,out string old) && old==fingerprint) { omitted.Add(name);saved=checked(saved+bytes); }
                else fields.Add(name);
            }
            return new Plan(this,fields,omitted,saved,snapshot);
        }

        internal sealed class Plan
        {
            private readonly GpuInputCache owner;
            private readonly Dictionary<string,string> snapshot;
            internal IReadOnlyList<string> Fields { get; }
            internal IReadOnlyList<string> Omitted { get; }
            internal long SavedPayloadBytes { get; }
            internal Plan(GpuInputCache owner,List<string> fields,List<string> omitted,long saved,Dictionary<string,string> snapshot)
            { this.owner=owner;Fields=fields;Omitted=omitted;SavedPayloadBytes=saved;this.snapshot=snapshot; }
            internal void Commit(JsonElement ack)
            {
                if(ack.GetProperty("payload_bytes").GetInt64()!=0)throw new System.IO.InvalidDataException("Unexpected init payload");
                foreach(var pair in snapshot)owner.committed[pair.Key]=pair.Value;
            }
        }
    }
}
