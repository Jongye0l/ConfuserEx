using System;

namespace ConfuserEx.API;

[Flags]
public enum RenameTargets {
    None = 0,
    Method = 1,
    Field = 2,
    Property = 4,
    Event = 8,
    Type = 16,
    TypeGenericParameter = 32,
    Parameter = 64,
    MethodGenericParameter = 128,
    Namespace = 256,
    All = 0xFFFFFFF
}