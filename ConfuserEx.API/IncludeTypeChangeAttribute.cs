using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Class |
                AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Parameter, AllowMultiple = true)]
public class IncludeTypeChangeAttribute : TypeChangeAttribute {
    public TypeChangeTargets Targets { get; }

    public IncludeTypeChangeAttribute(TypeChangeTargets targets) {
        Targets = targets;
    }
}
