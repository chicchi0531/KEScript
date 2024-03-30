using System.Text;

namespace KESCompiler.Runtime
{
    public class ProgramData
    {
        public string Signature { get; init; }
        public byte MajorVersion { get; init; }
        public byte MinorVersion { get; init; }
        public int EntryPoint { get; init; }
        
        public Operation[] Program { get; init; }
        public string[] StringBuffer { get; init; }
        public ClassData[] ClassTable { get; init; }
        public int GlobalVariableSize { get; init; }
        
        public static ProgramData Deserialize(byte[] data)
        {
            var view = new ReadOnlySpan<byte>(data);
            
            // header
            //  0..2     3byte : signature 'KES'
            //  3        1byte : major version
            //  4        1byte : minor version
            //  5..8     4byte : program chunk size
            //  9..12    4byte : string buffer chunk size
            //  13..16   4byte : class table chunk size
            //  17..20   4byte : entry point
            //  21..63   header reserved
            var signature = Encoding.ASCII.GetString(data[..3]);
            var majorVersion = view[3];
            var minorVersion = view[4];
            var programChunkSize = BitConverter.ToInt32(view.Slice(5,4));
            var strBufChunkSize = BitConverter.ToInt32(view.Slice(9,4));
            var classBufChunkSize = BitConverter.ToInt32(view.Slice(13,4));
            var entryPoint = BitConverter.ToInt32(view.Slice(17,4));

            // Program buffer
            //  64..     <program length>byte: program
            if (programChunkSize % 16 != 0)
            {
                throw new FormatException("Program size must be a multiple of 16.");
            }
            var program = new List<Operation>();
            var programChunkStart = 64;
            var programBytes = view.Slice(programChunkStart, programChunkSize);
            for (int i = 0; i < programChunkSize; i += 16)
            {
                program.Add(Operation.Deserialize(programBytes.Slice(i, 16)));
            }

            // String buffer chunk
            //  (64 + <program chunk size>)..  <string buffer chunk size>byte: string buffer
            var strBufStart = programChunkStart + programChunkSize;
            var strBufBytes = view.Slice(strBufStart, strBufChunkSize).ToArray();
            var buffer = new List<byte>();
            List <string> stringBuffer = new();
            foreach(var b in strBufBytes)
            {
                // ゼロ文字で区切る
                if (b == 0)
                {
                    stringBuffer.Add(Encoding.UTF8.GetString(buffer.ToArray()));
                    buffer.Clear();
                }
                else
                {
                    buffer.Add(b);
                }
            }

            // Class table
            // 4byte:                               class chunk count
            // 4 + SUM(<class chunk size>) byte:    class chunks
            var classBufBeginPos = strBufStart + strBufChunkSize;
            var classBufBytes = view.Slice(classBufBeginPos, classBufChunkSize);
            var classNum = BitConverter.ToInt32(classBufBytes[..4]);
            var classTable = new List<ClassData>();
            int sliceOffset = 4;
            for (int i = 0; i < classNum; i++)
            {
                // 4byte :                 class data size
                // <class data size> byte: class data
                var classBufSize = BitConverter.ToInt32(classBufBytes.Slice(sliceOffset, 4));
                var classBufSlice = classBufBytes.Slice(sliceOffset + 4, classBufSize);
                classTable.Add(ClassData.Deserialize(classBufSlice.ToArray()));
                sliceOffset += 4 + classBufSize;
            }
            
            // Global variable size
            var globalVariableSize = BitConverter.ToInt32(classBufBytes.Slice(sliceOffset, 4));

            return new ProgramData()
            {
                Signature = signature,
                MajorVersion = majorVersion,
                MinorVersion = minorVersion,
                EntryPoint = entryPoint,
                Program = program.ToArray(),
                StringBuffer = stringBuffer.ToArray(),
                ClassTable = classTable.ToArray(),
                GlobalVariableSize = globalVariableSize
            };
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Signature: {Signature}");
            stringBuilder.AppendLine($"MajorVersion: {MajorVersion}");
            stringBuilder.AppendLine($"MinorVersion: {MinorVersion}");
            stringBuilder.AppendLine($"EntryPoint: {EntryPoint}");
            stringBuilder.AppendLine("\nProgram:");
            foreach (var op in Program)
            {
                stringBuilder.AppendLine($"  {op}");
            }
            stringBuilder.AppendLine("\nStringBuffer:");
            foreach (var str in StringBuffer)
            {
                stringBuilder.AppendLine($"  {str}");
            }
            stringBuilder.AppendLine("\nClassTable:");
            foreach (var cls in ClassTable)
            {
                stringBuilder.AppendLine($"  {cls}");
            }
            stringBuilder.AppendLine($"GlobalVariableSize: {GlobalVariableSize}");
            return stringBuilder.ToString();
        }
    }
    
}
