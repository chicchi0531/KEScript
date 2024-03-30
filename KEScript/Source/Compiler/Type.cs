using KESCompiler.Compiler.Ast;

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

    public record KesType(
        int ElementTypeHandle,
        bool IsArray,
        bool IsImmutable)
    {
        public static readonly KesType typeOfInt = new(KesElementTypeTable.intTypeHandle, false, false);
        public static readonly KesType typeOfFloat = new(KesElementTypeTable.floatTypeHandle, false, false);
        public static readonly KesType typeOfBool = new(KesElementTypeTable.boolTypeHandle, false, false);
        public static readonly KesType typeOfString = new(KesElementTypeTable.stringTypeHandle, false, false);
        public static readonly KesType typeOfNull = new(-1, false, false);
        
        public static KesType ArrayOf(KesType elementType) => new KesType(elementType.ElementTypeHandle, true, elementType.IsImmutable);
    }

    public record KesField(
        string Name,
        EAccessModifier AccessModifier,
        KesType Type);
    
    public record KesMethod(
        string Name,
        EAccessModifier AccessModifier,
        KesType ReturnType,
        KesType[] ArgTypes
        );
    
    public record KesElementType(
        string ModuleName,
        string Name,
        int SuperClassHandle,
        bool IsReference,
        bool IsPrimitive,
        int Size,
        KesMethod[] MethodTable,
        KesField[] FieldTable);
    
    public class KesElementTypeTable
    {
        List<KesElementType> _elementTypeList;

        public const int intTypeHandle = 0;
        public const int floatTypeHandle = 1;
        public const int boolTypeHandle = 2;
        public const int stringTypeHandle = 3;

        public KesElementTypeTable()
        {
            _elementTypeList = new(1024);
            
            // 0:int 1:float 2:bool 3:string
            Register(new KesElementType("", "int", -1, false, true,4,  [], []));
            Register(new KesElementType("","float", -1, false, true,4, [], []));
            Register(new KesElementType("","bool", -1, false, true,4, [], []));
            Register(new KesElementType("","string", -1, false, true, 4, [], []));
        }
        
        public void Register(KesElementType elementType)
        {
            _elementTypeList.Add(elementType);
        }

        public KesElementType? Get(int handle)
        {
            if (handle < 0 && handle >= _elementTypeList.Count) return null;
            return _elementTypeList[handle];
        }

        public (int handle, KesElementType? value) Find(Ast.Expr name)
        {
            switch (name)
            {
                case Ast.PredefinedTypeName p: return Find(p);
                case Ast.ModuleName q: return Find(q);
                case Ast.IdentifierName i: return Find("", i.Name);
            }
            return (-1, null);
        }

        (int handle, KesElementType? value) Find(Ast.PredefinedTypeName name)
        {
            if (name.IsInt) return (intTypeHandle, _elementTypeList[intTypeHandle]);
            if (name.IsFloat) return (floatTypeHandle, _elementTypeList[floatTypeHandle]);
            if (name.IsBool) return (boolTypeHandle, _elementTypeList[boolTypeHandle]);
            if (name.IsString) return (stringTypeHandle, _elementTypeList[stringTypeHandle]);
            return (-1, null);
        }

        (int handle, KesElementType? value) Find(Ast.ModuleName name)
        {
            // モジュール名に続くのは、クラス名のみ
            if (name.Left is not Ast.IdentifierName i) return (-1, null);

            var moduleName = name.Right.Name;
            return Find(moduleName, i.Name);
        }

        public (int, KesElementType?) Find(string moduleName, string name)
        {
            return _elementTypeList
            .Select((value,index) => (index, value))
            .FirstOrDefault((type) => type.value.Name == name);
        }

        public int[] GetSuperClassList(int subClassHandle, Action circularInheritanceErrorCallback)
        {
            var result = new List<int>();
            var c = Get(subClassHandle);
            var superClass = c?.SuperClassHandle;
            while (superClass != null)
            {
                if (result.Contains(superClass.Value)) circularInheritanceErrorCallback();
                result.Add(superClass.Value);
                c = Get(superClass.Value);
                superClass = c?.SuperClassHandle;
            }
            return result.ToArray();
        }
    }
    
    public static class KesTypeUtility
    {
        public static string GetTypeName(KesType type, KesElementTypeTable elementTable)
        {
            var elem = elementTable.Get(type.ElementTypeHandle) ?? null;
            if (elem == null) return "Invalid type";
            return (type.IsImmutable ? "const " : "") + elem.Name +  (type.IsArray ? "[]" : "");
        }
    }
}
