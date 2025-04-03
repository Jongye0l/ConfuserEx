using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = true)]
public class ExcludeCompactBoolAttribute : CompactBoolAttribute;