using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Class |
                AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Parameter)]
public class SetRenameAttribute : RenameAttribute {
    public SetRenameAttribute(string name) {
    }
}