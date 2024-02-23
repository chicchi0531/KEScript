namespace KESCompiler.Compiler
{
    public struct Unit : IEquatable<Unit>
    {
        static readonly Unit _default = new();
        public static Unit Default => _default;
        public static bool operator ==(Unit first, Unit second) => true;
        public static bool operator !=(Unit first, Unit second) => false;
        public bool Equals(Unit other) => true;
        public override bool Equals(object obj) => obj is Unit;
        public override int GetHashCode() => 0;
    }
}
