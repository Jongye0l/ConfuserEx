using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Class |
                AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Parameter, AllowMultiple = true)]
public class ExcludeRenameAttribute : RenameAttribute {
    public RenameTargets Targets { get; }

    public ExcludeRenameAttribute(RenameTargets targets) {
        Targets = targets;
    }
}
