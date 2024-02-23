using System.Text;

namespace KESCompiler.Runtime
{
    public struct Address
    {
        public int head;
        public int offset;
        public Address(int h, int o)
        {
            head = h;
            offset = o;
        }
        public static Address Null => new(0, 0);
        
        public static Address Deserialize(byte[] data)
        {
            var head = BitConverter.ToInt32(data, 0);
            var offset = BitConverter.ToInt32(data, 4);
            return new Address(head, offset);
        }

        public static byte[] Serialize(Address address)
        {
            var data = new byte[8];
            BitConverter.GetBytes(address.head).CopyTo(data, 0);
            BitConverter.GetBytes(address.offset).CopyTo(data, 4);
            return data;
        }
    }

    public readonly struct MemoryChunkHeader
    {
        public readonly ValueType[] layout;
        public int Size => layout.Length;
        public MemoryChunkHeader(ValueType[] layout)
        {
            this.layout = layout;
        }
    }
    
    /// <summary>
    /// メモリの最小管理単位
    /// サイズは1つのオブエジェクト分となる。
    /// </summary>
    public class MemoryChunk
    {
        public MemoryChunk(MemoryChunkHeader header)
        {
            Header = header;
            Marked = false;
            _memory = new MemoryValue[header.Size];
            _size = header.Size;

            // レイアウトに従ってメモリ初期化
            for (int i=0; i<header.Size; i++)
            {
                switch (header.layout[i])
                {
                    case ValueType.Int32: 
                        _memory[i] = new MemoryValue(0);
                        break;
                    case ValueType.Float32: 
                        _memory[i] = new MemoryValue(0f);
                        break;
                    case ValueType.Boolean: 
                        _memory[i] = new MemoryValue(false);
                        break;
                    case ValueType.Address:
                    case ValueType.Null: 
                        _memory[i] = new MemoryValue(new Address(0,0));
                        break;
                }
            }
        }

        public ReadOnlySpan<MemoryValue> Memory => new(_memory);
        public MemoryChunkHeader Header { get; }
        public bool Marked { get; set; }
        
        readonly MemoryValue[] _memory;
        readonly int _size;
        
        public void Set(int offset, MemoryValue value)
        {
            if (Header.layout[offset] != value.type)
            {
                throw new ArgumentException($"Type mismatch. Expected {Header.layout[offset]} but {value.type}.");
            }
            _memory[offset] = value;
        }

        public MemoryValue Get(int offset) => _memory[offset];
        
        public override string ToString()
        {
            var str = new StringBuilder();
            foreach (var value in _memory)
            {
                str.Append($"{value} ");
            }
            return str.ToString();
        }
    }
    
    public class KesVirtualMemory
    {
        int _currentAddress = 1; //0はnullとみなすので1が最小
        int _collectThreshold = 1000;
        readonly Dictionary<int, MemoryChunk> _memory = new();

        public KesVirtualMemory()
        {
        }
        
        public Address Malloc(ValueType[] layout)
        {
            var header = new MemoryChunkHeader(layout);
            var chunk = new MemoryChunk(header);
            
            //メモリ確保
            var headAddress = new Address(_currentAddress++, 0);
            _memory.Add(headAddress.head, chunk);

            return headAddress;
        }

        public void Set(Address address, MemoryValue data)
        { 
            _memory[address.head].Set(address.offset, data);
        }

        public MemoryValue Get(Address address)
        {
            return _memory[address.head].Get(address.offset);
        }

        public void Collect(StackFrame currentFrame)
        {
            foreach (var m in _memory)
            {
                m.Value.Marked = false;
            }

            MarkFromStackFrame(currentFrame);
            Sweep();
        }

        void MarkFromStackFrame(StackFrame frame)
        {
            //現在のフレームの変数スタックを走査
            foreach (var v in frame.GetVariableStack())
            {
                if (v.type is ValueType.Address)
                {
                    Mark(v.address);
                }
            }
            //親フレームがあればそれも走査
            if(frame.Parent != null) MarkFromStackFrame(frame.Parent);
        }

        void Mark(Address address)
        {
            // nullチェック
            if (address.head == 0) return;
            
            // チャンクがマーク済みであればすでに訪れているので終了
            var chunk = _memory[address.head];
            if(chunk.Marked) return;
            chunk.Marked = true;
            
            // チャンクがアドレスを持っていればそのアドレスを再帰的にマーク
            foreach (var v in chunk.Memory)
            {
                if (v.type == ValueType.Address)
                {
                    Mark(v.address);
                }
            }
        }

        void Sweep()
        {
            List<int> removeList = new();
            foreach (var m in _memory)
            {
                if (!m.Value.Marked)
                {
                    removeList.Add(m.Key);
                }
            }
            foreach (var r in removeList)
            {
                _memory.Remove(r);
            }
        }

        public override string ToString()
        {
            var str = new StringBuilder();
            foreach (var m in _memory)
            {
                str.Append($"[${m.Key}:{m.Value}]\n");
            }
            return str.ToString();
        }
    }
}
