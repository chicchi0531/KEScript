using System.Runtime.InteropServices;

namespace KESCompiler.Runtime
{
        public enum EOpCode : byte
    {
        Nop,
        SystemCall,
        Break,
        
        LdArg,
        StArg,
        LdLoc,
        StLoc,
        LdGlb,
        StGlb,
        LdcI1,
        LdcI4,
        LdcR4,
        LdStr,
        LdNull,
        Dup,
        Pop,
        Jump,
        Call,
        CallVirt,
        Ret,
        Br,
        BrFalse,
        BrTrue,
        
        Add,
        Sub,
        Mul,
        Div,
        Mod,
        Neg,
        Not,
        And,
        Or,
        Xor,
        Shl,
        Shr,
        Inc,
        Dec,
        Ceq,
        Cgt,
        Cge,
        Clt,
        Cle,
        
        ConvI4,
        ConvR4,
        
        CpObj,
        NewObj,
        LdFld,
        StFld,
        
        NewArr,
        LdElem,
        StElem,
        
        DummyBreak,    //break分用のダミー命令　最終的にはbrに置き換わる
        DummyContinue, //continue分用のダミー命令　最終的にはbrに置き換わる
    }

    [StructLayout(LayoutKind.Explicit, Size=16)]
    public readonly struct Operation
    {
        [FieldOffset(0)] public readonly EOpCode eOpCode;
        
        //operand
        [FieldOffset(1)] public readonly Address address;
        [FieldOffset(1)] public readonly int iValue;
        [FieldOffset(1)] public readonly bool bValue;
        [FieldOffset(1)] public readonly float fValue;
         
        //デシリアライズ用
        [FieldOffset(0)] readonly byte b0;
        [FieldOffset(1)] readonly byte b1;
        [FieldOffset(2)] readonly byte b2;
        [FieldOffset(3)] readonly byte b3;
        [FieldOffset(4)] readonly byte b4;
        [FieldOffset(5)] readonly byte b5;
        [FieldOffset(6)] readonly byte b6;
        [FieldOffset(7)] readonly byte b7;
        [FieldOffset(8)] readonly byte b8;
        [FieldOffset(9)] readonly byte b9;
        [FieldOffset(10)] readonly byte b10;
        [FieldOffset(11)] readonly byte b11;
        [FieldOffset(12)] readonly byte b12;
        [FieldOffset(13)] readonly byte b13;
        [FieldOffset(14)] readonly byte b14;
        [FieldOffset(15)] readonly byte b15;

        Operation(ReadOnlySpan<byte> data)
        {
            eOpCode = EOpCode.Nop;
            address = default; iValue = 0; bValue = false; fValue = 0;
            b0 = data[0]; b1 = data[1]; b2 = data[2]; b3 = data[3];
            b4 = data[4]; b5 = data[5]; b6 = data[6]; b7 = data[7];
            b8 = data[8]; b9 = data[9]; b10 = data[10]; b11 = data[11];
            b12 = data[12]; b13 = data[13]; b14 = data[14]; b15 = data[15];
        }

        
        public Operation(EOpCode code)
        {
            b0=b1=b2=b3=b4=b5=b6=b7=b8=b9=b10=b11=b12=b13=b14=b15=0;
            address = default; bValue = false; fValue = 0; iValue = 0;
            
            eOpCode = code;
        }
        public Operation(EOpCode code, int operand)
        {
            b0=b1=b2=b3=b4=b5=b6=b7=b8=b9=b10=b11=b12=b13=b14=b15=0;
            address = default; bValue = false; fValue = 0;
            
            eOpCode = code;
            iValue = operand;
        }
        public Operation(EOpCode code, float operand)
        {
            b0=b1=b2=b3=b4=b5=b6=b7=b8=b9=b10=b11=b12=b13=b14=b15=0;
            address = default; bValue = false; iValue = 0;
            
            eOpCode = code;
            fValue = operand;
        }
        public Operation(EOpCode code, bool operand)
        {
            b0=b1=b2=b3=b4=b5=b6=b7=b8=b9=b10=b11=b12=b13=b14=b15=0;
            address = default; iValue = 0; fValue = 0;
            
            eOpCode = code;
            bValue = operand;
        }
        public Operation(EOpCode code, Address operand)
        {
            b0=b1=b2=b3=b4=b5=b6=b7=b8=b9=b10=b11=b12=b13=b14=b15=0;
            bValue = false; fValue = 0; iValue = 0;
            
            eOpCode = code;
            address = operand;
        }
        
        public static Operation Deserialize(byte[] data)
        {
            return Deserialize(new ReadOnlySpan<byte>(data));
        }
        public static Operation Deserialize(ReadOnlySpan<byte> data)
        {
            if (data.Length != 16)
            {
                throw new ArgumentException("The length of the data must be 16.");
            }
            
            return new Operation(data);
        }

        public override string ToString()
        {
            return $"{eOpCode} {iValue}";
        }
    }
    
