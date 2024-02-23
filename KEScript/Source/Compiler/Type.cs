namespace KESCompiler.Compiler
{
    public enum EPredefinedType
    {
        Int,
        Float,
        Bool,
        String,
        Void,
    }

    public class KesTypeTable
    {
        List<KesType> _typeList = new List<KesType>();
        
        public void AddType(KesType type)
        {
            _typeList.Add(type);
        }
        
        public KesType FindType(string name)
        {
            return _typeList.FirstOrDefault(type => type.name == name);
        }
        
        public KesType FindType(int address)
        {
            return _typeList.FirstOrDefault(type => type.address == address);
        }
    }

    public struct KesType
    {
        public string name;
        public string[] superTypes;
        public bool isArray;
        public bool isReferenceType;
        public int size;
        public int address;
        public int[] methodAddressTable;
        public int[] fieldAddressTable;
    }
}
