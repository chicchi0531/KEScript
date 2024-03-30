using System.Runtime.InteropServices;
using System.Text;

namespace KESCompiler.Runtime;

public enum MemoryValueType : byte
{
    Boolean,
    Int32,
    Float32,
    Address,
    Null,
}
    
public class StackFrame
{
    public StackFrame Parent { get; } //呼び出し元のスタックフレーム

    public List<MemoryValue> _stack = new();
    int _variableStackPointerHeadOffset;
    int _variableStackPointerTailOffset;
        
    public StackFrame(StackFrame parent, int head, int tail)
    {
        _variableStackPointerHeadOffset = head;
        _variableStackPointerTailOffset = tail;
        Parent = parent;
    }
        
    public MemoryValue[] GetVariableStack()
    {
        var stack = new MemoryValue[_variableStackPointerTailOffset - _variableStackPointerHeadOffset];
        for (int i = 0; i < stack.Length; i++)
        {
            stack[i] = _stack[_variableStackPointerHeadOffset + i];
        }
        return stack;
    }

    public void Push(MemoryValue value)
    {
        _stack.Add(value);
    }

    public MemoryValue Pop()
    {
        var tail = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        return tail;
    }
}
    
[StructLayout(LayoutKind.Explicit)]
public readonly struct MemoryValue
{
    [FieldOffset(0)]
    public readonly MemoryValueType type;

    //最大長は64bit(8byte)とする
    [FieldOffset(1)] public readonly byte bValue;
    [FieldOffset(1)] public readonly int iValue;
    [FieldOffset(1)] public readonly float fValue;
    [FieldOffset(1)] public readonly bool boolValue;
    [FieldOffset(1)] public readonly long lValue;
    [FieldOffset(1)] public readonly Address address;

    public MemoryValue(int value)
    {
        type = MemoryValueType.Int32;
        bValue = default;
        fValue = default;
        boolValue = default;
        lValue = default;
        address = default;

        iValue = value;
    }
        
    public MemoryValue(float value)
    {
        type = MemoryValueType.Float32;
        bValue = default;
        iValue = default;
        boolValue = default;
        lValue = default;
        address = default;

        fValue = value;
    }
    public MemoryValue(bool value)
    {
        type = MemoryValueType.Boolean;
        bValue = default;
        fValue = default;
        iValue = default;
        lValue = default;
        address = default;
            
        boolValue = value;
    }
    public MemoryValue(Address value)
    {
        type = MemoryValueType.Address;
        bValue = default;
        fValue = default;
        iValue = default;
        lValue = default;
        boolValue = default;
            
        address = value;
    }

    public override string ToString()
    {
        var str = new StringBuilder();
            
        // byte列を表示
        var byteArray = BitConverter.GetBytes(lValue);
        foreach(var b in byteArray)
        {
            str.Append($"{b:X2} ");
        }
            
        // 型に応じた値を表示
        switch (type)
        {
            case MemoryValueType.Int32: str.Append($"[int:{iValue}]"); break;
            case MemoryValueType.Float32: str.Append($"[float:{fValue}]"); break;
            case MemoryValueType.Boolean: str.Append($"[bool:{boolValue}]"); break;
            case MemoryValueType.Address: str.Append($"[addr:{address.head}.{address.offset}]"); break;
        }
        return str.ToString();
    }
}