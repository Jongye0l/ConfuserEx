using System;

namespace ConfuserEx.API;

[Flags]
public enum TypeChangeTargets {
    None = 0,
    Field = 1,
    Method = 2,
    Property = 4,
    Parameter = 8,
    Local = 0x10,
    All = 0xFFFFFFF
}
