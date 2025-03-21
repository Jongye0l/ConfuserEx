using System;

namespace ConfuserEx.API;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property |
                AttributeTargets.Event | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public class IncludeStaticMoveAttribute : StaticMoveAttribute {
	public StaticMoveTargets Targets { get; }

	public IncludeStaticMoveAttribute(StaticMoveTargets targets) {
		Targets = targets;
	}
}
