using System;

namespace ConfuserEx.API;

[Flags]
public enum StaticMoveTargets {
	None = 0,
	Field = 1,
	Method = 2,
	Property = 4,
	Event = 8,
	All = Field | Method | Property | Event
}
