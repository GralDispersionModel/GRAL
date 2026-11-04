using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
namespace GRAL_2001
{
    // Preserve the host allocation topology: reject nonzero data into absent jagged cells.
    internal static class GpuFieldImport
    {
        private static void ReadZeros(BinaryReader r,long count,string dtype,int size,ref byte[] buffer,string error)
        {
            bool floating=dtype switch {"Single"=>true,"Double"=>true,"Int32"=>false,"Int64"=>false,_=>throw new NotSupportedException(dtype)};
            if(count==0)return;
            byte[] scratch=buffer ??= new byte[GpuFieldExport.ChunkSize];
            while(count>0)
            {
                int n=(int)Math.Min(count,scratch.Length/size), length=n*size;
                r.BaseStream.ReadExactly(scratch.AsSpan(0,length));
                for(int i=0;i<length;i+=size)
                {
                    ulong bits=size==4?BinaryPrimitives.ReadUInt32LittleEndian(scratch.AsSpan(i,4)):BinaryPrimitives.ReadUInt64LittleEndian(scratch.AsSpan(i,8));
                    ulong magnitude=floating?(size==4?bits & 0x7fffffffUL:bits & 0x7fffffffffffffffUL):bits;
                    if(magnitude!=0)throw new InvalidDataException(error);
                }
                count-=n;
            }
        }
        private static long Elements(List<int> dims,int depth)
        {long n=1;for(int i=depth;i<dims.Count;i++)n=checked(n*dims[i]);return n;}
        private static void ReadNested(BinaryReader r,object node,List<int> dims,int depth,string dtype,int size,ref byte[] buffer)
        {
            if(node==null)
            {
                long n=Elements(dims,depth);
                ReadZeros(r,n,dtype,size,ref buffer,"Nonzero result in unallocated cell");
                return;
            }
            Array a=(Array)node; Type elem=a.GetType().GetElementType();
            if(elem.IsPrimitive)
            {
                if(elem.Name!=dtype)throw new InvalidDataException("Result dtype mismatch");
                if(a.Rank>1){for(int i=0;i<a.Rank;i++)if(a.GetLength(i)!=dims[depth+i])throw new InvalidDataException("Rectangular shape mismatch");}
                else if(a.Length>dims[depth])throw new InvalidDataException("Result dimension smaller than target");
                int total=Buffer.ByteLength(a);
                if(GpuFieldExport.TryPrimitiveBytes(a,out Span<byte> bytes))
                    for(int pos=0;pos<total;){int n=Math.Min(GpuFieldExport.ChunkSize,total-pos);r.BaseStream.ReadExactly(bytes.Slice(pos,n));pos+=n;}
                else if(total>0)
                {
                    byte[] scratch=buffer ??= new byte[GpuFieldExport.ChunkSize];
                    for(int pos=0;pos<total;){int n=Math.Min(scratch.Length,total-pos);r.BaseStream.ReadExactly(scratch.AsSpan(0,n));Buffer.BlockCopy(scratch,0,a,pos,n);pos+=n;}
                }
                if(a.Rank==1) ReadZeros(r,dims[depth]-a.Length,dtype,size,ref buffer,"Nonzero array padding");
                return;
            }
            if(a.Rank!=1 || a.Length>dims[depth])throw new InvalidDataException("Jagged shape mismatch");
            for(int i=0;i<dims[depth];i++) ReadNested(r,i<a.Length?(a is object[] children?children[i]:a.GetValue(i)):null,dims,depth+1,dtype,size,ref buffer);
        }
        // A complete [k, source-group] row is read before scattering. Errors are fatal;
        // on malformed or truncated input the stream can advance farther than ReadNested.
        private static void ReadConz5d(BinaryReader r,float[][][][] target,List<int> dims,int rowBytes,ref byte[] buffer)
        {
            if(target.Length>dims[0])throw new InvalidDataException("Jagged shape mismatch");
            for(int i=0;i<dims[0];i++)
            {
                float[][][] a=i<target.Length?target[i]:null;
                if(a==null){ReadZeros(r,Elements(dims,1),"Single",4,ref buffer,"Nonzero result in unallocated cell");continue;}
                if(a.Length>dims[1])throw new InvalidDataException("Jagged shape mismatch");
                for(int j=0;j<dims[1];j++)
                {
                    float[][] b=j<a.Length?a[j]:null;
                    if(b==null){ReadZeros(r,Elements(dims,2),"Single",4,ref buffer,"Nonzero result in unallocated cell");continue;}
                    if(b.Length>dims[2])throw new InvalidDataException("Jagged shape mismatch");
                    byte[] row=rowBytes==0?Array.Empty<byte>():buffer ??= new byte[GpuFieldExport.ChunkSize];
                    r.BaseStream.ReadExactly(row.AsSpan(0,rowBytes));
                    int stride=dims[2]==0?0:checked(dims[3]*4);
                    for(int k=0;k<dims[2];k++)
                    {
                        float[] leaf=k<b.Length?b[k]:null;
                        int start=k*stride;
                        if(leaf==null){CheckFloatZeros(row.AsSpan(start,stride),"Nonzero result in unallocated cell");continue;}
                        if(leaf.Length>dims[3])throw new InvalidDataException("Result dimension smaller than target");
                        int bytes=leaf.Length*4;
                        Buffer.BlockCopy(row,start,leaf,0,bytes);
                        CheckFloatZeros(row.AsSpan(start+bytes,stride-bytes),"Nonzero array padding");
                    }
                }
            }
        }
        private static void CheckFloatZeros(ReadOnlySpan<byte> data,string error)
        {
            for(int i=0;i<data.Length;i+=4)
                if((BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i,4)) & 0x7fffffffU)!=0)throw new InvalidDataException(error);
        }
        internal static long Apply(Stream source,JsonElement header)
        {
            using var r=new BinaryReader(source,System.Text.Encoding.UTF8,true);long bytesRead=0;byte[] buffer=null;
            foreach(var item in header.GetProperty("arrays").EnumerateArray())
            {
                string name=item.GetProperty("name").GetString(), dtype=item.GetProperty("dtype").GetString();
                if(name=="Trace" || name=="StepTrace" || name=="ResidualTrace")
                {
                    string variable=name switch {"Trace"=>"INU_GPU_DEVICE_TRACE","StepTrace"=>"INU_GPU_DEVICE_STEP_TRACE",_=>"INU_GPU_DEVICE_RESIDUAL_TRACE"};
                    string path=Environment.GetEnvironmentVariable(variable) ?? throw new InvalidDataException("Unexpected GPU trace");
                    long len=item.GetProperty("bytes").GetInt64();
                    var shape=item.GetProperty("shape");int rank=name=="StepTrace"?3:2;long elements=1;
                    foreach(var d in shape.EnumerateArray()){int n=d.GetInt32();if(n<0)throw new InvalidDataException("Invalid GPU trace");elements=checked(elements*n);}
                    int columns=name=="Trace"?8:16;
                    if(dtype!="Double" || item.GetProperty("offset").GetInt64()!=bytesRead || len<0 ||
                       shape.GetArrayLength()!=rank || shape[rank-1].GetInt32()!=columns || checked(elements*8)!=len)
                        throw new InvalidDataException("Invalid GPU trace");
                    using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read);long remaining=len;
                    if(remaining>0)
                    {
                        byte[] chunk=buffer ??= new byte[GpuFieldExport.ChunkSize];
                        while(remaining>0){int n=(int)Math.Min(remaining,chunk.Length);source.ReadExactly(chunk.AsSpan(0,n));file.Write(chunk,0,n);remaining-=n;}
                    }
                    bytesRead=checked(bytesRead+len);continue;
                }
                FieldInfo f=typeof(Program).GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static) ?? throw new InvalidDataException("Unknown result field "+name);
                if(!f.FieldType.IsArray) throw new InvalidDataException("Result is not an array: "+name);
                var dims=new List<int>();long count=1;
                foreach(var d in item.GetProperty("shape").EnumerateArray()){int n=d.GetInt32();if(n<0)throw new InvalidDataException("Negative dimension");dims.Add(n);count=checked(count*n);}
                int size=dtype switch {"Single"=>4,"Double"=>8,"Int32"=>4,"Int64"=>8,_=>throw new NotSupportedException(dtype)};
                long bytes=checked(count*size);
                if(item.GetProperty("offset").GetInt64()!=bytesRead || item.GetProperty("bytes").GetInt64()!=bytes)throw new InvalidDataException("Result frame layout mismatch");
                object target=f.GetValue(null);
                // Compare element count before byte multiplication, including zero outer dimensions.
                long rowElements=dims.Count==4?(long)dims[2]*dims[3]:0;
                if(name=="Conz5d" && dtype=="Single" && dims.Count==4 && target is float[][][][] typed && rowElements<=GpuFieldExport.ChunkSize/4)
                    ReadConz5d(r,typed,dims,(int)(rowElements*4),ref buffer);
                else ReadNested(r,target,dims,0,dtype,size,ref buffer);
                bytesRead=checked(bytesRead+bytes);
            }
            if(header.GetProperty("payload_bytes").GetInt64()!=bytesRead)throw new InvalidDataException("Result payload length mismatch");
            if(header.TryGetProperty("scalars",out var scalars))
                foreach(var pair in scalars.EnumerateObject())
                {
                    throw new InvalidDataException("Unexpected result scalar "+pair.Name);
                }
            return bytesRead;
        }
    }
}
