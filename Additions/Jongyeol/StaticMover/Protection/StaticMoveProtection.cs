﻿using System;
using System.Collections.Generic;
using System.Linq;
using Confuser.Core;
using Confuser.Core.Services;
using ConfuserEx_Additions.Jongyeol;
using ConfuserEx.API;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Utils = ConfuserEx_Additions.Jongyeol.Utils;

namespace Confuser.Protections {
    [BeforeProtection("Ki.ControlFlow")]
    [AfterProtection("Ki.RefProxy")]
    public class StaticMoveProtection : Protection {
        public override ProtectionPreset Preset => ProtectionPreset.Minimum;
        public override string Name => "Static Mover";
        public override string Description => "All Static Code Move Global Type";
        public override string Id => "static mover";
        public override string FullId => "Jongyeol.staticMover";
        protected override void Initialize(ConfuserContext context) { }

        protected override void PopulatePipeline(ProtectionPipeline pipeline) {
            pipeline.InsertPreStage(PipelineStage.OptimizeMethods, new StaticMovePhase(this));
        }

        private class StaticMovePhase : ProtectionPhase {

            public StaticMovePhase(ConfuserComponent parent) : base(parent) { }

            public override ProtectionTargets Targets => ProtectionTargets.Modules;

            public override string Name => "Static mover";

            public Dictionary<MethodDef, MethodDefUser> MethodMap = new();

            public Dictionary<TypeDef, List<IMemberDef>> NeedCtorMembers = new();

