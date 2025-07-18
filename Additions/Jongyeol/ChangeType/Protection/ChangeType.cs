using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using ConfuserEx_Additions.Jongyeol;
using ConfuserEx.API;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Confuser.Protections {
	public class ChangeType : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Minimum;
		public override string Name => "Type Change Protection";
		public override string Description => "Change All Types";
		public override string Id => "type Change";
		public override string FullId => "Jongyeol.TypeChange";
		protected override void Initialize(ConfuserContext context) { }
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.OptimizeMethods, new ChangeTypePhase(this));
		}

		private class ChangeTypePhase : ProtectionPhase {

			public ChangeTypePhase(ConfuserComponent parent) : base(parent) { }

			public override ProtectionTargets Targets => ProtectionTargets.Modules;

			public override string Name => "type Change";

			private IMarkerService marker;
			private ModuleDefMD module;
			private TypeSig objectTypeSig;
			private TypeSig byteTypeSig;
			private TypeSig shortTypeSig;
			private TypeSig intTypeSig;
			private TypeSig longTypeSig;

			private TypeSig GetObjectTypeSig() => objectTypeSig ??= MakeTypeSig(module.CorLibTypes.Object.TypeDefOrRef, false);
			private TypeSig GetByteTypeSig() => byteTypeSig ??= MakeTypeSig(module.CorLibTypes.Byte.TypeDefOrRef, true);
			private TypeSig GetShortTypeSig() => shortTypeSig ??= MakeTypeSig(module.CorLibTypes.Int16.TypeDefOrRef, true);
			private TypeSig GetIntTypeSig() => intTypeSig ??= MakeTypeSig(module.CorLibTypes.Int32.TypeDefOrRef, true);
			private TypeSig GetLongTypeSig() => longTypeSig ??= MakeTypeSig(module.CorLibTypes.Int64.TypeDefOrRef, true);

			private TypeSig MakeTypeSig(ITypeDefOrRef type, bool isEnum) {
				TypeDefUser typeDef = new(Rename.RandomName(), Rename.RandomName(), isEnum ? module.CorLibTypes.GetTypeRef("System", "Enum") : type);
				module.Types.Add(typeDef);
				marker.Mark(typeDef, Parent);
				if(isEnum) {
					FieldDefUser valueField = new("value__", new FieldSig(type.ToTypeSig())) {
						Attributes = FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName
					};
					typeDef.Fields.Add(valueField);
					marker.Mark(valueField, Parent);
				}
				return typeDef.ToTypeSig();
			}

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				marker = context.Registry.GetService<IMarkerService>();
				foreach(ModuleDefMD module in parameters.Targets.OfType<ModuleDefMD>().WithProgress(context.Logger)) {
					this.module = module;
					foreach(TypeDef type in module.Types.ToArray()) ChangeType(type, TypeChangeTargets.All);
				}
			}

			private void ChangeType(TypeDef type, TypeChangeTargets targets) {
		        targets = ParseTargets(targets, type.CustomAttributes);
		        bool attribute = false;
		        TypeDef type2 = type;
		        while(type2.BaseType != null) {
			        if(type2.BaseType.FullName == typeof(Attribute).FullName) {
				        attribute = true;
				        break;
			        }
			        type2 = type2.BaseType.ResolveTypeDef();
		        }
		        foreach(MethodDef method in type.Methods) {
		            TypeChangeTargets methodTargets = ParseTargets(targets, method.CustomAttributes);
		            if(methodTargets.HasFlag(TypeChangeTargets.Method) && CheckType(method.ReturnType, out TypeSig typeSig)) method.ReturnType = typeSig;
		            if(method is not { IsVirtual: true, IsNewSlot: false } && !(attribute && method.IsConstructor)) {
			            for(int i = 0; i < method.Parameters.Count; i++) {
				            Parameter parameter = method.Parameters[i];
				            TypeChangeTargets parameterTargets = ParseTargets(methodTargets, parameter.ParamDef?.CustomAttributes);
				            if(parameterTargets.HasFlag(TypeChangeTargets.Parameter) && CheckType(parameter.Type, out typeSig)) parameter.Type = typeSig;
			            }
		            }
		            if(method.HasBody)
		                foreach(Local local in method.Body.Variables)
		                    if(methodTargets.HasFlag(TypeChangeTargets.Local) && CheckType(local.Type, out typeSig)) local.Type = typeSig;
		        }
		        foreach(FieldDef field in type.Fields) {
		            TypeChangeTargets fieldTargets = ParseTargets(targets, field.CustomAttributes);
		            if(fieldTargets.HasFlag(TypeChangeTargets.Field) && CheckType(field.FieldType, out TypeSig typeSig)) field.FieldType = typeSig;
		        }
		        foreach(PropertyDef property in type.Properties) {
		            TypeChangeTargets propertyTargets = ParseTargets(targets, property.CustomAttributes);
		            if(propertyTargets.HasFlag(TypeChangeTargets.Property) && CheckType(property.PropertySig.RetType, out TypeSig typeSig)) property.PropertySig.RetType = typeSig;
		        }
		        foreach(TypeDef typeDef in type.GetTypes()) ChangeType(typeDef, targets);
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

			private bool CheckType(TypeSig typeSig, out TypeSig outTypeSig) {
				if(typeSig.IsPrimitive) {
					int size = typeSig.ElementType.GetPrimitiveSize();
					outTypeSig = size switch {
						1 => GetByteTypeSig(),
						2 => GetShortTypeSig(),
						4 => GetIntTypeSig(),
						8 => GetLongTypeSig(),
						_ => null
					};
					if(outTypeSig != null) return true;
				}
				if(typeSig.IsValueType) {
					TypeDef typeDef = typeSig.ToTypeDefOrRef().ResolveTypeDef();
					if(typeDef != null) {
						if(typeDef.IsEnum) {
							FieldDef fieldDef = typeDef.GetField("value__");
							int size = fieldDef.FieldType.ElementType.GetPrimitiveSize();
							outTypeSig = size switch {
								1 => GetByteTypeSig(),
								2 => GetShortTypeSig(),
								4 => GetIntTypeSig(),
								8 => GetLongTypeSig(),
								_ => null
							};
							if(outTypeSig != null) return true;
						}
					}
					outTypeSig = null;
					return false;
				}
				if(typeSig is GenericMVar or GenericVar) {
					outTypeSig = null;
					return false;
				}
				if(typeSig is NonLeafSig { Next: not GenericMVar and GenericVar }) {
					outTypeSig = null;
					return false;
				}
				outTypeSig = GetObjectTypeSig();
				return true;
			}
		}
	}
}
