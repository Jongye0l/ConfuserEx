using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Class |
                AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Parameter, AllowMultiple = true)]
public class IncludeRenameAttribute : RenameAttribute {
    public RenameTargets Targets { get; }

    public IncludeRenameAttribute(RenameTargets targets) {
        Targets = targets;
    }
}
