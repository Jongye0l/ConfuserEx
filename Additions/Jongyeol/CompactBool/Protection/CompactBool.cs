using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using ConfuserEx_Additions.Jongyeol;
using ConfuserEx.API;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;

namespace Confuser.Protections {
	public class CompactBool : Protection {
		public override ProtectionPreset Preset => ProtectionPreset.Minimum;
		public override string Name => "Compact Bool Protection";
		public override string Description => "Compact All boolean";
		public override string Id => "compact bool";
		public override string FullId => "Jongyeol.CompactBool";
		protected override void Initialize(ConfuserContext context) { }
		protected override void PopulatePipeline(ProtectionPipeline pipeline) {
			pipeline.InsertPostStage(PipelineStage.OptimizeMethods, new CompactBoolPhase(this));
		}

		private class CompactBoolPhase : ProtectionPhase {

			public CompactBoolPhase(ConfuserComponent parent) : base(parent) { }

			public override ProtectionTargets Targets => ProtectionTargets.Modules;

			public override string Name => "compact bool phase";

			public IMarkerService marker;
			public List<FieldDef> boolFields = [];
			public Dictionary<TypeDef, List<FieldDef>> boolFieldsByType = new();
			public Dictionary<TypeDef, List<FieldDef>> integerFieldsByType = new();
			public List<FieldDef> staticBoolFields = [];
			public List<FieldDef> staticIntegerFields;

			protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
				marker = context.Registry.GetService<IMarkerService>();
				foreach(ModuleDef m in parameters.Targets.OfType<ModuleDef>().WithProgress(context.Logger)) {
					foreach(TypeDef type in m.Types) CheckBoolField(type, true);
					int fieldCount = (int) Math.Ceiling(staticBoolFields.Count / 32.0);
					if(fieldCount > 0) {
						staticIntegerFields = new List<FieldDef>(fieldCount);
						TypeDefUser type = new(Rename.RandomName(), Rename.RandomName(), m.CorLibTypes.Object.TypeDef);
						m.Types.Add(type);
						marker.Mark(type, Parent);
						for(int i = 0; i < fieldCount; i++) {
							FieldDefUser field = new(Rename.RandomName(), new FieldSig(m.CorLibTypes.UInt32), FieldAttributes.Assembly | FieldAttributes.Static);
							type.Fields.Add(field);
							marker.Mark(type, Parent);
							staticIntegerFields.Add(field);
						}
					}
					foreach(TypeDef type in m.Types) MakeIntegerField(type);
					foreach(TypeDef type in m.Types) ConvertBoolToIntIL(type);
					foreach(KeyValuePair<TypeDef,List<FieldDef>> valuePair in boolFieldsByType)
						foreach(FieldDef fieldDef in valuePair.Value) valuePair.Key.Fields.Remove(fieldDef);
				}

			}

			private void CheckBoolField(TypeDef type, bool enabled) {
				enabled = ParseTargets(type.CustomAttributes, enabled);
				foreach(FieldDef field in type.Fields) {
					bool fieldEnabled = ParseTargets(field.CustomAttributes, enabled);
					if(!fieldEnabled || !field.IsPrivate && !field.IsAssembly || field.FieldType != type.Module.CorLibTypes.Boolean) continue;
					if(field.IsStatic) staticBoolFields.Add(field);
					else boolFields.Add(field);
				}
				foreach(TypeDef typeDef in type.GetTypes()) CheckBoolField(typeDef, enabled);
			}

			private static bool ParseTargets(CustomAttributeCollection attributes, bool currentEnabled) {
				if(attributes == null) return currentEnabled;
				List<CustomAttribute> removeAttributes = [];
				foreach(CustomAttribute attribute in attributes)
					if(attribute.TypeFullName == typeof(IncludeTypeChangeAttribute).FullName) {
						removeAttributes.Add(attribute);
						currentEnabled = true;
					} else if(attribute.TypeFullName == typeof(ExcludeTypeChangeAttribute).FullName) {
						removeAttributes.Add(attribute);
						currentEnabled = false;
					}
				foreach(CustomAttribute attribute in removeAttributes) attributes.Remove(attribute);
				return currentEnabled;
			}

			private void MakeIntegerField(TypeDef type) {
				List<FieldDef> fields = boolFields.Where(field => field.DeclaringType == type).ToList();
				boolFieldsByType[type] = fields;
				List<FieldDef> intFields;
				int fieldCount = (int) Math.Ceiling(fields.Count / 32.0);
				if(fieldCount > 0) {
					intFields = new List<FieldDef>(fieldCount);
					for(int i = 0; i < fieldCount; i++) {
						FieldDefUser field = new(Rename.RandomName(), new FieldSig(type.Module.CorLibTypes.Int32), FieldAttributes.Assembly);
						type.Fields.Add(field);
						marker.Mark(type, Parent);
						intFields.Add(field);
					}
					integerFieldsByType[type] = intFields;
				}
				foreach(TypeDef typeDef in type.GetTypes()) MakeIntegerField(typeDef);
			}

