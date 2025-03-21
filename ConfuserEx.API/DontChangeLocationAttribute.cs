using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Interface | AttributeTargets.Struct)]
public class DontChangeLocationAttribute : Attribute;
