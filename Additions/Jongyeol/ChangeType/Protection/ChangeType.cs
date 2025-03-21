using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using ConfuserEx_Additions.Jongyeol;
using ConfuserEx.API;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Utils = ConfuserEx_Additions.Jongyeol.Utils;

namespace Confuser.Protections {
	public class ChangeType : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Minimum;
		public override string Name => "Type Change Protection";
		public override string Description => "Change All Types";
		public override string Id => "type Change";
		public override string FullId => "Confuser.Jongyeol.TypeChange";
		protected override void Initialize(ConfuserContext context) { }
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPreStage(PipelineStage.WriteModule, new ChangeTypePhase(this));
		}

		private class ChangeTypePhase : ProtectionPhase {

			public ChangeTypePhase(ConfuserComponent parent) : base(parent) { }

			public override ProtectionTargets Targets => ProtectionTargets.AllMembers;

			public override string Name => "type Change";

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				foreach (ModuleDefMD module in parameters.Targets.OfType<ModuleDefMD>()) {
					TypeDefUser typeDef = new(Rename.RandomName(), Rename.RandomName(), module.CorLibTypes.Object.TypeDefOrRef);
					module.Types.Add(typeDef);
					TypeSig typeSig = typeDef.ToTypeSig();
					foreach(TypeDef type in module.Types) ChangeType(type, typeSig, TypeChangeTargets.All);
				}
			}

			private static void ChangeType(TypeDef type, TypeSig typeSig, TypeChangeTargets targets) {
		        targets = ParseTargets(targets, type.CustomAttributes);
		        foreach(MethodDef method in type.Methods) {
		            TypeChangeTargets methodTargets = ParseTargets(targets, method.CustomAttributes);
		            if(methodTargets.HasFlag(TypeChangeTargets.Method) && CheckType(method.ReturnType)) method.ReturnType = typeSig;
		            if(method is not { IsVirtual: true, IsNewSlot: false })
		                foreach(Parameter parameter in method.Parameters) {
		                    TypeChangeTargets parameterTargets = ParseTargets(methodTargets, parameter.ParamDef?.CustomAttributes);
		                    if(parameterTargets.HasFlag(TypeChangeTargets.Parameter) && CheckType(parameter.Type)) parameter.Type = typeSig;
		                }
		            if(method.HasBody)
		                foreach(Local local in method.Body.Variables)
		                    if(methodTargets.HasFlag(TypeChangeTargets.Local) && CheckType(local.Type)) local.Type = typeSig;
		        }
		        foreach(FieldDef field in type.Fields) {
		            TypeChangeTargets fieldTargets = ParseTargets(targets, field.CustomAttributes);
		            if(fieldTargets.HasFlag(TypeChangeTargets.Field) && CheckType(field.FieldType)) field.FieldType = typeSig;
		        }
		        foreach(PropertyDef property in type.Properties) {
		            TypeChangeTargets propertyTargets = ParseTargets(targets, property.CustomAttributes);
		            if(propertyTargets.HasFlag(TypeChangeTargets.Property) && CheckType(property.PropertySig.RetType)) property.PropertySig.RetType = typeSig;
		        }
		        foreach(TypeDef typeDef in type.GetTypes()) ChangeType(typeDef, typeSig, targets);
		    }

			private static TypeChangeTargets ParseTargets(TypeChangeTargets targets, CustomAttributeCollection attributes) {
				if(attributes == null) return targets;
				List<CustomAttribute> removeAttributes = [];
				foreach(CustomAttribute attribute in attributes)
					if(attribute.TypeFullName == typeof(IncludeTypeChangeAttribute).FullName) {
						removeAttributes.Add(attribute);
						targets |= (TypeChangeTargets) attribute.ConstructorArguments[0].Value;
					} else if(attribute.TypeFullName == typeof(ExcludeTypeChangeAttribute).FullName) {
						removeAttributes.Add(attribute);
						targets &= ~(TypeChangeTargets) attribute.ConstructorArguments[0].Value;
					}
				foreach(CustomAttribute attribute in removeAttributes) attributes.Remove(attribute);
				return targets;
			}

			private static bool CheckType(TypeSig typeSig) {
				if(typeSig is not ({ IsValueType: false, IsValueArray: false } and not GenericMVar and not GenericVar)) return false;
				if(typeSig is NonLeafSig { Next: not GenericMVar and GenericVar }) return false;
				return true;
			}
		}
	}
}
