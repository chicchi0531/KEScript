namespace KESCompiler.Runtime;

public class ClassData(int superClass, MethodData[] methods, FieldData[] fields)
{
    public int SuperClass { get; } = superClass;
    public MethodData[] Methods { get; } = methods;
    public FieldData[] Fields { get; } = fields;

    public static ClassData Deserialize(byte[] data)
    {
        var view = new ReadOnlySpan<byte>(data);
        
        // 4byte : super class
        var superClass = BitConverter.ToInt32(data, 0);
        var offset = 4;
        
        // 4byte : method count
        // loop(<method count>)
        //  4byte : method chunk size
        //  <method chunk size>byte: method chunk
        var methodCount = BitConverter.ToInt32(data, offset);
        offset += 4;
        
        var methods = new MethodData[methodCount];
        for (var i = 0; i < methodCount; i++)
        {
            var methodChunkSize = BitConverter.ToInt32(data, offset);
            offset += 4;
            
            var methodChunk = view.Slice(offset, methodChunkSize);
            offset += methodChunkSize;
            
            methods[i] = MethodData.Deserialize(methodChunk.ToArray());
        }

        // 4byte : field count
        // loop(<field count>)
        //  4byte : field chunk
        var fieldCount = BitConverter.ToInt32(data, offset);
        offset += 4;
        
        var fields = new FieldData[fieldCount];
        for (int i = 0; i < fieldCount; i++,offset += 4)
        {
            fields[i] = FieldData.Deserialize(view[offset]);
        }

        return new ClassData(
            superClass,
            methods,
            fields
        );
    }

    public override string ToString()
    {
        var stringBuilder = new System.Text.StringBuilder();
        stringBuilder.AppendLine($"MethodCount: {Methods.Length}");
        foreach (var method in Methods)
        {
            stringBuilder.AppendLine($"  {method}");
        }
        stringBuilder.AppendLine($"FieldCount: {Fields.Length}");
        foreach (var field in Fields)
        {
            stringBuilder.AppendLine($"  {field}");
        }
        return stringBuilder.ToString();
    }
}

public class FieldData(MemoryValueType type)
{
    public MemoryValueType Type { get; } = type;
        
    public static FieldData Deserialize(byte data)
    {
        return new FieldData((MemoryValueType)data);
    }
}
    
public class MethodData(int programAddress, int localVariableCount)
{
    public int ProgramAddress { get; } = programAddress;
    public int LocalVariableCount { get; } = localVariableCount;

    public static MethodData Deserialize(byte[] data)
    {
        // binary buffer layout
        // 0..3     4byte : program address
        // 4..7     4byte : local variable count
        // 8..(8 + <local variable count>)  <local variable count>byte:
        //                  local variable type layout
        var programAddress = BitConverter.ToInt32(data, 0);
        var localVariableCount = BitConverter.ToInt32(data, 4);

        return new MethodData(programAddress, localVariableCount);
    }

    public override string ToString() => $"PC:{ProgramAddress} LocalVariableCount:{LocalVariableCount}";
}