			private void ConvertBoolToIntIL(TypeDef type) {
				foreach(MethodDef method in type.Methods) {
					if(method.HasBody) {
						IList<Instruction> instructions = method.Body.Instructions;
						for(int i = 0; i < instructions.Count; i++) {
							Instruction instruction = instructions[i];
							if(instruction.Operand is FieldDef field && boolFields.Contains(field)) {
								FieldDef intField;
								int index;
								if(field.IsStatic) {
									index = staticBoolFields.IndexOf(field);
									intField = staticIntegerFields[index / 32];
								} else {
									index = boolFieldsByType[field.DeclaringType].IndexOf(field);
									intField = integerFieldsByType[field.DeclaringType][index / 32];
								}
								index %= 32;
								if(instruction.OpCode == OpCodes.Ldfld || instruction.OpCode == OpCodes.Ldsfld) {
									instruction.Operand = intField;
									instructions.Insert(++i, CreateIntOpCode(1 << index));
									instructions.Insert(++i, OpCodes.And.ToInstruction());
								} else if(instruction.OpCode == OpCodes.Stfld || instruction.OpCode == OpCodes.Stsfld) {
									OpCode prevCode = instructions[i - 1].OpCode;
									bool? value = null;
									if(prevCode == OpCodes.Ldc_I4_0) value = false;
									if(prevCode == OpCodes.Ldc_I4_1) value = true;
									if(value != null) {
										Instruction newInstruction = instructions[i - 1];
										if(!field.IsStatic) instructions.Insert(i++ - 2, OpCodes.Dup.ToInstruction());
										newInstruction.OpCode = field.IsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld;
										newInstruction.Operand = intField;
										if(value.Value) {
											instructions.Insert(i++, CreateIntOpCode(1 << index));
											instruction.OpCode = OpCodes.Or;
											instruction.Operand = null;
										} else {
											instructions.Insert(i++, CreateIntOpCode(~(1 << index)));
											instruction.OpCode = OpCodes.And;
											instruction.Operand = null;
										}
										instructions.Insert(++i, (field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld).ToInstruction(intField));
									} else if(field.IsStatic) {
										instruction.OpCode = OpCodes.Ldsfld;
										instruction.Operand = intField;
										Instruction falseInstruction = CreateIntOpCode(~(1 << index));
										instructions.Insert(++i, OpCodes.Brfalse_S.ToInstruction(falseInstruction));
										instructions.Insert(++i, CreateIntOpCode(1 << index));
										instructions.Insert(++i, OpCodes.Or.ToInstruction());
										Instruction endInstruction = OpCodes.Stsfld.ToInstruction(intField);
										instructions.Insert(++i, OpCodes.Br_S.ToInstruction(endInstruction));
										instructions.Insert(++i, falseInstruction);
										instructions.Insert(++i, OpCodes.And.ToInstruction());
										instructions.Insert(++i, endInstruction);
									} else {
										Local local = new(type.Module.CorLibTypes.Boolean);
										method.Body.Variables.Add(local);
										instructions.Insert(i++, OpCodes.Stloc_S.ToInstruction(local));
										instructions.Insert(i++, OpCodes.Dup.ToInstruction());
										instruction.OpCode = OpCodes.Ldfld;
										instruction.Operand = intField;
										instructions.Insert(++i, OpCodes.Ldloc_S.ToInstruction(local));
										Instruction falseInstruction = CreateIntOpCode(~(1 << index));
										instructions.Insert(++i, OpCodes.Brfalse_S.ToInstruction(falseInstruction));
										instructions.Insert(++i, CreateIntOpCode(1 << index));
										instructions.Insert(++i, OpCodes.Or.ToInstruction());
										Instruction endInstruction = OpCodes.Stfld.ToInstruction(intField);
										instructions.Insert(++i, OpCodes.Br_S.ToInstruction(endInstruction));
										instructions.Insert(++i, falseInstruction);
										instructions.Insert(++i, OpCodes.And.ToInstruction());
										instructions.Insert(++i, endInstruction);
									}
								}
							}
						}
					}
				}
				foreach(TypeDef typeDef in type.GetTypes()) ConvertBoolToIntIL(typeDef);
			}

			private Instruction CreateIntOpCode(int value) {
				return value switch {
					-1 => OpCodes.Ldc_I4_M1.ToInstruction(),
					0 => OpCodes.Ldc_I4_0.ToInstruction(),
					1 => OpCodes.Ldc_I4_1.ToInstruction(),
					2 => OpCodes.Ldc_I4_2.ToInstruction(),
					3 => OpCodes.Ldc_I4_3.ToInstruction(),
					4 => OpCodes.Ldc_I4_4.ToInstruction(),
					5 => OpCodes.Ldc_I4_5.ToInstruction(),
					6 => OpCodes.Ldc_I4_6.ToInstruction(),
					7 => OpCodes.Ldc_I4_7.ToInstruction(),
					8 => OpCodes.Ldc_I4_8.ToInstruction(),
					> 0 and < 256 => OpCodes.Ldc_I4_S.ToInstruction((byte) value),
					_ => OpCodes.Ldc_I4.ToInstruction(value)
				};
			}
		}
	}
}
