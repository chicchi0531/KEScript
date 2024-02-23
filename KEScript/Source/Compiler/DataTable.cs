namespace KESCompiler.Compiler
{
    public class DataTable
    {
        Dictionary<string, ClassObject> _classTable = new();
        Dictionary<string, FunctionObject> _globalFunctionTable = new();
        Dictionary<string, VariableObject> _globalVariableTable = new();
        
        Dictionary<string, int> _classAddressTable = new();
        Dictionary<string, int> _globalFunctionAddressTable = new();
        Dictionary<string, int> _globalVariableAddressTable = new();
        
        public void AddClass(string name, ClassObject obj)
        {
            _classTable.Add(name, obj);
        }
        public ClassObject FindClass(string name)
        {
            _classTable.TryGetValue(name, out var table);
            return table;
        }
        
        public void AddGlobalFunction(string name, FunctionObject obj)
        {
            _globalFunctionTable.Add(name, obj);
        }
        public FunctionObject FindGlobalFunction(string name)
        {
            _globalFunctionTable.TryGetValue(name, out var table);
            return table;
        }
        
        public void AddGlobalVariable(string name, VariableObject obj)
        {
            _globalVariableTable.Add(name, obj);
        }
        public VariableObject FindGlobalVariable(string name)
        {
            _globalVariableTable.TryGetValue(name, out var table);
            return table;
        }
        
    }

    public class ClassObject
    {
    }

    public class FunctionObject
    {
        
    }

    public class VariableObject
    {
        
    }
}
