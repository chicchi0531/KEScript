namespace KESCompiler.Runtime
{
    public readonly struct VmSettings
    {
        public readonly uint defaultStackSize;
        public readonly uint defaultCallStackSize;

        public VmSettings(uint defaultStackSize, uint defaultCallStackSize)
        {
            if (defaultStackSize > 65536)
            {
                throw new ArgumentException("The default stack size must be between 0 and 65536.");
            }
            this.defaultStackSize = defaultStackSize;
            
            if (defaultCallStackSize > 65536)
            {
                throw new ArgumentException("The default call stack size must be between 0 and 65536.");
            }
            this.defaultCallStackSize = defaultCallStackSize;
        }
    }
    
    public partial class KVirtualMachine
    {
        int _pc = 1;
        MemoryValue[] _stack;
        StackFrame[] _callStack;
        int _sp;
        int _csp;
        List<string> _stringBuffer;
        Stack<int> _stringBufferFreeList;
        bool _stop;

        public KVirtualMachine(VmSettings settings)
        {
            Reset(settings);
        }
        
        void Reset(VmSettings settings)
        {
            _pc = 1;
            _sp = 0;
            _csp = 0;
            _stack = new MemoryValue[settings.defaultStackSize];
            _callStack = new StackFrame[settings.defaultCallStackSize];
            _stringBuffer = new();
            _stringBufferFreeList = new();
            _stop = false;
        }
        
        public void Run(byte[] programData)
        {
            var program = ProgramData.Deserialize(programData);
            
            while (_pc != 0 && !_stop)
            {
                // ここで命令実行
                var op = program.Program[_pc++];
                
                return;
            }
        }
        
        public void Shut() => _stop = true;
    }
}
