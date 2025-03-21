using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using ConfuserEx.API;
using dnlib.DotNet;
using Utils = ConfuserEx_Additions.Jongyeol.Utils;

namespace Confuser.Protections {
	public class ShuffleProtection : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Minimum;
		public override string Name => "Shuffle Protection";
		public override string Description => "Shuffle All Members";
		public override string Id => "shuffle members";
		public override string FullId => "Confuser.SuffleMember";
		protected override void Initialize(ConfuserContext context) { }
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.WriteModule, new ShufflePhase(this));
		}

		private class ShufflePhase : ProtectionPhase {

			public ShufflePhase(ConfuserComponent parent) : base(parent) { }

			public override ProtectionTargets Targets => ProtectionTargets.AllMembers;

			public override string Name => "Shuffle Members";

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				foreach(TypeDef type in parameters.Targets.OfType<TypeDef>().WithProgress(context.Logger)) {
					CustomAttribute attribute = type.CustomAttributes.Find(nameof(DontChangeLocationAttribute));
					if(attribute != null) {
						type.CustomAttributes.Remove(attribute);
						continue;
					}
					List<IDnlibDef> members = [];
					members.AddRange(type.Methods);
					members.AddRange(type.Fields);
					members.AddRange(type.Properties);
					members = members.OrderBy(_ => Utils.Random.Next()).ToList();
					foreach(IDnlibDef t in members) {
						switch(t) {
							case FieldDef field:
								type.Fields.Remove(field);
								type.Fields.Add(field);
								break;
							case PropertyDef property:
								type.Properties.Remove(property);
								type.Properties.Add(property);
								break;
							case MethodDef method:
								type.Methods.Remove(method);
								type.Methods.Add(method);
								break;
						}
					}
				}
			}
		}
	}
}
