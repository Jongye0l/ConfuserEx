using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | 
                AttributeTargets.Event | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class ExcludeStaticMoveAttribute : StaticMoveAttribute {
	public StaticMoveTargets Targets { get; }
	
	public ExcludeStaticMoveAttribute(StaticMoveTargets targets) {
		Targets = targets;
	}
}