    public partial class KVirtualMachine
    {
        void Nop(){}
        void SystemCall(string operand){}
        void Break(){}
        void LdArg(int index){}
        void StArg(int index){}
        void LdLoc(int index){}
        void StLoc(int index){}
        void LdGlb(int index){}
        void StGlb(int index){}
        void LdcI1(byte value){}
        void LdcI4(int value){}
        void LdcR4(float value){}
        void LdStr(int index){}
        void LdNull(){}
        void Dup(){}
        MemoryValue Pop()
        {
            return _stack[--_sp];
        }
        void Jump(int address)
        {
            _pc = address;
        }
        void Call(int address)
        {
            //_callStack[_csp++] = new StackFrame();
            _pc = address;
        }
        void CallVirt(int index){}
        void Ret()
        {
            //_pc = _callStack[--_csp];
        }
        void Br(int address)
        {
            _pc = address;
        }
        void BrFalse(int address)
        {
            var value = Pop();
            if (!value.boolValue)
            {
                _pc = address;
            }
        }
        void BrTrue(int address)
        {
            var value = Pop();
            if (value.boolValue)
            {
                _pc = address;
            }
        }
        void Add()
        {
            MemoryValue result;
            var b = Pop();
            var a = Pop();
            switch (a.type)
            {
                case MemoryValueType.Int32:
                    result = new MemoryValue(a.iValue + b.iValue);
                    break;
                case MemoryValueType.Float32:
                    result = new MemoryValue(a.fValue + b.fValue);
                    break;
                default: throw new FormatException();
            }
            Push(result);
        }
        void Sub()
        {
            MemoryValue result;
            var b = Pop();
            var a = Pop();
            switch (a.type)
            {
                case MemoryValueType.Int32:
                    result = new MemoryValue(a.iValue - b.iValue);
                    break;
                case MemoryValueType.Float32:
                    result = new MemoryValue(a.fValue - b.fValue);
                    break;
                default: throw new FormatException();
            }
            Push(result);
        }
        void Mul()
        {
            MemoryValue result;
            var b = Pop();
            var a = Pop();
            switch (a.type)
            {
                case MemoryValueType.Int32:
                    result = new MemoryValue(a.iValue * b.iValue);
                    break;
                case MemoryValueType.Float32:
                    result = new MemoryValue(a.fValue * b.fValue);
                    break;
                default: throw new FormatException();
            }
            Push(result);
        }
        void Div()
        {
            MemoryValue result;
            var b = Pop();
            var a = Pop();
            switch (a.type)
            {
                case MemoryValueType.Int32:
                    result = new MemoryValue(a.iValue / b.iValue);
                    break;
                case MemoryValueType.Float32:
                    result = new MemoryValue(a.fValue / b.fValue);
                    break;
                default: throw new FormatException();
            }
            Push(result);
        }
        void Mod()
        {
            MemoryValue result;
            var b = Pop();
            var a = Pop();
            switch (a.type)
            {
                case MemoryValueType.Int32:
                    result = new MemoryValue(a.iValue % b.iValue);
                    break;
                case MemoryValueType.Float32:
                    result = new MemoryValue(a.fValue % b.fValue);
                    break;
                default: throw new FormatException();
            }
            Push(result);
        }
        void Neg()
        {
            var a = Pop();
            var value = -a.iValue;
            Push(new MemoryValue(value));
        }
        void Not()
        {
            var a = Pop();
            var value = !a.boolValue;
            Push(new MemoryValue(value));
        }
        void And()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue & b.iValue;
            Push(new MemoryValue(value));
        }
        void Or()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue | b.iValue;
            Push(new MemoryValue(value));
        }
        void Xor()
        {
            // todo
        }
        void Shl()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue << b.iValue;
            Push(new MemoryValue(value));
        }
        void Shr()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue >> b.iValue;
            Push(new MemoryValue(value));
        }
        void Inc()
        {
            // todo
        }
        void Dec()
        {
            
        }
        
        void Ceq()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue == b.iValue; //32bitが最大幅なので、intで検算。64bit値に対応する場合は要修正
            Push(new MemoryValue(value));
        }
        void Neq()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue != b.iValue;
            Push(new MemoryValue(value));
        }
        void Cgt()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue > b.iValue;
            Push(new MemoryValue(value));
        }
        void Cge()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue >= b.iValue;
            Push(new MemoryValue(value));
        }
        void Clt()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue < b.iValue;
            Push(new MemoryValue(value));
        }
        void Cle()
        {
            var b = Pop();
            var a = Pop();
            var value = a.iValue <= b.iValue;
            Push(new MemoryValue(value));
        }

        void ConvI4()
        {
            
        }

        void ConvR4()
        {
            
        }

        void CpObj()
        {
            
        }
        
        void NewObj()
        {}
        
        void LdFld(int index)
        {}
        void StFld(int index)
        {
            
        }
        
        void NewArr(int size)
        {
        }
        
        void LdElem(int index)
        {
        }
        
        void StElem(int index)
        {
        }

        void AddStr()
        {
            
        }
        
        void Push(MemoryValue value)
        {
            _stack[_sp++] = value;
        }
    }
}