            protected override void Execute(ConfuserContext context, ProtectionParameters parameters) {
                IMarkerService marker = context.Registry.GetService<IMarkerService>();
                foreach(ModuleDefMD module in parameters.Targets.OfType<ModuleDefMD>().WithProgress(context.Logger)) {
                    TypeDef globalType = module.GlobalType;
                    TypeDefUser moveType = new(Rename.RandomName(), Rename.RandomName(), globalType.Module.CorLibTypes.Object.TypeDefOrRef);
                    globalType.Module.Types.Add(moveType);
                    marker.Mark(moveType, Parent);
                    foreach(TypeDef type in module.Types) MoveMembers(type, globalType, StaticMoveTargets.All);
                    foreach(TypeDef type in module.Types) CheckFunction(type, globalType, moveType, marker);
                    CheckFunction(moveType, globalType, moveType, marker);
                    HashSet<TypeDef> needCtors1 = [];
                    HashSet<TypeDef> needCtors2 = [];
                    foreach(KeyValuePair<TypeDef,List<IMemberDef>> pair in NeedCtorMembers) {
                        bool ctor1 = false;
                        bool ctor2 = false;
                        foreach(IMemberDef member in pair.Value) {
                            if(member.DeclaringType == globalType) {
                                ctor1 = true;
                                needCtors1.Add(pair.Key);
                            } else if(member.DeclaringType == moveType) {
                                ctor2 = true;
                                needCtors2.Add(pair.Key);
                            } else context.Logger.Debug("Unknown Member: " + member.FullName);
                            if(ctor1 && ctor2) break;
                        }
                    }
                    Dictionary<TypeDef, MethodDefUser> methodMap = new();
                    MethodDef cctor = globalType.FindOrCreateStaticConstructor();
                    foreach(TypeDef typeDef in needCtors1.OrderBy(_ => Utils.Random.Next())) {
                        if(typeDef.IsNestedPrivate) continue;
                        MethodDefUser method = new(Rename.RandomName(), MethodSig.CreateStatic(globalType.Module.CorLibTypes.Void), MethodImplAttributes.Managed | MethodImplAttributes.IL, MethodAttributes.Assembly | MethodAttributes.Static);
                        typeDef.Methods.Add(method);
                        marker.Mark(typeDef, Parent);
                        method.Body = new CilBody();
                        method.Body.Instructions.Insert(0, OpCodes.Ret.ToInstruction());
                        methodMap.Add(typeDef, method);
                        cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, method));
                    }
                    cctor = moveType.FindOrCreateStaticConstructor();
                    marker.Mark(cctor, Parent);
                    foreach(TypeDef typeDef in needCtors2.OrderBy(_ => Utils.Random.Next())) {
                        if(!methodMap.TryGetValue(typeDef, out MethodDefUser method)) {
                            if(typeDef.IsNestedPrivate) continue;
                            method = new MethodDefUser(Rename.RandomName(), MethodSig.CreateStatic(globalType.Module.CorLibTypes.Void), MethodImplAttributes.Managed | MethodImplAttributes.IL, MethodAttributes.Assembly | MethodAttributes.Static);
                            typeDef.Methods.Add(method);
                            marker.Mark(typeDef, Parent);
                            method.Body = new CilBody();
                            method.Body.Instructions.Insert(0, OpCodes.Ret.ToInstruction());
                            methodMap.Add(typeDef, method);
                        }
                        cctor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, method));
                    }
                }
            }

            private void MoveMembers(TypeDef type, TypeDef globalType, StaticMoveTargets targets) {
                targets = ParseTargets(targets, type.CustomAttributes);
                if(type != globalType) {
                    StaticMoveTargets curTargets;
                    MethodDef[] methods = type.Methods.ToArray();
                    List<IMemberDef> movedMembers = [];
                    foreach(MethodDef method in methods) {
                        if(!method.HasBody || !method.Body.HasInstructions || !method.IsStatic || method.IsSpecialName || method.IsPublic && type.IsPublic) continue;
                        curTargets = ParseTargets(targets, method.CustomAttributes);
                        if(!curTargets.HasFlag(StaticMoveTargets.Method)) continue;
                        method.DeclaringType = globalType;
                        if(method.IsPrivate) method.Access = MethodAttributes.Assembly;
                        movedMembers.Add(method);
                        foreach(Instruction instruction in method.Body.Instructions) {
                            if(instruction.Operand is MethodDef method2) {
                                if(method2.IsPrivate) method2.Access = MethodAttributes.Assembly;
                                else if(method2.Access is MethodAttributes.Family or MethodAttributes.FamANDAssem) method2.Access = MethodAttributes.FamORAssem;
                                if(method2.DeclaringType.IsNestedPrivate)
                                    method2.DeclaringType.Attributes = method2.DeclaringType.Attributes & ~TypeAttributes.VisibilityMask | TypeAttributes.NestedAssembly;
                                else if(method2.DeclaringType.IsNestedFamily || method2.DeclaringType.IsNestedFamilyAndAssembly)
                                    method2.DeclaringType.Attributes = method2.DeclaringType.Attributes & ~TypeAttributes.VisibilityMask | TypeAttributes.NestedFamORAssem;
                            } else if(instruction.Operand is FieldDef field) {
                                if(field.IsPrivate) field.Access = FieldAttributes.Assembly;
                                else if(field.Access is FieldAttributes.Family or FieldAttributes.FamANDAssem) field.Access = FieldAttributes.FamORAssem;
                                if(field.DeclaringType.IsNestedPrivate)
                                    field.DeclaringType.Attributes = field.DeclaringType.Attributes & ~TypeAttributes.VisibilityMask | TypeAttributes.NestedAssembly;
                                else if(field.DeclaringType.IsNestedFamily || field.DeclaringType.IsNestedFamilyAndAssembly)
                                    field.DeclaringType.Attributes = field.DeclaringType.Attributes & ~TypeAttributes.VisibilityMask | TypeAttributes.NestedFamORAssem;
                            }
                        }
                    }
                    FieldDef[] fields = type.Fields.ToArray();
                    foreach(FieldDef field in fields) {
                        if(!field.IsStatic || field.IsSpecialName || field.IsPublic && type.IsPublic) continue;
                        curTargets = ParseTargets(targets, field.CustomAttributes);
                        if(curTargets.HasFlag(StaticMoveTargets.Field)) field.DeclaringType = globalType;
                        if(field.IsPrivate) field.Access = FieldAttributes.Assembly;
                        movedMembers.Add(field);
                    }
                    PropertyDef[] properties = type.Properties.ToArray();
                    foreach(PropertyDef property in properties) {
                        if(!property.IsStatic() || property.IsSpecialName || property.IsPublic() && type.IsPublic) continue;
                        curTargets = ParseTargets(targets, property.CustomAttributes);
                        if(!curTargets.HasFlag(StaticMoveTargets.Property)) continue;
                        property.DeclaringType = globalType;
                        foreach(MethodDef method in property.GetMethods) {
                            method.DeclaringType = globalType;
                            if(method.IsPrivate) method.Access = MethodAttributes.Assembly;
                            movedMembers.Add(method);
                        }
                    }
                    EventDef[] events = type.Events.ToArray();
                    foreach(EventDef @event in events) {
                        if(!@event.IsStatic() || @event.IsSpecialName || type.IsPublic) continue;
                        curTargets = ParseTargets(targets, @event.CustomAttributes);
                        if(curTargets.HasFlag(StaticMoveTargets.Event)) @event.DeclaringType = globalType;
                    }
                    MethodDef cctor = type.FindStaticConstructor();
                    if(cctor is { HasBody: true, Body.HasInstructions: true }) {
                        List<IMemberDef> needCtorMembers = [];
                        foreach(Instruction instruction in cctor.Body.Instructions)
                            if(instruction.Operand is IMemberDef member && member.DeclaringType == globalType && movedMembers.Contains(member))
                                needCtorMembers.Add(member);
                        if(needCtorMembers.Count > 0) NeedCtorMembers[type] = needCtorMembers;
                    }
                } else foreach(MethodDef method in type.Methods) if(method.IsPrivate) method.Access = MethodAttributes.Assembly;
                foreach(TypeDef typeDef in type.GetTypes()) MoveMembers(typeDef, globalType, targets);
            }

            private void CheckFunction(TypeDef type, TypeDef globalType, TypeDefUser moveType, IMarkerService marker) {
                foreach(MethodDef method in type == moveType ? type.Methods.ToArray() : type.Methods) {
                    if(!method.HasBody || !method.Body.HasInstructions) continue;
                    bool noMovedStatic = CheckNoMovedStatic(method.CustomAttributes);
                    foreach(Instruction instruction in method.Body.Instructions) {
                        if(instruction.OpCode == OpCodes.Ldftn) {
                            IMemberRef iMemberRef = instruction.Operand as IMemberRef;
                            if(iMemberRef.DeclaringType != globalType) continue;
                            if(iMemberRef is MethodDef methodDef) {
                                if(!MethodMap.TryGetValue(methodDef, out MethodDefUser methodDefUser)) methodDefUser = MakeNewMethod(methodDef, moveType, marker);
                                instruction.Operand = methodDefUser;
                            } else if(iMemberRef is FieldDef fieldDef) fieldDef.DeclaringType = moveType;
                        }
                        if(!noMovedStatic) continue;
                        if(instruction.Operand is MethodDef method2) {
                            if(method2.DeclaringType != globalType) continue;
                            if(!MethodMap.TryGetValue(method2, out MethodDefUser methodDefUser)) methodDefUser = MakeNewMethod(method2, moveType, marker);
                            instruction.Operand = methodDefUser;
                        } else if(instruction.Operand is FieldDef fieldDef) {
                            if(fieldDef.DeclaringType != globalType) continue;
                            fieldDef.DeclaringType = moveType;
                        }
                    }
                }
            }

            private MethodDefUser MakeNewMethod(MethodDef methodDef, TypeDefUser moveType, IMarkerService marker) {
                MethodDefUser methodDefUser = new(Rename.RandomName(), methodDef.MethodSig.Clone(), MethodImplAttributes.Managed | MethodImplAttributes.IL, MethodAttributes.Assembly | MethodAttributes.Static) {
                    Body = new CilBody()
                };
                foreach(CustomAttribute customAttribute in methodDef.CustomAttributes) methodDefUser.CustomAttributes.Add(customAttribute);
                for(ushort i = 0; i < methodDefUser.Parameters.Count; i++) {
                    Instruction instruction;
                    switch(i) {
                        case 0:
                            instruction = OpCodes.Ldarg_0.ToInstruction();
                            break;
                        case 1:
                            instruction = OpCodes.Ldarg_1.ToInstruction();
                            break;
                        case 2:
                            instruction = OpCodes.Ldarg_2.ToInstruction();
                            break;
                        case 3:
                            instruction = OpCodes.Ldarg_3.ToInstruction();
                            break;
                        default:
                            Parameter parameter = methodDefUser.Parameters[i];
                            instruction = i < 256 ? OpCodes.Ldarg_S.ToInstruction(parameter) : OpCodes.Ldarg.ToInstruction(parameter);
                            break;
                    }
                    methodDefUser.Body.Instructions.Add(instruction);
                    ParamDef param = methodDef.ParamDefs[i];
                    methodDefUser.ParamDefs.Add(new ParamDefUser(param.Name, param.Sequence, param.Attributes));
                }
                methodDefUser.Body.Instructions.Add(OpCodes.Call.ToInstruction(methodDef));
                methodDefUser.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
                MethodMap.Add(methodDef, methodDefUser);
                moveType.Methods.Add(methodDefUser);
                marker.Mark(methodDefUser, Parent);
                return methodDefUser;
            }

            private static StaticMoveTargets ParseTargets(StaticMoveTargets targets, CustomAttributeCollection attributes) {
                if(attributes == null) return targets;
                List<CustomAttribute> removeAttributes = [];
                foreach(CustomAttribute attribute in attributes)
                    if(attribute.TypeFullName == typeof(IncludeStaticMoveAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets |= (StaticMoveTargets) attribute.ConstructorArguments[0].Value;
                    } else if(attribute.TypeFullName == typeof(ExcludeStaticMoveAttribute).FullName) {
                        removeAttributes.Add(attribute);
                        targets &= ~(StaticMoveTargets) attribute.ConstructorArguments[0].Value;
                    }
                foreach(CustomAttribute attribute in removeAttributes) attributes.Remove(attribute);
                return targets;
            }

            private static bool CheckNoMovedStatic(CustomAttributeCollection attributes) {
                if(attributes == null) return false;
                foreach(CustomAttribute attribute in attributes)
                    if(attribute.TypeFullName == typeof(NoMovedStaticAttribute).FullName) {
                        attributes.Remove(attribute);
                        return true;
                    }
                return false;
            }
        }
    }
}